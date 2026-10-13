using System;

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyTelemetrySource : MonoBehaviour
{
    [Header("배치 정보")]
    [SerializeField] private string _enemyId;
    [SerializeField] private TelemetryEnemyType _enemyType = TelemetryEnemyType.Melee;
    [SerializeField] private bool _countsForStageClear = true;

    [Header("수집 상태")]
    private TelemetryManager _manager;
    private EnemyHealth _health;
    private EnemyController _controller;
    private BossController _boss;
    private TwinBossController _twins;
    private EnemyAttackBehaviour[] _patterns;
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private EnemyAttackTrace _activeMeleeTrace;
    private bool _rangedAttempt;
    private bool _rangedSuccess;

    public string EnemyId => _enemyId;
    public TelemetryEnemyType EnemyType => _enemyType;
    public bool CountsForStageClear => _countsForStageClear;
    public EnemyHealth Health => _health;
    public event Action<EnemyTelemetrySource, bool> LifeStateChanged;

    /// <summary>manager와 twins를 연결하고 적의 기존 상태 조회 컴포넌트와 마지막 위치를 캐시한다.</summary>
    public void Bind(TelemetryManager manager, TwinBossController twins)
    {
        _manager = manager;
        _health = GetComponent<EnemyHealth>();
        _controller = GetComponent<EnemyController>();
        _boss = GetComponent<BossController>();
        _patterns = GetComponents<EnemyAttackBehaviour>();
        _twins = twins;
        _rangedAttempt = _rangedSuccess = false;
        CapturePosition();
    }

    /// <summary>kind의 실행 공격을 시작하고 시도 이벤트와 원거리 래치 또는 근거리 활성 문맥을 기록한다.</summary>
    public EnemyAttackTrace BeginAttack(TelemetryAttackKind kind)
    {
        if (_manager == null) return null;
        _manager.EnsureAttemptStarted();
        if (!_manager.IsRecording) return null;
        var trace = new EnemyAttackTrace(this, kind, _manager);
        if (kind == TelemetryAttackKind.Ranged) _rangedAttempt = true;
        else _activeMeleeTrace = trace;
        _manager.RecordEnemyAttack(trace, false);
        return trace;
    }

    /// <summary>source의 배치 컴포넌트에서 kind 공격 문맥을 생성하며 로깅이 연결되지 않은 적은 null을 반환한다.</summary>
    public static EnemyAttackTrace BeginAttack(GameObject source, TelemetryAttackKind kind)
    {
        return source.TryGetComponent(out EnemyTelemetrySource telemetry) ? telemetry.BeginAttack(kind) : null;
    }

    /// <summary>kind가 원거리이면 성공 래치를 설정한다. 횟수는 공격 문맥이 별도로 집계한다.</summary>
    public void MarkAttackSuccess(TelemetryAttackKind kind)
    {
        if (kind == TelemetryAttackKind.Ranged) _rangedSuccess = true;
    }

    /// <summary>trace가 현재 근거리 공격 문맥이면 활성 상태를 종료한다.</summary>
    public void EndAttack(EnemyAttackTrace trace)
    {
        if (_activeMeleeTrace == trace) _activeMeleeTrace = null;
    }

    /// <summary>현재 적 위치를 캐시해 반환하며 파괴된 공격 주체는 마지막 확인 위치를 유지한다.</summary>
    public Vector3 CapturePosition()
    {
        if (this != null)
        {
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }
        return _lastPosition;
    }

    /// <summary>time과 tick 기준의 적 상태를 순수 데이터로 복사하고 원거리 사건 래치를 초기화해 반환한다.</summary>
    public object CaptureSnapshot(double time, long tick)
    {
        CapturePosition();
        bool melee = false;
        foreach (EnemyAttackBehaviour pattern in _patterns)
            if (pattern != null && pattern.Phase == EnemyAttackPhase.Active && !(pattern is EnemyRangedAttack)) melee = true;
        if (_boss != null || _enemyType == TelemetryEnemyType.BeastBoss) melee = _activeMeleeTrace != null;
        bool suspicious = _controller != null && _controller.State == EnemyState.Searching;
        bool targeting = _controller != null && _controller.HasVisibleTarget;
        if (_boss != null) targeting = _boss.HasTelemetryTarget;
        if (_twins != null) targeting = _health.IsAlive && _twins.Pattern != TwinBossPattern.Idle && _twins.Pattern != TwinBossPattern.Finished;
        bool alive = _health != null && _health.IsAlive;
        object snapshot = new
        {
            elapsed_real_seconds = time, fixed_tick = tick, enemy_id = _enemyId, enemy_type = (int)_enemyType,
            position = new TelemetryPosition(_lastPosition), rotation = new TelemetryRotation(_lastRotation),
            is_alive = alive, melee_attacking = alive && melee,
            ranged_attempt = _rangedAttempt, ranged_success = _rangedSuccess,
            suspicious = alive && suspicious, targeting = alive && targeting,
            alert_position = new TelemetryPosition(_controller != null ? _controller.LastKnownPosition : _lastPosition)
        };
        _rangedAttempt = _rangedSuccess = false;
        return snapshot;
    }

    /// <summary>씬 수집 연결과 래치를 해제하며 에디터에 저장된 배치 ID는 유지한다.</summary>
    public void Unbind()
    {
        CapturePosition();
        _manager = null;
        _activeMeleeTrace = null;
        _rangedAttempt = _rangedSuccess = false;
    }

    void OnEnable() { if (_health != null) LifeStateChanged?.Invoke(this, _health.IsAlive); }
    void OnDisable() { CapturePosition(); LifeStateChanged?.Invoke(this, false); }
    void OnDestroy() { CapturePosition(); }
}
