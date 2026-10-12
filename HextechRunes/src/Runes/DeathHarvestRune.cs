namespace HextechRunes;

// 吸血习性（类名与模型 ID 沿用重做前的“死亡收割”，保持存档与配置身份）。
public sealed class DeathHarvestRune : HextechRelicBase
{
	// 本符文自己的回血经过治疗补丁时放行；Heal 的前缀在调用时同步执行，调用返回后立即复位。
	private static bool _isOwnHeal;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("HealPercent", 25m)
	];

	/// <summary>
	/// 持有者不能通过本符文以外的方式回复生命。濒死复活类的回血（当前生命为 0）照常放行，
	/// 否则复活效果会被消耗却留下 0 生命的存活状态。
	/// </summary>
	internal static bool BlocksHeal(Player? player, Creature creature)
	{
		return !_isOwnHeal
			&& player != null
			&& creature == player.Creature
			&& creature.CurrentHp > 0
			&& player.GetRelic<DeathHarvestRune>() != null;
	}

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (Owner.Creature.IsDead
			|| target.Side != CombatSide.Enemy
			|| result.UnblockedDamage <= 0
			|| !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		int heal = FloorToInt(result.UnblockedDamage * DynamicVars["HealPercent"].BaseValue / 100m);
		if (heal <= 0)
		{
			return;
		}

		Flash([Owner.Creature]);
		Task healTask;
		_isOwnHeal = true;
		try
		{
			healTask = CreatureCmd.Heal(Owner.Creature, heal);
		}
		finally
		{
			_isOwnHeal = false;
		}

		await healTask;
	}
}
