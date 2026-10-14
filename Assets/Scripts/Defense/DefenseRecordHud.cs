using UnityEngine;

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

    [SerializeField, Tooltip("HUD 프리팹의 Record. 글꼴·크기·색·배치는 해당 UI에서 조정합니다.")]
    private TextMeshProUGUI _text;
    private int _shownSeconds = -1;
    private int _shownKills = -1;
    private bool _shownStarted;

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
