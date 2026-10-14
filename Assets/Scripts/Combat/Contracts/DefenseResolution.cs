namespace BlindBlade.Combat.Contracts
{
    public enum IncomingAttackKind
    {
        Unspecified,
        Melee,
        Projectile,
        Charge
    }

    public enum DefenseOutcome
    {
        Unresolved,
        Hit,
        Guard,
        NormalParry,
        PerfectParry,
        DodgeAvoided,
        Invulnerable
    }

    /// <summary>A가 한 공격의 개별 타격에 대해 확정하는 동기 판정 결과다.</summary>
    public readonly struct DefenseResolution
    {
        public long AttackId { get; }
        public long HitId { get; }
        public DefenseOutcome Outcome { get; }

        /// <summary>
        /// attackId, hitId와 outcome을 저장해 해당 타격의 방어 결과를 반환한다.
        /// 피해 적용, 반사, 보상 발행은 수행하지 않고 각 담당자에게 결과만 전달한다.
        /// </summary>
        public DefenseResolution(long attackId, long hitId, DefenseOutcome outcome)
        {
            AttackId = attackId;
            HitId = hitId;
            Outcome = outcome;
        }
    }
}
