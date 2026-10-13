namespace HextechRunes;

internal sealed class MikaelsBlessingEnemyHex : HextechEnemyHexEffect
{
	private const decimal HealPercent = 0.25m;

	internal override MonsterHexKind Kind => MonsterHexKind.MikaelsBlessing;

	internal override async Task AfterEnemyHealthThreshold(HextechEnemyHexContext context, Creature target, uint combatId)
	{
		// 每个敌人每场战斗的触发次数按幕 1/2/3 次；回复量固定为最大生命的 25%。
		if (context.Tracking.MikaelsBlessingTriggers.GetValueOrDefault(combatId, 0) >= context.TierValue(Kind, 1, 2, 3))
		{
			return;
		}

		context.Tracking.MikaelsBlessingTriggers[combatId] = context.Tracking.MikaelsBlessingTriggers.GetValueOrDefault(combatId, 0) + 1;
		int heal = HextechEnemyHexContext.FractionOfMaxHp(target, HealPercent);
		HextechMikaelsBlessingVfx.Play(target);
		await CreatureCmd.Heal(target, heal);

		List<PowerModel> negativePowers = target.Powers
			.Where(static power => power.GetTypeForAmount(power.Amount) == PowerType.Debuff)
			.ToList();
		foreach (PowerModel power in negativePowers)
		{
			await PowerCmd.Remove(power);
		}
	}
}
