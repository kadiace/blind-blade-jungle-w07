using UnityEngine;

/// <summary>
/// 근접 적이 시야와 무관하게 항상 현재 플레이어를 향해 달려오도록 소음 위치를 주기적으로 갱신한다.
/// 추적·공격 중에는 기존 EnemyController의 판정을 그대로 따른다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyController))]
public sealed class DefenseChaser : MonoBehaviour
{
    [Header("추적 고정")]
    [SerializeField, Min(0.05f)] private float _alertInterval = 0.25f;
    [SerializeField, Min(0.1f)] private float _alertDuration = 1f;
    private EnemyController _controller;
    private float _nextAlertAt;

    void Awake()
    {
        _controller = GetComponent<EnemyController>();
    }

    void OnEnable()
    {
        _nextAlertAt = 0f;
    }

    void Update()
    {
        if (Time.time < _nextAlertAt)
            return;
        _nextAlertAt = Time.time + _alertInterval;
        PlayerController player = Game.Player;
        EnemyState state = _controller.State;
        if (player == null || state == EnemyState.Chasing || state == EnemyState.Attacking)
            return;
        _controller.HearNoise(player.transform.position, _alertDuration);
    }
}
