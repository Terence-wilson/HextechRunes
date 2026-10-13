namespace HextechRunes;

internal sealed class BloodIdolEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.BloodIdol;

	internal override async Task AfterGoldGained(HextechEnemyHexContext context, Player player)
	{
		if (player.RunState != context.RunState || player.Creature.IsDead)
		{
			return;
		}

		if (player.Creature.CombatState == null || !CombatManager.Instance.IsInProgress
			|| CombatManager.Instance.IsOverOrEnding)
		{
			// 奖励界面已不再处理战斗死亡/队友复活。战斗外直接扣生命并保留 1 点，
			// 仍走原版命令通知生命变化；不让战斗伤害修正把这次扣血放大至致死。
			int hp = NonCombatHpAfterGold(player.Creature.CurrentHp);
			if (hp != player.Creature.CurrentHp)
			{
				await CreatureCmd.SetCurrentHp(player.Creature, hp);
			}

			return;
		}

		// 战斗中（如夺金）获得金币时受到 1 点伤害，可被格挡。
		await CreatureCmd.Damage(
			new BlockingPlayerChoiceContext(),
			player.Creature,
			1m,
			ValueProp.Unpowered,
			null,
			null);
	}

	internal static int NonCombatHpAfterGold(int currentHp) => Math.Max(1, currentHp - 1);
}
