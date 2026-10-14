using UnityEngine;


/// <summary>같은 스테이지의 검·발걸음·스캔·환경 소음을 적 청각에 전달한다.</summary>
public sealed class StageSwordNoise : MonoBehaviour
{
    private EnemyController[] _enemies;
    private PlayerController _player;
    private GameSceneInitializer _settings;
    private SensoryManager _sensory;
    private bool _useSwordNoise;

    private float _hearingDistance;
    private float _alertDuration;
    private int _lastActionId = -1;

    /// <summary>player와 현재 씬 enemies를 연결하고 hearingDistance 및 alertDuration으로 검 소음 전달을 준비한다.</summary>
    public void Initialize(PlayerController player, EnemyController[] enemies, float hearingDistance, float alertDuration, bool useSwordNoise = true)
    {
        Shutdown();
        _player = player;
        _settings = GetComponent<GameSceneInitializer>();
        _enemies = enemies;
        _hearingDistance = hearingDistance;
        _alertDuration = alertDuration;
        _useSwordNoise = useSwordNoise;
        _lastActionId = -1;
        _player.ActionEvent += HandleAction;
        _sensory = Game.Instance.SensoryManager;
        _sensory.SoundEmitted += HandleSound;
    }

    void OnDisable()
    {
        Shutdown();
    }

    /// <summary>현재 플레이어의 검 행동 구독을 해제하고 씬 참조를 정리한다.</summary>
    public void Shutdown()
    {
        if (_player != null)
            _player.ActionEvent -= HandleAction;
        if (_sensory != null)
            _sensory.SoundEmitted -= HandleSound;
        _sensory = null;
        _player = null;
        _enemies = System.Array.Empty<EnemyController>();
    }

    /// <summary>일반 베기 시작 또는 발도 베기 action의 소음을 행동당 한 번 복도에 전달한다.</summary>
    private void HandleAction(PlayerActionEvent action)
    {
        bool swing = (action.Stage == PlayerActionStage.AttackStarted && action.State == PlayerActionState.NormalAttack)
            || action.Stage == PlayerActionStage.SlashStarted;
        if (!swing || action.ActionId == _lastActionId
            || !(_settings != null ? _settings.SwordNoiseEnabled : _useSwordNoise))
            return;
        _lastActionId = action.ActionId;
        _sensory.ReportSound(_player.transform.position, 1f, _player.gameObject);
    }

    /// <summary>position·volume·source의 소음을 같은 씬 적에게 전달하며 발생한 적 자신은 제외한다.</summary>
    private void HandleSound(Vector3 position, float volume, GameObject source)
    {
        if (source != null && source.scene != gameObject.scene) return;
        AlertEnemies(position, volume, source);
    }

    /// <summary>position과 volume으로 청각 범위 안의 적에게 회전할 소리 방향을 전달한다. 시각 추적 상태와 NavMesh 목적지는 변경하지 않는다.</summary>
    public void AlertEnemies(Vector3 position, float volume = 1f, GameObject source = null)
    {
        if (_settings != null)
        {
            _hearingDistance = _settings.SwordHearingDistance;
            _alertDuration = _settings.SwordAlertDuration;
        }
        foreach (EnemyController enemy in _enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.gameObject.scene != gameObject.scene)
                continue;
            if (source != null && source.transform.IsChildOf(enemy.transform)) continue;
            float hearingDistance = Mathf.Min(_hearingDistance, enemy.GetComponent<EnemySensor>().HearingDistance) * volume;
            if (hearingDistance > 0f && Vector3.Distance(enemy.transform.position, position) <= hearingDistance)
                enemy.HearNoise(position, _alertDuration);
        }
    }
}
