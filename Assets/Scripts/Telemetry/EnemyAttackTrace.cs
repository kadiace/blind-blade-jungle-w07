public sealed class EnemyAttackTrace : System.IDisposable
{
    private readonly EnemyTelemetrySource _source;
    private readonly TelemetryManager _manager;
    private bool _succeeded;

    public string EnemyId { get; }
    public TelemetryAttackKind Kind { get; }
    public int AttemptGeneration { get; }

    /// <summary>source·kind·manager로 공격 문맥을 생성하고 배치 ID와 시작 구간을 고정한다.</summary>
    public EnemyAttackTrace(EnemyTelemetrySource source, TelemetryAttackKind kind, TelemetryManager manager)
    {
        _source = source;
        _manager = manager;
        EnemyId = source.EnemyId;
        Kind = kind;
        AttemptGeneration = manager.AttemptGeneration;
    }

    /// <summary>실제 플레이어 피해가 적용된 현재 구간의 공격만 한 번 성공 처리하고 이벤트를 기록한다.</summary>
    public void ConfirmSuccess()
    {
        if (_succeeded || !_manager.IsRecording || _manager.AttemptGeneration != AttemptGeneration) return;
        _succeeded = true;
        _source.MarkAttackSuccess(Kind);
        _manager.RecordEnemyAttack(this, true);
    }

    /// <summary>공격 주체의 현재 또는 마지막 확인 위치를 Unity 객체 참조 없는 저장 값으로 반환한다.</summary>
    public TelemetryPosition CapturePosition()
    {
        return new TelemetryPosition(_source.CapturePosition());
    }

    /// <summary>현재 근거리 실행 구간을 닫으며 나중에 도착하는 투사체 성공 문맥은 유지한다.</summary>
    public void Dispose() { _source.EndAttack(this); }
}
