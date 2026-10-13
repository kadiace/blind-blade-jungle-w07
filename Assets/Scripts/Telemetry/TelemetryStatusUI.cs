using UnityEngine;

public sealed class TelemetryStatusUI : MonoBehaviour
{
    private TelemetryManager _manager;

    void Awake() { _manager = GetComponent<TelemetryManager>(); }

    void OnGUI()
    {
        if (_manager.State == TelemetryRecordingState.Disabled || _manager.State == TelemetryRecordingState.Closed) return;
        string label = _manager.State == TelemetryRecordingState.Failed ? "플레이 기록 실패"
            : _manager.State == TelemetryRecordingState.Degraded ? "플레이 기록 일부 누락"
            : _manager.State == TelemetryRecordingState.Closing ? "플레이 기록 저장 중" : "플레이 기록 중";
        GUI.Box(new Rect(12f, 12f, 180f, 30f), label);
    }
}
