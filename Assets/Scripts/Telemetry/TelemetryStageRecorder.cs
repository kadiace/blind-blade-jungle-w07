using System;
using System.Collections.Generic;

using UnityEngine;

using Unity.Profiling;

[DisallowMultipleComponent]
public sealed class TelemetryStageRecorder : MonoBehaviour
{
    private static readonly ProfilerMarker _snapshotMarker = new ProfilerMarker("Telemetry.FixedSnapshot");

    [Header("스테이지 정보")]
    [SerializeField] private string _stageId;
    [SerializeField] private string _mapVersion = "1";
    private TelemetryManager _manager;
    private InputManager _input;
    private PlayerController _player;
    private PlayerHealth _health;
    private PlayerMotor _motor;
    private KatanaCombatCoordinator _combat;
    private readonly List<EnemyTelemetrySource> _enemies = new List<EnemyTelemetrySource>();
    private readonly HashSet<EnemyHealth> _alive = new HashSet<EnemyHealth>();
    private readonly List<SceneTransitionFade> _fades = new List<SceneTransitionFade>();
    private object _placements;
    private long _tick;
    private int _lastHitActionId = -1;
    private bool _started;
    private bool _prepared;

    public string StageId => _stageId;
    public string MapVersion => _mapVersion;

    /// <summary>manager·input·player로 씬 수집 대상을 준비하고 배치 ID를 검증한 뒤 기존 이벤트를 구독한다.</summary>
    public void Prepare(TelemetryManager manager, InputManager input, PlayerController player)
    {
        if (_prepared || manager.State == TelemetryRecordingState.Disabled || manager.State == TelemetryRecordingState.Failed) return;
        if (!Guid.TryParseExact(_stageId, "N", out _) || string.IsNullOrWhiteSpace(_mapVersion) || _mapVersion.Length > 128)
        {
            manager.Fail("스테이지 ID 또는 맵 버전이 없습니다. Tools/Telemetry/Prepare Gameplay Assets를 실행하세요.");
            return;
        }
        _manager = manager;
        _input = input;
        _player = player;
        _health = player.GetComponent<PlayerHealth>();
        _motor = player.GetComponent<PlayerMotor>();
        _combat = player.GetComponent<KatanaCombatCoordinator>();
        var roots = gameObject.scene.GetRootGameObjects();
        TwinBossController twins = null;
        foreach (GameObject root in roots)
        {
            _fades.AddRange(root.GetComponentsInChildren<SceneTransitionFade>(true));
            TwinBossController candidate = root.GetComponentInChildren<TwinBossController>(true);
            if (candidate != null) twins = candidate;
        }
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var placements = new List<object>();
        foreach (GameObject root in roots)
        {
            foreach (EnemyHealth enemy in root.GetComponentsInChildren<EnemyHealth>(true))
            {
                if (!enemy.TryGetComponent(out EnemyTelemetrySource source)
                    || !Guid.TryParseExact(source.EnemyId, "N", out _) || !ids.Add(source.EnemyId))
                {
                    manager.Fail("배치 적의 로그 컴포넌트 또는 고유 ID가 누락·중복되었습니다: " + enemy.name);
                    Shutdown();
                    return;
                }
                source.Bind(manager, twins);
                _enemies.Add(source);
                source.LifeStateChanged += HandleEnemyLifeState;
                enemy.Died += HandleEnemyDied;
                if (source.CountsForStageClear && enemy.IsAlive) _alive.Add(enemy);
                placements.Add(new { enemy_id = source.EnemyId, enemy_type = (int)source.EnemyType,
                    initial_position = new TelemetryPosition(source.CapturePosition()), counts_for_stage_clear = source.CountsForStageClear });
            }
        }
        _placements = placements.ToArray();
        _input.GameplayInputPressed += HandleInput;
        _player.ActionEvent += HandlePlayerAction;
        _health.DamageReceived += HandleDamage;
        if (_combat != null) _combat.DefenseSucceeded += HandleDefense;
        else _health.DamageBlocked += HandleLegacyDefense;
        _prepared = true;
        _manager.PrepareRecorder(this);
    }

    /// <summary>최초 조작 가능 상태에서 현재 시도를 한 번 시작하며 페이드·일시정지 중에는 대기한다.</summary>
    public void EnsureStarted()
    {
        if (!_prepared || _started || !_input.CanAcceptGameplayInput || _player.IsPaused) return;
        foreach (SceneTransitionFade fade in _fades) if (fade != null && fade.IsFading) return;
        _manager.BeginAttempt(this, _stageId, gameObject.scene.name, _mapVersion, _placements);
        _started = _manager.IsRecording;
    }

