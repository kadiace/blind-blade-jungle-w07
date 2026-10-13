using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.AI;

using Random = UnityEngine.Random;

/// <summary>
/// 대숲 공터의 무한 생존 시간에 비례해 투척수와 근접 적의 수를 같은 비율로 늘린다.
/// 플레이어 주변에는 스폰하지 않고 동시에 날아오는 수리검 수를 제한하며 처치 수를 기록한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BambooDefenseDirector : MonoBehaviour
{
    [Header("시작")]
    [SerializeField, Min(0f), Tooltip("세션이 시작된 뒤 첫 스폰까지의 준비 시간(초)")]
    private float _startDelay = 2f;
    private float _countdown;
    private bool _started;
    private float _elapsed;
    private int _kills;

    [Header("플레이어 이동 추적")]
    [SerializeField, Min(0.1f), Tooltip("수리검 예측에 쓰는 플레이어 수평 속도의 평활 계수. 클수록 방향 전환에 빨리 반응한다.")]
    private float _playerVelocitySmoothing = 8f;
    private Vector3 _playerVelocity;
    private Vector3 _lastPlayerPosition;
    private bool _hasPlayerSample;

    [Header("스폰 공통")]
    [SerializeField, Min(0f), Tooltip("플레이어와 이 수평 거리 안에서는 적을 스폰하지 않는다.")]
    private float _minimumSpawnDistance = 8f;
    [SerializeField] private Transform _unitRoot;

    [Header("투척수")]
    [SerializeField] private BambooThrower _throwerPrefab;
    [SerializeField, Min(0f)] private float _throwerMinimumRadius = 25.4f;
    [SerializeField, Min(0f)] private float _throwerMaximumRadius = 29f;
    [SerializeField, Min(1f), Tooltip("플레이어와 이 수평 거리보다 먼 숲에는 투척수를 배치하지 않는다.")]
    private float _throwerMaximumDistance = 30f;
    [SerializeField, Min(0f)] private float _throwerMinimumHeight = 0.6f;
    [SerializeField, Min(0f)] private float _throwerMaximumHeight = 5.5f;
    [SerializeField, Min(0f), Tooltip("투척수끼리 유지하는 최소 거리(미터)")]
    private float _throwerSeparation = 3f;
    [SerializeField, Min(0)] private int _initialThrowers = 3;
    [SerializeField, Min(0f), Tooltip("분당 늘어나는 목표 투척수")]
    private float _throwersPerMinute = 3f;
    [SerializeField, Min(1), Tooltip("성능 보호용 최대 투척수")]
    private int _maximumThrowers = 12;
    [SerializeField, Min(0f)] private float _throwerSpawnInterval = 1.33f;
    private readonly List<BambooThrower> _activeThrowers = new List<BambooThrower>();
    private readonly Stack<BambooThrower> _idleThrowers = new Stack<BambooThrower>();
    private float _nextThrowerSpawnAt;

    [Header("동시 투척 제한")]
    [SerializeField, Min(1)] private int _maximumConcurrentShurikens = 3;
    [SerializeField, Min(0f), Tooltip("연속 투척 허가 사이의 최소 간격(초)")]
    private float _minimumThrowGap = 0.25f;
    [SerializeField, Tooltip("가까운 근접 적이 공격을 준비하는 동안 새 투척을 미룬다.")]
    private bool _deferDuringMeleeTelegraph = true;
    [SerializeField, Min(0f)] private float _meleeTelegraphRadius = 4f;
    private int _activeShurikens;
    private int _reservedThrows;
    private float _nextThrowAllowedAt;

    [Header("근접 적")]
    [SerializeField] private GameObject _meleePrefab;
    [SerializeField, Min(0f), Tooltip("근접 적이 나타나는 최소 반경. 숲 안쪽 가장자리 근처로 설정한다.")]
    private float _meleeSpawnMinimumRadius = 24f;
    [SerializeField, Min(0f), Tooltip("근접 적이 나타나는 최대 반경. 플레이어까지 경로가 있는 숲 안 위치만 사용한다.")]
    private float _meleeSpawnMaximumRadius = 28.6f;
    [SerializeField, Min(0f)] private float _meleeStartTime = 5f;
    [SerializeField, Min(0.01f), Tooltip("근접 적 등장 시작 시점의 분당 스폰 수")]
    private float _meleeSpawnsPerMinute = 15f;
    [SerializeField, Min(0f), Tooltip("1분마다 늘어나는 분당 스폰 수")]
    private float _meleeSpawnGrowthPerMinute = 9f;
    [SerializeField, Min(0f), Tooltip("이 시간 이후 돌진형 근접 적을 섞는다.")]
    private float _chargeTypeStartTime = 90f;
    [SerializeField, Range(0f, 1f)] private float _chargeTypeChance = 0.4f;
    [SerializeField, Min(1), Tooltip("성능 보호용 최대 근접 적 수")]
    private int _maximumMelee = 30;
    private readonly List<MeleeUnit> _activeMelee = new List<MeleeUnit>();
    private readonly Stack<MeleeUnit> _idleMelee = new Stack<MeleeUnit>();
    private readonly Dictionary<EnemyHealth, MeleeUnit> _meleeByHealth = new Dictionary<EnemyHealth, MeleeUnit>();
    private NavMeshPath _meleePath;
    private float _nextMeleeSpawnAt;

    public bool HasStarted => _started;
    public float Countdown => _countdown;
    public float SurvivalTime => _elapsed;
    public int Kills => _kills;
    public Vector3 PlayerVelocity => _playerVelocity;
    public event Action<Vector3> ThrowTelegraphed;
    public event Action<DefenseShuriken> ShurikenLaunched;

    private sealed class MeleeUnit
    {
        public GameObject Root { get; }
        public EnemyHealth Health { get; }
        public EnemyController Controller { get; }
        public EnemyAttackSelector Selector { get; }
        public EnemyChargeAttack Charge { get; }

        /// <summary>root에서 근접 적의 체력·추적·공격 선택·돌진 컴포넌트를 찾아 보관한다.</summary>
        public MeleeUnit(GameObject root)
        {
            Root = root;
            Health = root.GetComponent<EnemyHealth>();
            Controller = root.GetComponent<EnemyController>();
            Selector = root.GetComponent<EnemyAttackSelector>();
            Charge = root.GetComponent<EnemyChargeAttack>();
        }
    }

    void Awake()
    {
        _countdown = _startDelay;
        _meleePath = new NavMeshPath();
        if (_throwerPrefab == null || _meleePrefab == null)
        {
            Debug.LogError("대숲 방어 디렉터의 투척수·근접 적 프리팹 연결이 누락되었습니다.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (!Game.IsInitialized || Game.Player == null || Game.Instance.GameStateManager == null
            || Game.Instance.GameStateManager.State != GameplayState.Playing)
            return;

        TrackPlayerVelocity();
        if (!_started)
        {
            _countdown = Mathf.Max(0f, _countdown - Time.deltaTime);
            if (_countdown > 0f)
                return;
            _started = true;
            _nextMeleeSpawnAt = Time.time + Mathf.Max(0f, _meleeStartTime);
        }

        _elapsed += Time.deltaTime;
        UpdateThrowers();
        UpdateMelee();
    }

    /// <summary>
    /// 현재 플레이어의 프레임 간 수평 이동으로 속도를 구하고 평활 계수로 부드럽게 갱신한다.
    /// 회피 순간의 급격한 속도는 완화되며 결과는 수리검 예측 조준에 사용한다.
    /// </summary>
    private void TrackPlayerVelocity()
    {
        Vector3 position = Game.Player.transform.position;
        if (_hasPlayerSample && Time.deltaTime > 0f)
        {
            Vector3 velocity = Vector3.ProjectOnPlane(position - _lastPlayerPosition, Vector3.up) / Time.deltaTime;
            _playerVelocity = Vector3.Lerp(_playerVelocity, velocity,
                1f - Mathf.Exp(-_playerVelocitySmoothing * Time.deltaTime));
        }
        _lastPlayerPosition = position;
        _hasPlayerSample = true;
    }

    /// <summary>
    /// 생존 시간에 비례하는 목표 투척수보다 적으면 스폰 간격마다 빈 위치에 투척수를 하나 배치한다.
    /// 목표 수는 시작 수에 분당 증가량을 선형으로 더하고 성능 보호 상한으로 제한한다.
    /// </summary>
    private void UpdateThrowers()
    {
        int target = Mathf.Min(_maximumThrowers,
            _initialThrowers + Mathf.FloorToInt(_elapsed / 60f * _throwersPerMinute));
        if (_activeThrowers.Count >= target || Time.time < _nextThrowerSpawnAt)
            return;
        if (!TryFindThrowerPosition(null, out Vector3 position))
            return;

        BambooThrower thrower = _idleThrowers.Count > 0 ? _idleThrowers.Pop() : CreateThrower();
        _activeThrowers.Add(thrower);
        thrower.Activate(this, position);
        _nextThrowerSpawnAt = Time.time + _throwerSpawnInterval;
    }

    /// <summary>
    /// 근접 적 스폰 시각이 되면 다음 시각을 생존 시간에 비례해 당기고 숲 가장자리의 도달 가능한 위치에 근접 적을 배치한다.
    /// 분당 스폰 수는 시작값에 분당 증가량을 선형으로 더하며 동시 생존 상한을 넘으면 이번 스폰을 건너뛴다.
    /// </summary>
    private void UpdateMelee()
    {
        if (_meleePrefab == null || Time.time < _nextMeleeSpawnAt)
            return;
        float minutes = Mathf.Max(0f, _elapsed - _meleeStartTime) / 60f;
        float spawnsPerMinute = _meleeSpawnsPerMinute + _meleeSpawnGrowthPerMinute * minutes;
        _nextMeleeSpawnAt = Time.time + 60f / Mathf.Max(0.01f, spawnsPerMinute);
        if (_activeMelee.Count >= _maximumMelee || !TryFindMeleePosition(out Vector3 position))
            return;

        MeleeUnit unit = _idleMelee.Count > 0 ? _idleMelee.Pop() : CreateMelee();
        bool chargeType = _elapsed >= _chargeTypeStartTime && Random.value < _chargeTypeChance;
        Vector3 toCenter = Vector3.ProjectOnPlane(transform.position - position, Vector3.up);
        unit.Root.transform.SetPositionAndRotation(position,
            toCenter.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCenter) : Quaternion.identity);
        unit.Charge.enabled = chargeType;
        unit.Root.SetActive(true);
        unit.Controller.SetTarget(Game.Player.transform);
        _activeMelee.Add(unit);
    }

    /// <summary>
    /// 동시 투척 수·최소 간격·근접 전조 조건을 만족하면 한 번의 투척을 예약하고 true를 반환한다.
    /// 비행 중인 수리검과 전조 중인 예약을 합쳐 상한을 넘지 않게 한다.
    /// </summary>
    internal bool TryReserveThrow()
    {
        if (!_started || _activeShurikens + _reservedThrows >= _maximumConcurrentShurikens
            || Time.time < _nextThrowAllowedAt || IsMeleeTelegraphNearPlayer())
            return false;
        _reservedThrows++;
        _nextThrowAllowedAt = Time.time + _minimumThrowGap;
        return true;
    }

    /// <summary>던지지 못한 투척 예약 하나를 반환한다. 예약 수는 0 아래로 내려가지 않는다.</summary>
    internal void CancelThrowReservation()
    {
        _reservedThrows = Mathf.Max(0, _reservedThrows - 1);
    }

    /// <summary>
    /// 예약 하나를 shuriken의 비행으로 전환하고 비행 종료 시 동시 투척 수를 줄이도록 구독한다.
    /// 방향 표시 등 구독자에게 새 비행을 알린다.
    /// </summary>
    internal void NotifyShurikenLaunched(DefenseShuriken shuriken)
    {
        _reservedThrows = Mathf.Max(0, _reservedThrows - 1);
        _activeShurikens++;
        shuriken.Released += HandleShurikenReleased;
        ShurikenLaunched?.Invoke(shuriken);
    }

    /// <summary>position에서 투척 전조가 시작됐음을 방향 표시 등 구독자에게 알린다.</summary>
    internal void NotifyThrowTelegraph(Vector3 position)
    {
        ThrowTelegraphed?.Invoke(position);
    }

    /// <summary>
    /// 숲 고리 안의 무작위 각도·반경·높이 중 플레이어 최소·최대 거리와 다른 투척수 간격을 만족하는 위치를 찾는다.
    /// self는 간격 검사에서 제외할 투척수이며 찾으면 position을 채우고 true를 반환한다.
    /// </summary>
    internal bool TryFindThrowerPosition(BambooThrower self, out Vector3 position)
    {
        Vector3 player = Game.Player.transform.position;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(_throwerMinimumRadius, Mathf.Max(_throwerMinimumRadius, _throwerMaximumRadius));
            float height = Random.Range(_throwerMinimumHeight, Mathf.Max(_throwerMinimumHeight, _throwerMaximumHeight));
            position = transform.position + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            float distance = HorizontalDistance(position, player);
            if (distance < _minimumSpawnDistance || distance > _throwerMaximumDistance || IsCrowded(self, position))
                continue;
            return true;
        }
        position = default;
        return false;
    }

    /// <summary>position과 다른 활성 투척수의 수평 거리가 최소 간격보다 가까우면 true를 반환한다. self는 제외한다.</summary>
    private bool IsCrowded(BambooThrower self, Vector3 position)
    {
        foreach (BambooThrower thrower in _activeThrowers)
        {
            if (thrower != self && HorizontalDistance(thrower.transform.position, position) < _throwerSeparation)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 숲 가장자리 반경의 무작위 위치 중 NavMesh 위이고 플레이어까지 완전한 경로가 있으며 최소 거리 이상인 곳을 찾는다.
    /// 찾으면 position을 채우고 true를 반환하며 모든 시도가 실패하면 false를 반환한다.
    /// </summary>
    private bool TryFindMeleePosition(out Vector3 position)
    {
        Vector3 player = Game.Player.transform.position;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(_meleeSpawnMinimumRadius, Mathf.Max(_meleeSpawnMinimumRadius, _meleeSpawnMaximumRadius));
            Vector3 desired = transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)
                || HorizontalDistance(hit.position, player) < _minimumSpawnDistance
                || !NavMesh.CalculatePath(hit.position, player, NavMesh.AllAreas, _meleePath)
                || _meleePath.status != NavMeshPathStatus.PathComplete)
                continue;
            position = hit.position;
            return true;
        }
        position = default;
        return false;
    }

    /// <summary>가까운 활성 근접 적이 공격 전조 중이면 true를 반환한다. 설정이 꺼져 있으면 항상 false다.</summary>
    private bool IsMeleeTelegraphNearPlayer()
    {
        if (!_deferDuringMeleeTelegraph)
            return false;
        Vector3 player = Game.Player.transform.position;
        foreach (MeleeUnit unit in _activeMelee)
        {
            EnemyAttackBehaviour attack = unit.Selector.CurrentAttack;
            if (attack != null && attack.Phase == EnemyAttackPhase.Telegraph
                && HorizontalDistance(unit.Root.transform.position, player) <= _meleeTelegraphRadius)
                return true;
        }
        return false;
    }

    /// <summary>투척수 프리팹을 비활성 상태로 생성하고 사망 사건을 구독해 반환한다.</summary>
    private BambooThrower CreateThrower()
    {
        BambooThrower thrower = Instantiate(_throwerPrefab, transform.position, Quaternion.identity, _unitRoot);
        thrower.gameObject.SetActive(false);
        thrower.Health.Died += HandleThrowerDied;
        return thrower;
    }

    /// <summary>근접 적 프리팹을 비활성 상태로 생성하고 구성 요소와 사망 사건 구독을 등록해 반환한다.</summary>
    private MeleeUnit CreateMelee()
    {
        GameObject root = Instantiate(_meleePrefab, transform.position, Quaternion.identity, _unitRoot);
        root.SetActive(false);
        MeleeUnit unit = new MeleeUnit(root);
        _meleeByHealth.Add(unit.Health, unit);
        unit.Health.Died += HandleMeleeDied;
        return unit;
    }

    /// <summary>health의 투척수 사망을 처치 수에 더하고 다음 재사용을 위해 대기 목록으로 옮긴다.</summary>
    private void HandleThrowerDied(EnemyHealth health)
    {
        BambooThrower thrower = health.GetComponent<BambooThrower>();
        if (!_activeThrowers.Remove(thrower))
            return;
        _kills++;
        _idleThrowers.Push(thrower);
        _nextThrowerSpawnAt = Mathf.Max(_nextThrowerSpawnAt, Time.time + _throwerSpawnInterval);
    }

    /// <summary>health의 근접 적 사망을 처치 수에 더하고 다음 재사용을 위해 대기 목록으로 옮긴다.</summary>
    private void HandleMeleeDied(EnemyHealth health)
    {
        if (!_meleeByHealth.TryGetValue(health, out MeleeUnit unit) || !_activeMelee.Remove(unit))
            return;
        _kills++;
        _idleMelee.Push(unit);
    }

    /// <summary>shuriken의 비행 종료를 동시 투척 수에서 제외하고 구독을 해제한다.</summary>
    private void HandleShurikenReleased(DefenseShuriken shuriken)
    {
        shuriken.Released -= HandleShurikenReleased;
        _activeShurikens = Mathf.Max(0, _activeShurikens - 1);
    }

    /// <summary>first와 second의 높이를 제외한 수평 거리를 반환한다.</summary>
    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }
}
