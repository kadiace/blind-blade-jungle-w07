using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using UnityEngine;

public sealed class TelemetryManager : MonoBehaviour
{
    private const int SCHEMA_VERSION = 1;

    [Header("기록 설정")]
    [SerializeField] private TelemetrySettings _settings;
    private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
    private readonly Dictionary<string, int> _attemptNumbers = new Dictionary<string, int>();
    private TelemetryFileWriter _writer;
    private TelemetryStageRecorder _recorder;
    private TelemetryRecordingState _state = TelemetryRecordingState.Disabled;
    private string _directory;
    private string _stageId;
    private string _sceneName;
    private string _mapVersion;
    private string _error;
    private int _attemptNumber;
    private int _generation;
    private double _attemptStart;
    private double _stageStart;
    private long _attackInputs;
    private long _defenseInputs;
    private long _scanInputs;
    private long _playerHits;
    private long _blocks;
    private long _meleeAttempts;
    private long _meleeSuccesses;
    private long _rangedAttempts;
    private long _rangedSuccesses;
    private long _snapshots;
    private long _droppedSnapshots;
    private long _droppedEvents;
    private double _firstDrop = -1d;
    private double _lastDrop = -1d;
    private bool _quitRequested;
    private bool _allowQuit;
    private bool _reportedFailure;
    private double _quitStartedAt;
    private bool _shutdownTimedOut;
    private bool _ownsBackgroundSetting;
    private bool _previousRunInBackground;

    public TelemetryRecordingState State => _state;
    public string Error => _error;
    public string OutputDirectory { get; private set; }
    public double ElapsedRealSeconds => _clock.Elapsed.TotalSeconds;
    public int AttemptGeneration => _generation;
    public bool IsRecording => _directory != null && (_state == TelemetryRecordingState.Recording || _state == TelemetryRecordingState.Degraded);

    /// <summary>recorder를 첫 조작 가능 시점 확인 대상으로 보관한다.</summary>
    public void PrepareRecorder(TelemetryStageRecorder recorder) { _recorder = recorder; }

    /// <summary>준비된 수집기가 조작 가능하면 첫 공격·입력을 기록하기 전에 구간을 시작한다.</summary>
    public void EnsureAttemptStarted() { if (_recorder != null) _recorder.EnsureStarted(); }

    void Awake()
    {
        if (_settings == null) { Fail("플레이 로그 설정 에셋이 연결되지 않았습니다."); return; }
        if (!_settings.RecordingEnabled) return;
        if (string.IsNullOrWhiteSpace(_settings.ParticipantId) || string.IsNullOrWhiteSpace(_settings.TestVersion)
            || _settings.ParticipantId.Length > 128 || _settings.TestVersion.Length > 128)
        {
            Fail("참가자 ID와 테스트 버전은 1~128자로 설정해야 합니다.");
            return;
        }
        _clock.Start();
        string sessionId = Guid.NewGuid().ToString("N");
        OutputDirectory = Path.Combine(Application.persistentDataPath, "Telemetry", sessionId);
        _writer = new TelemetryFileWriter(OutputDirectory, _settings.QueueCapacity, _settings.ReservedEventCapacity,
            _settings.FileSizeBytes, _settings.FlushIntervalMilliseconds);
        _state = TelemetryRecordingState.Recording;
        Enqueue(new TelemetryWriteRequest("", "manifest", new
        {
            session_id = sessionId, participant_id = _settings.ParticipantId, test_version = _settings.TestVersion,
            schema_version = SCHEMA_VERSION, started_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            unity_version = Application.unityVersion, application_version = Application.version,
            fixed_delta_time = Time.fixedDeltaTime, time_basis = "session_elapsed_real_seconds",
            coordinate_system = "Unity world XYZ, metres, quaternion XYZW", pause_time_included = true
        }, true), true);
        Application.wantsToQuit += HandleWantsToQuit;
    }

