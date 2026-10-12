namespace HextechRunes;

public sealed class KeystoneHunterRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<ToolsOfTheTradePower>(1m),
		new PowerVar<MasterPlannerPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<ToolsOfTheTradePower>(),
		HoverTipFactory.FromPower<MasterPlannerPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsSilentPlayer(player);
	}

	public override async Task BeforeCombatStart()
	{
		if (Owner.Creature.IsDead || !IsSilentPlayer(Owner))
		{
			return;
		}

		Flash();
		await HextechCombatVfx.KeystoneHunterBlueprint(Owner.Creature);
		await PowerCmd.Apply<ToolsOfTheTradePower>(Owner.Creature, DynamicVars["ToolsOfTheTradePower"].BaseValue, Owner.Creature, null);
		await PowerCmd.Apply<MasterPlannerPower>(Owner.Creature, DynamicVars["MasterPlannerPower"].BaseValue, Owner.Creature, null);
	}
}
