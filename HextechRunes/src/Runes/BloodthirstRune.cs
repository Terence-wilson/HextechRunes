namespace HextechRunes;

public sealed class BloodthirstRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("HealPercent", 15m)
	];

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
		await CreatureCmd.Heal(Owner.Creature, heal);
	}
}