    void Update()
    {
        if (_writer != null && _writer.Error != null) Fail(_writer.Error);
        if (!_quitRequested || _writer == null) return;
        if (!_writer.Completed)
        {
            if ((ElapsedRealSeconds - _quitStartedAt) * 1000d < _settings.ShutdownTimeoutMilliseconds) return;
            _shutdownTimedOut = true;
            Fail("정상 종료 저장 대기 시간을 초과했습니다.");
        }
        if (_state != TelemetryRecordingState.Failed) _state = TelemetryRecordingState.Closed;
        _allowQuit = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>recorder와 스테이지 정보를 연결하고 새 재도전 구간을 시작해 시작 시각과 집계를 초기화한다.</summary>
    public void BeginAttempt(TelemetryStageRecorder recorder, string stageId, string sceneName, string mapVersion, object placements)
    {
        if (_writer == null || _state == TelemetryRecordingState.Failed || _quitRequested || _directory != null) return;
        if (_stageId != stageId) _stageStart = ElapsedRealSeconds;
        _stageId = stageId;
        _sceneName = sceneName;
        _mapVersion = mapVersion;
        _attemptNumbers.TryGetValue(stageId, out int attempt);
        _attemptNumber = attempt + 1;
        _attemptNumbers[stageId] = _attemptNumber;
        _directory = Path.Combine(stageId, "attempt_" + _attemptNumber.ToString("D3", CultureInfo.InvariantCulture));
        _recorder = recorder;
        _generation++;
        _attemptStart = ElapsedRealSeconds;
        _attackInputs = _defenseInputs = _scanInputs = _playerHits = _blocks = 0;
        _meleeAttempts = _meleeSuccesses = _rangedAttempts = _rangedSuccesses = _snapshots = 0;
        _droppedSnapshots = _droppedEvents = 0;
        _firstDrop = _lastDrop = -1d;
        Enqueue(new TelemetryWriteRequest(_directory, "placements", placements, true), true);
        Enqueue(new TelemetryWriteRequest(_directory, "attempt", new
        {
            stage_id = stageId, stage_name = sceneName, map_version = mapVersion, attempt_number = _attemptNumber,
            started_at = _attemptStart, fixed_delta_time = Time.fixedDeltaTime
        }, true), true);
    }

    /// <summary>reason으로 현재 시도를 종료하고 closeStage이면 스테이지 종료 시각도 확정해 요약 저장을 요청한다.</summary>
    public void EndAttempt(string reason, bool closeStage)
    {
        if (_directory == null)
        {
            if (closeStage) CloseStage(reason);
            return;
        }
        double endedAt = ElapsedRealSeconds;
        object summary = new
        {
            stage_id = _stageId, stage_name = _sceneName, map_version = _mapVersion, attempt_number = _attemptNumber,
            start_time = _attemptStart, end_time = endedAt, duration_seconds = endedAt - _attemptStart,
            stage_start_time = _stageStart, stage_duration_seconds = endedAt - _stageStart, stage_ended = closeStage,
            end_reason = reason, attack_inputs = _attackInputs, defense_inputs = _defenseInputs, scan_inputs = _scanInputs,
            player_attack_successes = _playerHits, defense_successes = _blocks,
            enemy_melee_attempts = _meleeAttempts, enemy_melee_successes = _meleeSuccesses,
            enemy_ranged_attempts = _rangedAttempts, enemy_ranged_successes = _rangedSuccesses,
            collected_snapshots = _snapshots, dropped_snapshots = _droppedSnapshots, dropped_events = _droppedEvents,
            first_drop_time = _firstDrop, last_drop_time = _lastDrop, recording_state = _state.ToString(), error = _error
        };
        Enqueue(new TelemetryWriteRequest(_directory, "summary", summary, true, true,
            dataComplete: _state == TelemetryRecordingState.Recording && _droppedSnapshots == 0 && _droppedEvents == 0), true);
        _directory = null;
        _recorder = null;
        if (closeStage) CloseStage(reason);
    }

    /// <summary>reason으로 스테이지 종료 시각을 기록하며 재도전 로딩 중 종료도 실제 체류 시간에 포함한다.</summary>
    private void CloseStage(string reason)
    {
        if (_stageId == null || _writer == null) return;
        double endedAt = ElapsedRealSeconds;
        Enqueue(new TelemetryWriteRequest(_stageId, "stage_events", new
        {
            event_kind = "StageEnded", stage_id = _stageId, stage_name = _sceneName,
            map_version = _mapVersion, start_time = _stageStart, end_time = endedAt,
            duration_seconds = endedAt - _stageStart, end_reason = reason, last_attempt_number = _attemptNumber
        }, isControl: true), true);
        _stageId = null;
    }

    /// <summary>씬 전환 직전 현재 구간을 한 번 닫으며 기록 매니저가 없는 메뉴에서는 동작하지 않는다.</summary>
    public static void NotifySceneExit()
    {
        if (Game.IsInitialized && Game.Instance.TelemetryManager != null)
            Game.Instance.TelemetryManager.EndAttempt("SceneTransition", true);
    }

    /// <summary>kind의 실제 입력 누름과 position을 기록하고 해당 입력의 전체 횟수를 증가시킨다.</summary>
    public void RecordInput(TelemetryInputKind kind, Vector3 position)
    {
        if (!IsRecording) return;
        switch (kind)
        {
            case TelemetryInputKind.Attack: _attackInputs++; break;
            case TelemetryInputKind.Defense: _defenseInputs++; break;
            case TelemetryInputKind.Scan: _scanInputs++; break;
        }
        RecordPlayerEvent(kind.ToString() + "Input", position);
    }

    /// <summary>defense와 position으로 플레이어 공격 적중 또는 차단 성공을 한 번 기록하고 집계를 갱신한다.</summary>
    public void RecordPlayerSuccess(bool defense, Vector3 position)
    {
        if (!IsRecording) return;
        if (defense) _blocks++; else _playerHits++;
        RecordPlayerEvent(defense ? "DefenseSuccess" : "AttackSuccess", position);
    }

    /// <summary>trace의 적 배치·공격 종류와 success로 공격 이벤트를 기록하며 세 필드 형식을 유지한다.</summary>
    public void RecordEnemyAttack(EnemyAttackTrace trace, bool success)
    {
        if (!IsRecording || trace.AttemptGeneration != _generation) return;
        if (trace.Kind == TelemetryAttackKind.Melee)
        {
            if (success) _meleeSuccesses++; else _meleeAttempts++;
        }
        else
        {
            if (success) _rangedSuccesses++; else _rangedAttempts++;
        }
        string stream = "enemy_" + (trace.Kind == TelemetryAttackKind.Melee ? "melee" : "ranged")
            + (success ? "_successes" : "_attempts");
        Enqueue(new TelemetryWriteRequest(_directory, stream, new
        {
            enemy_id = trace.EnemyId, elapsed_real_seconds = ElapsedRealSeconds, position = trace.CapturePosition()
        }), true);
    }

    /// <summary>stream과 순수 snapshot을 일반 큐에 넣고 수집 및 누락 건수를 갱신한다.</summary>
    public void RecordSnapshot(string stream, object snapshot)
    {
        if (!IsRecording) return;
        _snapshots++;
        Enqueue(new TelemetryWriteRequest(_directory, stream, snapshot), false);
    }

    /// <summary>message로 기록 실패를 확정하고 큐 밖 상태와 한 번의 Console 오류 알림을 유지한다.</summary>
    public void Fail(string message)
    {
        _state = TelemetryRecordingState.Failed;
        _error = message;
        if (_reportedFailure) return;
        _reportedFailure = true;
        Debug.LogError("플레이 로그 저장 실패: " + message, this);
    }

    /// <summary>현재 구간의 종료 시각을 확정하고 저장 완료 뒤 정상 앱 종료를 요청한다.</summary>
    public void RequestQuit()
    {
        if (_quitRequested) return;
        EndAttempt("ApplicationQuit", true);
        _quitRequested = true;
        _quitStartedAt = ElapsedRealSeconds;
        if (_writer == null)
        {
            _allowQuit = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }
        if (_state != TelemetryRecordingState.Failed) _state = TelemetryRecordingState.Closing;
        _previousRunInBackground = Application.runInBackground;
        _ownsBackgroundSetting = true;
        Application.runInBackground = true;
        _writer.Stop();
    }

    /// <summary>Editor 또는 최종 종료에서 현재 구간을 닫고 제한 시간 내 저장 작업자 정리 결과를 반환한다.</summary>
    public bool CompleteSynchronously()
    {
        EndAttempt("ApplicationQuit", true);
        if (_writer == null) return true;
        if (_shutdownTimedOut) return false;
        _writer.Stop();
        if (!_writer.WaitForCompletion(_settings.ShutdownTimeoutMilliseconds))
        {
            Fail("정상 종료 저장 대기 시간을 초과했습니다.");
            _shutdownTimedOut = true;
            return false;
        }
        if (_writer.Error != null) { Fail(_writer.Error); return false; }
        if (_state != TelemetryRecordingState.Failed) _state = TelemetryRecordingState.Closed;
        return _state != TelemetryRecordingState.Failed;
    }

    /// <summary>kind와 position을 현재 구간의 플레이어 이벤트 파일에 저장하도록 요청한다.</summary>
    private void RecordPlayerEvent(string kind, Vector3 position)
    {
        Enqueue(new TelemetryWriteRequest(_directory, "player_events", new
        {
            event_kind = kind, elapsed_real_seconds = ElapsedRealSeconds, position = new TelemetryPosition(position)
        }), true);
    }

    /// <summary>request를 critical 정책으로 전달하고 거절된 기록의 종류·시간 및 실패 상태를 갱신한다.</summary>
    private void Enqueue(TelemetryWriteRequest request, bool critical)
    {
        if (_writer.TryEnqueue(request, critical)) return;
        double now = ElapsedRealSeconds;
        if (_firstDrop < 0d) _firstDrop = now;
        _lastDrop = now;
        if (critical)
        {
            _droppedEvents++;
            Fail(_writer.Error ?? "중요 이벤트 예약 큐가 가득 찼습니다.");
        }
        else
        {
            _droppedSnapshots++;
            if (_state != TelemetryRecordingState.Failed) _state = TelemetryRecordingState.Degraded;
        }
    }

    /// <summary>최초 정상 종료 요청을 저장 절차로 전환하고 저장이 끝난 뒤에만 종료를 허용한다.</summary>
    private bool HandleWantsToQuit()
    {
        if (_allowQuit) return true;
        RequestQuit();
        return false;
    }

    void OnApplicationQuit() { CompleteSynchronously(); }

    void OnDestroy()
    {
        Application.wantsToQuit -= HandleWantsToQuit;
        CompleteSynchronously();
        if (_ownsBackgroundSetting) Application.runInBackground = _previousRunInBackground;
    }
}
