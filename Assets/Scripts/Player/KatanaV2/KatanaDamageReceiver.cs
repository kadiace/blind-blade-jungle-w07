using UnityEngine;


/// <summary>적의 기존 IDamageable 호출을 받아 피해 전달 전에 전방 근접·보스 돌진 방어를 확인한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(KatanaCombatCoordinator))]
public sealed class KatanaDamageReceiver : PlayerHealth, IPlayerDefenseResolver
{
    [Header("Katana connection")]
    [SerializeField] private KatanaCombatCoordinator _combat;
    [SerializeField] private Transform _view;
    [SerializeField, Range(1f, 180f)] private float _frontArc = 120f;
    [System.NonSerialized] private PlayerMotor _motor;

    protected override void Awake()
    {
        base.Awake();
        _motor = GetComponent<PlayerMotor>();
        if (_combat == null) _combat = GetComponent<KatanaCombatCoordinator>();
        if (_view == null)
        {
            Camera camera = GetComponentInChildren<Camera>();
            _view = camera != null ? camera.transform : transform;
        }
    }

    void OnEnable()
    {
        _motor = GetComponent<PlayerMotor>();
    }

    /// <summary>
    /// 기존 IDamageable 호출을 ResolveIncomingAttack에 전달하고 판정 결과는 사용하지 않는다.
    /// 방어에 실패한 피해만 PlayerHealth가 HP 차감과 사망 및 피해 이벤트로 처리한다.
    /// </summary>
    public override void TakeDamage(DamageContext context)
    {
        ResolveIncomingAttack(context);
    }

    /// <summary>
    /// context의 공격 1회를 가드·패링으로 판정하고 방어하지 못하면 체력을 줄인 뒤 결과를 반환한다.
    /// Kind가 Projectile이면 진입 방향과 반격 없음 규칙을, 그 외에는 기존 전방 근접·보스 공격 규칙을 사용한다.
    /// 사망·일시정지·시간 정지에서는 Ignored를 반환하며 NormalParry·DodgeAvoided·Invulnerable은 아직 반환하지 않는다.
    /// 모든 결과는 반환 전에 DefenseResolved 사건으로 한 번 발행한다.
    /// </summary>
    public DefenseResolution ResolveIncomingAttack(DamageContext context)
    {
        if (!isActiveAndEnabled || context.Amount <= 0f || _combat.Player.IsPaused
            || Time.timeScale <= 0f || _combat.Player.CurrentState == PlayerActionState.Dead)
            return PublishResolution(context, DefenseOutcome.Ignored, 0f);

        // 투사체는 반사·강화 보상을 투사체 쪽에서 처리하므로 근접 자동 반격을 만들지 않는다.
        bool projectile = context.Kind == IncomingAttackKind.Projectile;
        bool eligible = projectile
            ? IsFrontProjectile(context)
            : IsFrontMelee(context) || IsFrontBossCharge(context) || IsFrontBossRanged(context);
        bool perfect = _combat.IsParryWindowOpen;
        if (_combat.TryDefendIncoming(context, eligible, !projectile))
            return PublishResolution(context, perfect ? DefenseOutcome.PerfectParry : DefenseOutcome.Guard, 0f);

        float healthBefore = CurrentHealth;
        base.TakeDamage(context);
        return PublishResolution(context, DefenseOutcome.Hit, healthBefore - CurrentHealth);
    }

    /// <summary>
    /// context의 투사체 진입 방향으로 전방 가드·패링을 검사하고 방어 성공 여부를 반환한다.
    /// parried에 정확한 패링 여부를 출력하며 기존 방어 효과를 적용한다. allowCounter가 false이면 반격 기회를 만들지 않는다.
    /// 방어에 성공한 경우에만 DefenseResolved를 발행하고, 실패는 호출자의 TakeDamage가 Hit으로 발행한다.
    /// </summary>
    public bool TryDefendProjectile(DamageContext context, out bool parried, bool allowCounter = true)
    {
        parried = false;
        if (!isActiveAndEnabled) return false;
        bool eligible = IsFrontProjectile(context);
        bool perfect = _combat.IsParryWindowOpen;
        if (!_combat.TryDefendIncoming(context, eligible, allowCounter)) return false;
        parried = perfect;
        PublishResolution(context, perfect ? DefenseOutcome.PerfectParry : DefenseOutcome.Guard, 0f);
        return true;
    }

