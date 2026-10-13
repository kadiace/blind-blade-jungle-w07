using UnityEngine;

/// <summary>
/// 현재 플레이어가 마지막으로 실제 피해를 받은 뒤 일정 시간이 지나면 체력을 천천히 회복한다.
/// 가드·패링으로 막은 공격은 회복 대기를 초기화하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DefenseHealthRegen : MonoBehaviour
{
    [Header("자연 회복")]
    [SerializeField, Min(0f), Tooltip("마지막 피해 이후 회복을 시작하기까지의 시간(초)")]
    private float _delay = 5f;
    [SerializeField, Min(0f), Tooltip("회복 중 초당 체력 회복량")]
    private float _healthPerSecond = 3f;
    private PlayerHealth _health;
    private float _lastDamageTime;

    void Start()
    {
        _health = Game.Player.GetComponent<PlayerHealth>();
        _health.DamageReceived += HandleDamage;
        _lastDamageTime = Time.time;
    }

    void Update()
    {
        if (_health.IsAlive && Time.time - _lastDamageTime >= _delay)
            _health.Heal(_healthPerSecond * Time.deltaTime);
    }

    void OnDestroy()
    {
        if (_health != null)
            _health.DamageReceived -= HandleDamage;
    }

    /// <summary>context의 실제 피해 시각을 기록해 회복 대기를 다시 시작한다.</summary>
    private void HandleDamage(DamageContext context)
    {
        _lastDamageTime = Time.time;
    }
}
