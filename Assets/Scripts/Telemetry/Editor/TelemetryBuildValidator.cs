using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public sealed class TelemetryBuildValidator : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    /// <summary>빌드 대상 scene의 로그 설정과 배치 ID를 검증하고 잘못된 에셋은 report 빌드를 중단한다.</summary>
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return;
        string error = ValidateScene(scene);
        if (error != null) throw new BuildFailedException(error);
    }

    /// <summary>scene의 수집기·적 배치 ID·타입을 읽기 전용으로 검증하고 오류 설명 또는 null을 반환한다.</summary>
    public static string ValidateScene(Scene scene)
    {
        bool gameplay = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (GameSceneInitializer initializer in root.GetComponentsInChildren<GameSceneInitializer>(true))
            {
                gameplay = true;
                TelemetryStageRecorder recorder = initializer.GetComponent<TelemetryStageRecorder>();
                if (recorder == null || !Guid.TryParseExact(recorder.StageId, "N", out _) || string.IsNullOrWhiteSpace(recorder.MapVersion))
                    return scene.path + ": 로그 스테이지 설정이 누락되었습니다.";
            }
        }
        if (!gameplay) return null;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (EnemyHealth enemy in root.GetComponentsInChildren<EnemyHealth>(true))
            {
                EnemyTelemetrySource source = enemy.GetComponent<EnemyTelemetrySource>();
                if (source == null || enemy.GetComponents<EnemyTelemetrySource>().Length != 1
                    || !Guid.TryParseExact(source.EnemyId, "N", out _) || !ids.Add(source.EnemyId)
                    || !Enum.IsDefined(typeof(TelemetryEnemyType), source.EnemyType))
                    return scene.path + ": 적 로그 ID 또는 타입 누락·중복: " + enemy.name;
            }
        }
        return null;
    }
}
