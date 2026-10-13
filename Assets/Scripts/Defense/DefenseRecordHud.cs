using UnityEngine;

using UnityEngine.UI;

using TMPro;

/// <summary>
/// 대숲 방어의 준비 카운트다운, 생존 시간과 처치 수를 화면 상단에 표시한다.
/// 표시 전용이며 게임 진행 상태는 변경하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DefenseRecordHud : MonoBehaviour
{
    [Header("기록 연결")]
    [SerializeField] private BambooDefenseDirector _director;

    [Header("표시")]
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField, Min(8f)] private float _fontSize = 34f;
    [SerializeField, Min(0f)] private float _topOffset = 28f;
    [SerializeField] private Color _color = new Color(0.85f, 0.85f, 0.85f, 1f);
    private TextMeshProUGUI _text;
    private int _shownSeconds = -1;
    private int _shownKills = -1;
    private bool _shownStarted;

    void Awake()
    {
        GameObject canvasObject = new GameObject("DefenseRecordCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        GameObject textObject = new GameObject("Record", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -_topOffset);
        rect.sizeDelta = new Vector2(900f, 60f);
        _text = textObject.GetComponent<TextMeshProUGUI>();
        if (_font != null)
            _text.font = _font;
        _text.fontSize = _fontSize;
        _text.color = _color;
        _text.alignment = TextAlignmentOptions.Center;
        _text.raycastTarget = false;
    }

    void Update()
    {
        bool started = _director.HasStarted;
        int seconds = started ? Mathf.FloorToInt(_director.SurvivalTime) : Mathf.CeilToInt(_director.Countdown);
        int kills = _director.Kills;
        if (started == _shownStarted && seconds == _shownSeconds && kills == _shownKills)
            return;
        _shownStarted = started;
        _shownSeconds = seconds;
        _shownKills = kills;
        _text.text = started ? $"생존 {seconds / 60:00}:{seconds % 60:00}    처치 {kills}" : "준비 " + seconds;
    }
}
