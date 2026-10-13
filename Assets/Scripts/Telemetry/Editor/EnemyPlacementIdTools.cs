using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class EnemyPlacementIdTools
{
    private static readonly Dictionary<string, EnemyTelemetrySource> _owners = new Dictionary<string, EnemyTelemetrySource>(StringComparer.OrdinalIgnoreCase);
    private static bool _repairQueued;
    private static bool _preparing;

    /// <summary>에디터 계층 변경을 구독해 새 배치 또는 복제 인스턴스의 ID 발급을 예약한다.</summary>
    static EnemyPlacementIdTools()
    {
        EditorApplication.hierarchyChanged += QueueRepair;
        QueueRepair();
    }

    /// <summary>열린 일반 씬의 배치 변경을 다음 에디터 주기에 검사하도록 한 번 예약한다.</summary>
    private static void QueueRepair()
    {
        if (_repairQueued || _preparing || EditorApplication.isPlayingOrWillChangePlaymode) return;
        _repairQueued = true;
        EditorApplication.delayCall += RepairOpenScenes;
    }

    /// <summary>열린 씬의 새 ID와 중복 ID를 처리하며 이미 확인한 원본 배치의 ID를 보존한다.</summary>
    private static void RepairOpenScenes()
    {
        _repairQueued = false;
        if (_preparing || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var staleKeys = new List<string>();
        foreach (KeyValuePair<string, EnemyTelemetrySource> pair in _owners)
            if (pair.Value == null) staleKeys.Add(pair.Key);
        foreach (string key in staleKeys) _owners.Remove(key);
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (!scene.isLoaded || string.IsNullOrEmpty(scene.path) || EditorSceneManager.IsPreviewScene(scene)) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (EnemyTelemetrySource source in root.GetComponentsInChildren<EnemyTelemetrySource>(true)) RepairId(source, scene.path);
        }
    }

    /// <summary>source의 ID를 scenePath 범위에서 검증하고 누락·복제인 경우 새 ID를 Undo 가능한 상태로 저장한다.</summary>
    private static void RepairId(EnemyTelemetrySource source, string scenePath)
    {
        string key = scenePath + ":" + source.EnemyId;
        bool duplicate = _owners.TryGetValue(key, out EnemyTelemetrySource owner) && owner != null && owner != source;
        if (!Guid.TryParseExact(source.EnemyId, "N", out _) || duplicate)
        {
            Undo.RecordObject(source, "Assign enemy placement ID");
            var serialized = new SerializedObject(source);
            serialized.FindProperty("_enemyId").stringValue = Guid.NewGuid().ToString("N");
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
            key = scenePath + ":" + source.EnemyId;
        }
        _owners[key] = source;
    }

    /// <summary>현재 게임 프리팹과 관련 씬에 수집 컴포넌트·설정·고유 배치 ID를 연결하고 변경 에셋 수를 반환한다.</summary>
    [MenuItem("Tools/Telemetry/Prepare Gameplay Assets")]
    public static int PrepareGameplayAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode에서는 로그 에셋을 변경할 수 없습니다.");
        for (int index = 0; index < SceneManager.sceneCount; index++)
            if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("열린 씬의 변경을 먼저 저장하세요. 로그 도구는 기존 미저장 변경을 저장하지 않습니다.");
        _preparing = true;
        int changed = 0;
        Scene originalScene = SceneManager.GetActiveScene();
        try
        {
            PrepareManagerAssets();
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" });
            var prefabs = new List<string>();
            foreach (string guid in prefabGuids) prefabs.Add(AssetDatabase.GUIDToAssetPath(guid));
            prefabs.Sort(ComparePrefabOrder);
            foreach (string path in prefabs)
            {
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool modified = false;
                    foreach (EnemyHealth enemy in prefab.GetComponentsInChildren<EnemyHealth>(true))
                    {
                        if (enemy.GetComponent<EnemyTelemetrySource>() != null) continue;
                        GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(enemy.gameObject);
                        if (instanceRoot != null && instanceRoot != prefab) continue;
                        EnemyTelemetrySource source = enemy.gameObject.AddComponent<EnemyTelemetrySource>();
                        SetEnemyType(source);
                        modified = true;
                    }
                    if (prefab.GetComponent<GameSceneInitializer>() != null && prefab.GetComponent<TelemetryStageRecorder>() == null)
                    {
                        prefab.AddComponent<TelemetryStageRecorder>();
                        modified = true;
                    }
                    if (modified) { PrefabUtility.SaveAsPrefabAsset(prefab, path); changed++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            string initializerGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/GameManagers/GameSceneInitializer.cs");
            string initializerPrefabGuid = AssetDatabase.AssetPathToGUID("Assets/Resources/Prefabs/Scene/GameSceneInitializer.prefab");
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string sceneText = File.ReadAllText(path);
                if (!sceneText.Contains(initializerGuid) && !sceneText.Contains(initializerPrefabGuid)) continue;
                bool invalidPrefabReference = false;
                foreach (Match match in Regex.Matches(sceneText, @"m_SourcePrefab: \{fileID: [^,]+, guid: ([a-f0-9]{32})"))
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value))) invalidPrefabReference = true;
                if (invalidPrefabReference)
                {
                    Debug.LogWarning("기존 누락 프리팹이 있는 씬은 로그 연결에서 제외했습니다: " + path);
                    continue;
                }
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    bool missingPrefab = false;
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                            if (PrefabUtility.IsPrefabAssetMissing(child.gameObject)) missingPrefab = true;
                    if (missingPrefab)
                    {
                        Debug.LogWarning("기존 누락 프리팹이 있는 씬은 로그 연결에서 제외했습니다: " + path);
                        continue;
                    }
                    if (PrepareScene(scene, guid)) { EditorSceneManager.SaveScene(scene, path); changed++; }
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            AssetDatabase.SaveAssets();
            return changed;
        }
        finally
        {
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            _preparing = false;
            QueueRepair();
        }
    }

    /// <summary>left와 right의 프리팹 종류를 비교하여 기본 프리팹을 Variant보다 먼저 처리한다.</summary>
    private static int ComparePrefabOrder(string left, string right)
    {
        bool leftVariant = PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(left)) == PrefabAssetType.Variant;
        bool rightVariant = PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(right)) == PrefabAssetType.Variant;
        return leftVariant.CompareTo(rightVariant);
    }

    /// <summary>scene과 stageId를 사용해 수집 설정과 적 ID를 작성하고 실제 수정 여부를 반환한다.</summary>
    private static bool PrepareScene(Scene scene, string stageId)
    {
        bool modified = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (GameSceneInitializer initializer in root.GetComponentsInChildren<GameSceneInitializer>(true))
            {
                TelemetryStageRecorder recorder = initializer.GetComponent<TelemetryStageRecorder>();
                if (recorder == null) { recorder = initializer.gameObject.AddComponent<TelemetryStageRecorder>(); modified = true; }
                if (recorder.StageId == stageId) continue;
                var serialized = new SerializedObject(recorder);
                serialized.FindProperty("_stageId").stringValue = stageId;
                if (string.IsNullOrWhiteSpace(recorder.MapVersion)) serialized.FindProperty("_mapVersion").stringValue = "1";
                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(recorder);
                modified = true;
            }
            foreach (EnemyHealth enemy in root.GetComponentsInChildren<EnemyHealth>(true))
            {
                modified |= MigrateAddedSource(enemy);
                EnemyTelemetrySource source = enemy.GetComponent<EnemyTelemetrySource>();
                if (source == null) { source = enemy.gameObject.AddComponent<EnemyTelemetrySource>(); SetEnemyType(source); modified = true; }
                string before = source.EnemyId;
                RepairId(source, scene.path);
                if (source.EnemyId != before) modified = true;
            }
        }
        return modified;
    }

    /// <summary>enemy에 새 기본 컴포넌트와 기존 씬 추가 컴포넌트가 함께 있으면 배치 설정을 기본 컴포넌트로 보존해 옮긴다.</summary>
    private static bool MigrateAddedSource(EnemyHealth enemy)
    {
        EnemyTelemetrySource[] sources = enemy.GetComponents<EnemyTelemetrySource>();
        if (sources.Length < 2) return false;
        EnemyTelemetrySource inherited = null;
        EnemyTelemetrySource added = null;
        foreach (EnemyTelemetrySource source in sources)
        {
            if (PrefabUtility.IsAddedComponentOverride(source)) added = source;
            else inherited = source;
        }
        if (inherited == null || added == null || !string.IsNullOrEmpty(inherited.EnemyId))
            throw new InvalidOperationException("적 로그 컴포넌트가 중복되었습니다: " + enemy.name);
        var serialized = new SerializedObject(inherited);
        serialized.FindProperty("_enemyId").stringValue = added.EnemyId;
        serialized.FindProperty("_enemyType").intValue = (int)added.EnemyType;
        serialized.FindProperty("_countsForStageClear").boolValue = added.CountsForStageClear;
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(inherited);
        PrefabUtility.RevertAddedComponent(added, InteractionMode.AutomatedAction);
        return true;
    }

    /// <summary>source의 기존 적 구현을 검사해 로그용 적 종류를 Inspector 설정에 저장한다.</summary>
    private static void SetEnemyType(EnemyTelemetrySource source)
    {
        TelemetryEnemyType type = source.GetComponent<HumanBossAttack>() != null ? TelemetryEnemyType.HumanBoss
            : source.GetComponent<BeastBossAttack>() != null ? TelemetryEnemyType.BeastBoss
            : source.GetComponent<BossController>() != null ? TelemetryEnemyType.Boss
            : source.GetComponent<EnemyRhythmChargeAttack>() != null ? TelemetryEnemyType.RhythmCharge
            : source.GetComponent<EnemyChargeAttack>() != null ? TelemetryEnemyType.Charge
            : source.GetComponent<EnemyRangedAttack>() != null ? TelemetryEnemyType.Ranged : TelemetryEnemyType.Melee;
        var serialized = new SerializedObject(source);
        serialized.FindProperty("_enemyType").intValue = (int)type;
        serialized.ApplyModifiedProperties();
    }

    /// <summary>설정 에셋과 영속 매니저·상태 표시 프리팹을 생성하며 기존 설정이 있으면 보존한다.</summary>
    private static void PrepareManagerAssets()
    {
        const string SETTINGS_PATH = "Assets/Resources/TelemetrySettings.asset";
        const string MANAGER_PATH = "Assets/Resources/Prefabs/GameManagers/TelemetryManager.prefab";
        TelemetrySettings settings = AssetDatabase.LoadAssetAtPath<TelemetrySettings>(SETTINGS_PATH);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<TelemetrySettings>();
            AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MANAGER_PATH) != null) return;
        var manager = new GameObject("TelemetryManager");
        try
        {
            TelemetryManager component = manager.AddComponent<TelemetryManager>();
            manager.AddComponent<TelemetryStatusUI>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("_settings").objectReferenceValue = settings;
            serialized.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(manager, MANAGER_PATH);
        }
        finally { UnityEngine.Object.DestroyImmediate(manager); }
    }
}

[CustomEditor(typeof(EnemyTelemetrySource))]
[CanEditMultipleObjects]
public sealed class EnemyTelemetrySourceInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("_enemyId"));
        DrawPropertiesExcluding(serializedObject, "m_Script", "_enemyId");
        serializedObject.ApplyModifiedProperties();
    }
}
