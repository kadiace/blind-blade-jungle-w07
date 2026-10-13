using UnityEngine;

public readonly struct DamageContext
{
    public float Amount { get; }
    public GameObject Instigator { get; }
    public Vector3 Point { get; }
    public Vector3 Direction { get; }
    public string PatternId { get; }
    public EnemyAttackTrace AttackTrace { get; }

    /// <summary>
    /// amount, instigator, point, direction, patternId와 선택적인 attackTrace로 피해 수신 문맥을 만든다.
    /// 피해 정보와 내부 공격 추적 참조를 반환 객체에 보관하며 추적 참조는 파일에 저장하지 않는다.
    /// </summary>
    public DamageContext(float amount, GameObject instigator, Vector3 point, Vector3 direction, string patternId, EnemyAttackTrace attackTrace = null)
    {
        Amount = amount;
        Instigator = instigator;
        Point = point;
        Direction = direction;
        PatternId = patternId;
        AttackTrace = attackTrace;
    }
}