    /// <summary>context의 공격자가 실행 중인 유일한 패턴이 근접 공격이고 전방 각도 안에 있는지 반환한다.</summary>
    private bool IsFrontMelee(DamageContext context)
    {
        if (context.Instigator == null) return false;
        EnemyAttackBehaviour matching = null;
        foreach (EnemyAttackBehaviour pattern in context.Instigator.GetComponents<EnemyAttackBehaviour>())
        {
            if (pattern.PatternId != context.PatternId) continue;
            if (matching != null) return false;
            matching = pattern;
        }
        if (!(matching is EnemyMeleeAttack) || matching.Phase != EnemyAttackPhase.Active) return false;
        Vector3 toEnemy = Vector3.ProjectOnPlane(context.Instigator.transform.position - transform.position, Vector3.up);
        Vector3 forward = _motor.ViewForward;
        return toEnemy.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f
            && Vector3.Angle(forward, toEnemy) <= _frontArc * 0.5f;
    }

    /// <summary>context가 진행 중인 보스 돌진이고 진입 방향이 전방 방어 각도 안인지 반환한다.</summary>
    private bool IsFrontBossCharge(DamageContext context)
    {
        if (context.PatternId != "BossCharge" || context.Instigator == null) return false;
        BossController boss = context.Instigator.GetComponent<BossController>();
        if (boss == null || (boss.State != BossState.Charging && boss.State != BossState.ChainCharge)) return false;
        Vector3 incoming = Vector3.ProjectOnPlane(-context.Direction, Vector3.up);
        return incoming.sqrMagnitude > 0.0001f
            && Vector3.Angle(_motor.ViewForward, incoming) <= _frontArc * 0.5f;
    }

    /// <summary>보스 원거리 투사체의 진입 방향이 전방 방어 각도 안인지 반환한다. 발사 후 보스 상태와 무관하게 검사한다.</summary>
    private bool IsFrontBossRanged(DamageContext context)
    {
        if (context.PatternId != "BossRanged" || context.Instigator == null
            || context.Instigator.GetComponent<BossController>() == null) return false;
        Vector3 incoming = Vector3.ProjectOnPlane(-context.Direction, Vector3.up);
        return incoming.sqrMagnitude > 0.0001f
            && Vector3.Angle(_motor.ViewForward, incoming) <= _frontArc * 0.5f;
    }

    /// <summary>context의 투사체 진입 방향이 시점 기준 전방 방어 각도 안인지 반환한다. 공격자 상태는 검사하지 않는다.</summary>
    private bool IsFrontProjectile(DamageContext context)
    {
        Vector3 incoming = Vector3.ProjectOnPlane(-context.Direction, Vector3.up);
        return incoming.sqrMagnitude > 0.0001f
            && Vector3.Angle(_motor.ViewForward, incoming) <= _frontArc * 0.5f;
    }

    /// <summary>
    /// context의 AttackId·HitId·Point와 현재 게임 시간·실시간으로 outcome 판정 결과를 만들고 DefenseResolved로 발행한 뒤 반환한다.
    /// 가드·패링 결과에는 Coordinator의 GuardInputId를 넣으며 appliedDamage는 이번 판정에서 실제로 줄어든 체력이다.
    /// </summary>
    private DefenseResolution PublishResolution(DamageContext context, DefenseOutcome outcome, float appliedDamage)
    {
        bool defended = outcome == DefenseOutcome.Guard || outcome == DefenseOutcome.NormalParry
            || outcome == DefenseOutcome.PerfectParry;
        DefenseResolution resolution = new DefenseResolution(outcome, context.AttackId, context.HitId,
            defended ? _combat.GuardInputId : 0, appliedDamage, context.Point,
            Time.time, Time.realtimeSinceStartupAsDouble);
        _combat.Events.RaiseDefenseResolved(resolution);
        return resolution;
    }

    /// <summary>context의 방향 제한 없는 기본 가드는 사용하지 않는다. 전방 근접·보스 돌진 방어는 Coordinator에서 확정한다.</summary>
    protected override bool CanBlockDamage(DamageContext context)
    {
        return false;
    }
}
