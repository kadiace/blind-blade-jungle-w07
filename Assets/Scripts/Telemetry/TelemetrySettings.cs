using UnityEngine;

[CreateAssetMenu(menuName = "Telemetry/Settings")]
public sealed class TelemetrySettings : ScriptableObject
{
    [Header("테스트 정보")]
    [SerializeField] private bool _recordingEnabled = true;
    [SerializeField] private string _participantId = "tester";
    [SerializeField] private string _testVersion = "1";

    [Header("저장 제한")]
    [SerializeField, Min(64)] private int _queueCapacity = 4096;
    [SerializeField, Min(16)] private int _reservedEventCapacity = 512;
    [SerializeField, Min(1)] private int _fileSizeMegabytes = 64;
    [SerializeField, Range(0.05f, 5f)] private float _flushIntervalSeconds = 0.5f;
    [SerializeField, Range(1f, 30f)] private float _shutdownTimeoutSeconds = 10f;

    public bool RecordingEnabled => _recordingEnabled;
    public string ParticipantId => _participantId;
    public string TestVersion => _testVersion;
    public int QueueCapacity => Mathf.Max(64, _queueCapacity);
    public int ReservedEventCapacity => Mathf.Clamp(_reservedEventCapacity, 16, QueueCapacity / 2);
    public long FileSizeBytes => Mathf.Max(1, _fileSizeMegabytes) * 1024L * 1024L;
    public int FlushIntervalMilliseconds => Mathf.RoundToInt(Mathf.Clamp(_flushIntervalSeconds, 0.05f, 5f) * 1000f);
    public int ShutdownTimeoutMilliseconds => Mathf.RoundToInt(Mathf.Clamp(_shutdownTimeoutSeconds, 1f, 30f) * 1000f);
}
