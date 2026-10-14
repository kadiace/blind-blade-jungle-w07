using System;

using UnityEngine;

using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>기존 방어 씬의 직렬화 값을 보존하면서 책임별 컴포넌트로 옮기는 명시적 에디터 도구다.</summary>
public static class BambooDefenseRefactoring
{
    private const string SCENE_PATH = "Assets/Scenes/Defense/BambooDefense.unity";

    /// <summary>저장된 방어 씬에 HUD 프리팹과 표시 참조를 연결하고 씬을 저장한다. 기존 인스턴스는 유지한다.</summary>
    [MenuItem("BlindBlade/대숲 방어/리팩토링/HUD 연결 이전")]
    public static void MigrateHud()
    {
        RequireCleanDefenseScene();
        BambooDefenseDirector director = UnityEngine.Object.FindFirstObjectByType<BambooDefenseDirector>();
        BambooDefenseHudAuthoring.Connect(director);
        EditorSceneManager.SaveScene(director.gameObject.scene);
        Debug.Log("방어 HUD 프리팹 연결 완료");
    }

    /// <summary>현재 씬의 Director 스폰 값을 같은 오브젝트의 Spawner로 복사하고 연결·저장하며 이전 필드 수를 반환한다.</summary>
    [MenuItem("BlindBlade/대숲 방어/리팩토링/스폰 값 이전")]
    public static void MigrateSpawner()
    {
        RequireCleanDefenseScene();
        BambooDefenseDirector director = UnityEngine.Object.FindFirstObjectByType<BambooDefenseDirector>();
        SerializedObject source = new SerializedObject(director);
        if (source.FindProperty("_spawner").objectReferenceValue != null)
            return;
        DefenseEnemySpawner spawner = Undo.AddComponent<DefenseEnemySpawner>(director.gameObject);
        SerializedObject target = new SerializedObject(spawner);
        SerializedProperty iterator = target.GetIterator();
        int copied = 0;
        while (iterator.NextVisible(true))
        {
            if (!iterator.propertyPath.StartsWith("_", StringComparison.Ordinal))
                continue;
            SerializedProperty original = source.FindProperty(iterator.propertyPath);
            if (original == null)
                throw new InvalidOperationException("이전 원본 필드가 없습니다: " + iterator.propertyPath);
            target.CopyFromSerializedProperty(original);
            copied++;
        }
        target.ApplyModifiedProperties();
        source.FindProperty("_spawner").objectReferenceValue = spawner;
        source.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        EditorSceneManager.SaveScene(director.gameObject.scene);
        Debug.Log("방어 스폰 필드 이전 완료: " + copied);
    }

    /// <summary>편집 모드의 저장된 방어 씬인지 검사한다. 다른 씬·미저장 편집·Play 상태면 예외로 변경을 중단한다.</summary>
    private static void RequireCleanDefenseScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || scene.path != SCENE_PATH || scene.isDirty)
            throw new InvalidOperationException("저장된 BambooDefense 씬의 편집 모드에서 실행하세요.");
    }
}
