namespace HextechRunes;

public sealed class BadgeBrothersRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<FreeAttackPower>(1m),
		new PowerVar<FreeSkillPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<FreeAttackPower>(),
		HoverTipFactory.FromPower<FreeSkillPower>()
	];

	public override async Task BeforeCombatStart()
	{
		if (Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await HextechCombatVfx.BadgeBrothersSlam<FreeAttackPower>(Owner.Creature, -1f);
		await PowerCmd.Apply<FreeAttackPower>(Owner.Creature, DynamicVars["FreeAttackPower"].BaseValue, Owner.Creature, null);
		await HextechCombatVfx.BadgeBrothersSlam<FreeSkillPower>(Owner.Creature, 1f);
		await PowerCmd.Apply<FreeSkillPower>(Owner.Creature, DynamicVars["FreeSkillPower"].BaseValue, Owner.Creature, null);
	}
}
