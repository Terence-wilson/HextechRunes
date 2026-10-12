namespace HextechRunes;

internal sealed class DeathHarvestEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.DeathHarvest;

	internal override async Task AfterEnemyDamageGivenImmediate(HextechEnemyHexContext context, Creature dealer, DamageResult result, Creature target, CardModel? cardSource)
	{
		// 吸血习性：敌人对玩家造成的未格挡伤害,按 tier 比例回复自身生命(全程,无回合限制)。
		if (result.UnblockedDamage <= 0 || target.Player == null || !dealer.IsAlive)
		{
			return;
		}

		decimal pct = context.TierValue(Kind, 0.50m, 0.75m, 1.00m);
		int heal = Math.Max(1, (int)Math.Floor(result.UnblockedDamage * pct));
		await CreatureCmd.Heal(dealer, heal);
	}
}