    void LateUpdate() { EnsureStarted(); }

    void FixedUpdate()
    {
        if (!_started || !_manager.IsRecording) return;
        using (_snapshotMarker.Auto()) CaptureSnapshots();
    }

    /// <summary>캐시된 플레이어·적 상태를 현재 실제 시간과 틱으로 복사하고 스냅샷 큐에 전달한다.</summary>
    private void CaptureSnapshots()
    {
        _tick++;
        double time = _manager.ElapsedRealSeconds;
        _manager.RecordSnapshot("player_snapshots", new
        {
            elapsed_real_seconds = time, fixed_tick = _tick, fixed_delta_time = Time.fixedDeltaTime,
            attack_input = _input.AttackInputHeld, defense_input = _input.DefenseInputHeld, scan_input = _input.ScanInputActive,
            dashing = _motor.IsDashing || _motor.IsDodging, hp = _health.CurrentHealth,
            position = new TelemetryPosition(_player.transform.position), rotation = new TelemetryRotation(_player.transform.rotation),
            view_direction = new TelemetryPosition(_motor.ViewForward), stage_name = gameObject.scene.name, remaining_enemies = _alive.Count
        });
        foreach (EnemyTelemetrySource enemy in _enemies)
            _manager.RecordSnapshot("enemy_snapshots", enemy.CaptureSnapshot(time, _tick));
    }

    /// <summary>kind의 실제 새 누름을 조작 시작 시점부터 입력 이벤트와 전체 횟수로 기록한다.</summary>
    private void HandleInput(TelemetryInputKind kind)
    {
        EnsureStarted();
        _manager.RecordInput(kind, _player.transform.position);
    }

    /// <summary>action의 확정 적중을 행동 ID별 한 번으로 제한해 플레이어 공격 성공으로 기록한다.</summary>
    private void HandlePlayerAction(PlayerActionEvent action)
    {
        if (action.Stage != PlayerActionStage.HitConfirmed || action.ActionId == _lastHitActionId) return;
        _lastHitActionId = action.ActionId;
        _manager.RecordPlayerSuccess(false, _player.transform.position);
    }

    /// <summary>실제 HP 차감 뒤 받은 context의 적 공격 문맥을 성공 처리한다.</summary>
    private void HandleDamage(DamageContext context) { context.AttackTrace?.ConfirmSuccess(); }

    /// <summary>result의 확정 차단을 플레이어 방어 성공 이벤트로 한 번 기록한다.</summary>
    private void HandleDefense(KatanaDefenseResult result) { _manager.RecordPlayerSuccess(true, _player.transform.position); }

    /// <summary>카타나가 없는 수신자의 context 차단을 방어 성공으로 기록한다.</summary>
    private void HandleLegacyDefense(DamageContext context) { _manager.RecordPlayerSuccess(true, _player.transform.position); }

    /// <summary>enemy의 실제 사망을 생존 집계에서 제거해 남은 적 수를 갱신한다.</summary>
    private void HandleEnemyDied(EnemyHealth enemy) { _alive.Remove(enemy); }

    /// <summary>source의 활성 생존 상태 alive로 클리어 집계 대상 등록과 제거를 갱신한다.</summary>
    private void HandleEnemyLifeState(EnemyTelemetrySource source, bool alive)
    {
        if (alive && source.CountsForStageClear) _alive.Add(source.Health);
        else _alive.Remove(source.Health);
    }

    /// <summary>현재 씬의 플레이어·적 이벤트 구독과 참조를 해제하며 영속 저장 작업자는 유지한다.</summary>
    public void Shutdown()
    {
        if (_input != null) _input.GameplayInputPressed -= HandleInput;
        if (_player != null) _player.ActionEvent -= HandlePlayerAction;
        if (_health != null)
        {
            _health.DamageReceived -= HandleDamage;
            _health.DamageBlocked -= HandleLegacyDefense;
        }
        if (_combat != null) _combat.DefenseSucceeded -= HandleDefense;
        foreach (EnemyTelemetrySource enemy in _enemies)
        {
            if (enemy.Health != null) enemy.Health.Died -= HandleEnemyDied;
            enemy.LifeStateChanged -= HandleEnemyLifeState;
            enemy.Unbind();
        }
        _enemies.Clear();
        _alive.Clear();
        _fades.Clear();
        _prepared = _started = false;
        _lastHitActionId = -1;
    }

    void OnDisable() { Shutdown(); }
}
