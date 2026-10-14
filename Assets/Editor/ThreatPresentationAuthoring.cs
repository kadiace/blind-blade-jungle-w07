using System.IO;

using UnityEngine;

using UnityEditor;
using UnityEditor.SceneManagement;

using BlindBlade.Perception;

/// <summary>C 전용 인지 설정·프리팹·작업 씬을 생성한다. 정식 게임 씬이나 프리팹을 저장하지 않는다.</summary>
public static class ThreatPresentationAuthoring
{
    private const string SETTINGS_PATH = "Assets/Settings/Perception/ThreatPresentationSettings.asset";
    private const string ROOT_PATH = "Assets/Scenes/Perception/C_ThreatPresentation";
    private const string SCENE_PATH = ROOT_PATH + "/C_ThreatPresentation.unity";
    private const string MELEE_PATH = ROOT_PATH + "/ThreatPreviewMelee.prefab";
    private const string PROJECTILE_PATH = ROOT_PATH + "/ThreatPreviewProjectile.prefab";

    /// <summary>
    /// 메뉴 또는 Unity CLI 입력 없이 C의 설정·전용 프리팹·작업 씬을 생성하고 연다.
    /// 기존 에셋과 편집값은 재생성하지 않으며 생성한 경로를 로그에 남긴다.
    /// </summary>
    [MenuItem("Tools/Perception/Open or Create C Threat Sandbox")]
    public static void OpenOrCreateSandbox()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Directory.CreateDirectory("Assets/Settings/Perception");
        Directory.CreateDirectory(ROOT_PATH);
        AssetDatabase.Refresh();
        ThreatPresentationSettings settings = AssetDatabase.LoadAssetAtPath<ThreatPresentationSettings>(SETTINGS_PATH);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ThreatPresentationSettings>();
            AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
        }
        CreatePreviewPrefab(MELEE_PATH, PrimitiveType.Capsule, ThreatTargetKind.Melee);
        CreatePreviewPrefab(PROJECTILE_PATH, PrimitiveType.Sphere, ThreatTargetKind.Projectile);
        if (File.Exists(SCENE_PATH))
        {
            EditorSceneManager.OpenScene(SCENE_PATH);
            Debug.Log("C 인지 작업 씬 열기: " + SCENE_PATH);
            return;
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject observer = new GameObject("Observer");
        GameObject cameraObject = new GameObject("ViewCamera");
        cameraObject.transform.SetParent(observer.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        Camera viewCamera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        viewCamera.clearFlags = CameraClearFlags.SolidColor;
        viewCamera.backgroundColor = Color.black;
        viewCamera.nearClipPlane = 0.1f;
        viewCamera.farClipPlane = 100f;
        GameObject presentation = new GameObject("ThreatPresentation");
        ThreatPresentationResolver resolver = presentation.AddComponent<ThreatPresentationResolver>();
        SerializedObject connection = new SerializedObject(resolver);
        connection.FindProperty("_settings").objectReferenceValue = settings;
        connection.FindProperty("_observer").objectReferenceValue = observer.transform;
        connection.FindProperty("_viewCamera").objectReferenceValue = viewCamera;
        connection.ApplyModifiedPropertiesWithoutUndo();

        CreateTarget("Near_Normal", new Vector3(0f, 0f, 2f), resolver);
        CreateTarget("Boundary_Normal", new Vector3(0f, 0f, 11.5f), resolver);
        CreateTarget("Outside_Normal", new Vector3(0f, 0f, 13f), resolver);
        CreateTarget("Offscreen_Normal", new Vector3(6f, 0f, 0f), resolver);
        CreateTarget("Charge_Default", new Vector3(-3f, 0f, 8f), resolver, charge: true);
        CreateTarget("Charge_Individual18m", new Vector3(4f, 0f, 15f), resolver, charge: true, radius: 18f);
        CreateTarget("Hostile_Projectile", new Vector3(-2f, 1f, 6f), resolver, projectile: true);
        CreateTarget("Reflected_Projectile", new Vector3(2f, 1f, 6f), resolver, projectile: true, reflected: true);
        CreateTarget("Passed_Projectile", new Vector3(0f, 1f, -6f), resolver, projectile: true, passed: true);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SCENE_PATH);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = presentation;
        Debug.Log("C 인지 작업 에셋 생성 완료: " + SCENE_PATH + " / " + SETTINGS_PATH);
    }

    /// <summary>
    /// path가 없을 때 primitive 외형과 kind를 가진 비활성 C 전용 프리팹을 저장한다.
    /// Resolver는 씬 인스턴스에 연결하며 게임 전투용 프리팹에는 변경을 적용하지 않는다.
    /// </summary>
    private static void CreatePreviewPrefab(string path, PrimitiveType primitive, ThreatTargetKind kind)
    {
        if (File.Exists(path))
            return;
        GameObject root = GameObject.CreatePrimitive(primitive);
        root.name = Path.GetFileNameWithoutExtension(path);
        root.SetActive(false);
        ThreatPresentationTarget targetComponent = root.AddComponent<ThreatPresentationTarget>();
        SerializedObject data = new SerializedObject(targetComponent);
        data.FindProperty("_kind").enumValueIndex = (int)kind;
        SerializedProperty renderers = data.FindProperty("_renderers");
        renderers.arraySize = 1;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = root.GetComponent<Renderer>();
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    /// <summary>
    /// name·position과 표시 조건으로 C 전용 미리보기를 배치하고 resolver에 연결한다.
    /// charge·radius·projectile·reflected·passed를 Inspector 초기값에 저장하고 원본 프리팹을 유지한다.
    /// </summary>
    private static void CreateTarget(string name, Vector3 position, ThreatPresentationResolver resolver,
        bool charge = false, float radius = -1f, bool projectile = false, bool reflected = false, bool passed = false)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(projectile ? PROJECTILE_PATH : MELEE_PATH);
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        root.name = name;
        root.transform.position = position;
        ThreatPresentationTarget targetComponent = root.GetComponent<ThreatPresentationTarget>();
        SerializedObject data = new SerializedObject(targetComponent);
        data.FindProperty("_resolver").objectReferenceValue = resolver;
        data.FindProperty("_meleeType").enumValueIndex = charge ? (int)MeleeThreatType.Charge : (int)MeleeThreatType.Normal;
        data.FindProperty("_overrideEnemyRadius").boolValue = radius >= 0f;
        data.FindProperty("_enemyRadius").floatValue = radius >= 0f ? radius : 12f;
        data.FindProperty("_ownership").enumValueIndex = reflected ? (int)ThreatOwnership.Reflected : (int)ThreatOwnership.Hostile;
        data.FindProperty("_hasPassedPlayer").boolValue = passed;
        data.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(true);
        resolver.RefreshTarget(targetComponent);
    }
}
