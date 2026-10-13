using UnityEditor;

[InitializeOnLoad]
public static class TelemetryEditorLifecycle
{
    /// <summary>Editor의 Play 종료 알림을 구독하여 정상 기록 종료 처리를 연결한다.</summary>
    static TelemetryEditorLifecycle() { EditorApplication.playModeStateChanged += HandlePlayModeState; }

    /// <summary>state가 Play 종료이면 현재 기록 매니저의 큐와 파일을 제한 시간 내 정리한다.</summary>
    private static void HandlePlayModeState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode || !Game.IsInitialized) return;
        TelemetryManager telemetry = Game.Instance.TelemetryManager;
        if (telemetry != null) telemetry.CompleteSynchronously();
    }
}
