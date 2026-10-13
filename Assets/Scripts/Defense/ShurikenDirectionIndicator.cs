using System.Collections.Generic;

using UnityEngine;

using UnityEngine.UI;

/// <summary>
/// 배그 모바일의 발소리 표시처럼 화면 중심 원 위에 투척 전조와 비행 중인 수리검의 방향을 표시한다.
/// 플레이어 시점 기준 수평 방향만 사용하며 가까울수록 진하고 크게 보인다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShurikenDirectionIndicator : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private BambooDefenseDirector _director;
    private PlayerMotor _motor;

    [Header("원형 표시")]
    [SerializeField, Min(10f), Tooltip("1920×1080 기준 화면 중심에서 표시 원까지의 반경(픽셀)")]
    private float _radius = 230f;
    [SerializeField, Min(1f)] private float _iconSize = 16f;
    [SerializeField, Min(1f)] private float _arcThickness = 4f;
    [SerializeField, Range(5f, 90f)] private float _arcAngle = 18f;
    [SerializeField] private Color _color = new Color(1f, 0.1f, 0.06f, 1f);
    [SerializeField, Min(1)] private int _poolSize = 12;
    private readonly List<Marker> _markers = new List<Marker>();

    [Header("전조·비행 표현")]
    [SerializeField, Min(0.05f), Tooltip("투척 전조 표시 시간(초). 투척수의 전조 시간과 맞춘다.")]
    private float _telegraphDuration = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _telegraphAlpha = 0.55f;
    [SerializeField, Min(0.1f), Tooltip("전조 깜빡임 속도(초당 횟수)")]
    private float _pulseRate = 6f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.3f;
    [SerializeField, Min(0.1f), Tooltip("이 거리 이하에서 가장 진하고 크게 표시한다.")]
    private float _nearDistance = 4f;
    [SerializeField, Min(0.1f), Tooltip("이 거리 이상에서 가장 흐리고 작게 표시한다.")]
    private float _farDistance = 30f;
    [SerializeField, Min(0.1f), Tooltip("가장 먼 거리에서의 아이콘 크기 배율")]
    private float _farScale = 0.8f;
    [SerializeField, Min(0.1f), Tooltip("가장 가까운 거리에서의 아이콘 크기 배율. 근접도의 제곱으로 커져 가까울수록 급격히 커진다.")]
    private float _nearScale = 3f;

    private enum MarkerMode
    {
        Hidden,
        Telegraph,
        Flight,
        Fade
    }

    private sealed class Marker
    {
        public DefenseDirectionMarker Graphic { get; }
        public MarkerMode Mode { get; set; }
        public DefenseShuriken Shuriken { get; set; }
        public Vector3 Position { get; set; }
        public float StartedAt { get; set; }
        public float FadeFromAlpha { get; set; }

        /// <summary>graphic을 숨김 상태의 재사용 표시로 보관한다.</summary>
        public Marker(DefenseDirectionMarker graphic)
        {
            Graphic = graphic;
            Mode = MarkerMode.Hidden;
        }
    }

    void Awake()
    {
        GameObject canvasObject = new GameObject("ShurikenDirectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        for (int i = 0; i < _poolSize; i++)
            _markers.Add(new Marker(CreateGraphic(canvasObject.transform, i)));
    }

    void OnEnable()
    {
        _director.ThrowTelegraphed += HandleTelegraph;
        _director.ShurikenLaunched += HandleLaunch;
    }

    void OnDisable()
    {
        _director.ThrowTelegraphed -= HandleTelegraph;
        _director.ShurikenLaunched -= HandleLaunch;
        foreach (Marker marker in _markers)
            Hide(marker);
    }

    void LateUpdate()
    {
        PlayerController player = Game.Player;
        if (player == null)
            return;
        if (_motor == null || _motor.gameObject != player.gameObject)
            _motor = player.GetComponent<PlayerMotor>();
        foreach (Marker marker in _markers)
        {
            if (marker.Mode != MarkerMode.Hidden)
                UpdateMarker(marker);
        }
    }

    /// <summary>parent 아래 화면 중심에 놓인 방향 표시 그래픽을 index 이름으로 만들고 숨긴 상태로 반환한다.</summary>
    private DefenseDirectionMarker CreateGraphic(Transform parent, int index)
    {
        GameObject markerObject = new GameObject("Marker_" + index, typeof(RectTransform), typeof(DefenseDirectionMarker));
        RectTransform rect = (RectTransform)markerObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        DefenseDirectionMarker graphic = markerObject.GetComponent<DefenseDirectionMarker>();
        graphic.color = _color;
        graphic.raycastTarget = false;
        graphic.Configure(_radius, _iconSize, _arcThickness, _arcAngle);
        graphic.enabled = false;
        return graphic;
    }

    /// <summary>position의 투척 전조를 깜빡이는 흐린 표시로 등록한다. 빈 표시가 없으면 생략한다.</summary>
    private void HandleTelegraph(Vector3 position)
    {
        Marker marker = AcquireMarker();
        if (marker == null)
            return;
        marker.Mode = MarkerMode.Telegraph;
        marker.Shuriken = null;
        marker.Position = position;
        marker.StartedAt = Time.time;
        marker.Graphic.enabled = true;
    }

    /// <summary>shuriken의 비행을 위치 추적 표시로 등록하고 비행 종료 시 페이드하도록 구독한다.</summary>
    private void HandleLaunch(DefenseShuriken shuriken)
    {
        Marker marker = AcquireMarker();
        if (marker == null)
            return;
        marker.Mode = MarkerMode.Flight;
        marker.Shuriken = shuriken;
        marker.Position = shuriken.transform.position;
        marker.StartedAt = Time.time;
        marker.Graphic.enabled = true;
        shuriken.Released += HandleReleased;
    }

    /// <summary>shuriken을 추적하던 표시를 마지막 위치에서 페이드 상태로 전환하고 구독을 해제한다.</summary>
    private void HandleReleased(DefenseShuriken shuriken)
    {
        shuriken.Released -= HandleReleased;
        foreach (Marker marker in _markers)
        {
            if (marker.Shuriken != shuriken)
                continue;
            marker.Position = shuriken.transform.position;
            marker.FadeFromAlpha = FlightAlpha(Closeness(marker.Position));
            marker.Shuriken = null;
            marker.Mode = MarkerMode.Fade;
            marker.StartedAt = Time.time;
        }
    }

    /// <summary>
    /// marker의 추적 위치를 시점 기준 수평 각도로 바꿔 원 위 회전·아이콘 크기·투명도를 갱신한다.
    /// 아이콘은 근접도의 제곱으로 커지고 원 반경은 유지하며 전조 시간 또는 페이드가 끝난 표시는 숨긴다.
    /// </summary>
    private void UpdateMarker(Marker marker)
    {
        if (marker.Mode == MarkerMode.Flight)
            marker.Position = marker.Shuriken.transform.position;
        float elapsed = Time.time - marker.StartedAt;
        float closeness = Closeness(marker.Position);
        float alpha;
        switch (marker.Mode)
        {
            case MarkerMode.Telegraph:
                if (elapsed >= _telegraphDuration)
                {
                    Hide(marker);
                    return;
                }
                alpha = _telegraphAlpha * (0.6f + 0.4f * (0.5f + 0.5f * Mathf.Sin(elapsed * _pulseRate * Mathf.PI * 2f)));
                break;
            case MarkerMode.Fade:
                if (elapsed >= _fadeDuration)
                {
                    Hide(marker);
                    return;
                }
                alpha = marker.FadeFromAlpha * (1f - elapsed / _fadeDuration);
                break;
            default:
                alpha = FlightAlpha(closeness);
                break;
        }
        Vector3 offset = Vector3.ProjectOnPlane(marker.Position - _motor.ViewOrigin, Vector3.up);
        float angle = offset.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(_motor.ViewForward, offset, Vector3.up) : 0f;
        RectTransform rect = marker.Graphic.rectTransform;
        rect.localRotation = Quaternion.Euler(0f, 0f, -angle);
        marker.Graphic.SetIconScale(Mathf.Lerp(_farScale, _nearScale, closeness * closeness));
        marker.Graphic.canvasRenderer.SetAlpha(alpha);
    }

    /// <summary>position과 플레이어 시점의 거리를 먼 거리 0, 가까운 거리 1 사이의 근접도로 반환한다.</summary>
    private float Closeness(Vector3 position)
    {
        return Mathf.InverseLerp(_farDistance, _nearDistance, Vector3.Distance(position, _motor.ViewOrigin));
    }

    /// <summary>closeness로 비행 중 표시의 투명도를 반환한다. 멀어도 읽을 수 있도록 최소 0.6을 유지한다.</summary>
    private static float FlightAlpha(float closeness)
    {
        return Mathf.Lerp(0.6f, 1f, closeness);
    }

    /// <summary>숨김 상태의 표시를 반환한다. 모두 사용 중이면 null을 반환한다.</summary>
    private Marker AcquireMarker()
    {
        foreach (Marker marker in _markers)
        {
            if (marker.Mode == MarkerMode.Hidden)
                return marker;
        }
        return null;
    }

    /// <summary>marker를 숨기고 추적 대상을 해제한다. 비행 중 수리검의 구독도 함께 해제한다.</summary>
    private void Hide(Marker marker)
    {
        if (marker.Shuriken != null)
            marker.Shuriken.Released -= HandleReleased;
        marker.Shuriken = null;
        marker.Mode = MarkerMode.Hidden;
        marker.Graphic.enabled = false;
    }
}
