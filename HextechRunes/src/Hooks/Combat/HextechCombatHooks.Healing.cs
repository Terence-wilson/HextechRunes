using MegaCrit.Sts2.Core.Models.Monsters;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private readonly record struct HealPostState(Player? Player, Creature Creature, int CurrentHpBefore, bool ShouldProcess);

	internal static decimal ClampHealAmountToCap(int currentHp, int maxHp, decimal amount, decimal capPercent)
	{
		if (amount <= 0m)
		{
			return amount;
		}

		int healCap = (int)Math.Floor(maxHp * capPercent);
		return Math.Min(amount, Math.Max(0m, healCap - currentHp));
	}

	private static async Task HealAfterOriginal(Task original, HealPostState state)
	{
		await original;

		Player? player = state.Player;
		Creature creature = state.Creature;
		// 按实际血量变化算：被上限截断的部分不算，治疗期间同时掉血也不能变成负治疗。
		decimal amount = Math.Max(0m, creature.CurrentHp - (decimal)state.CurrentHpBefore);
		if (amount <= 0m)
		{
			return;
		}

		if (player?.GetRelic<CircleOfDeathRune>() is CircleOfDeathRune circleOfDeathRune
			&& creature == player.Creature
			&& creature.CombatState != null)
		{
			await circleOfDeathRune.HandleSustainGained(amount);
		}

		// 我们的治疗(仅联机):持有者被治疗后分享给每个存活队友,战斗内外通吃。
		await OurHealingRune.ShareHolderHeal(creature, amount);
	}

	private static bool IsSkulkingColony(Creature creature)
	{
		return creature.Side == CombatSide.Enemy && creature.Monster is SkulkingColony;
	}

	private static bool IsEnemyReviveHeal(Creature creature, decimal amount)
	{
		return creature.Side == CombatSide.Enemy && creature.IsDead && amount > 0m;
	}

	private static bool TryQueueEnemyHealAsDelayedBlock(
		Creature creature,
		decimal amount,
		RunState? runState,
		HextechMayhemModifier? modifier)
	{
		// 未启用本模组的局(或缺少本局 modifier)保持原版治疗:不在治疗回调里补挂 modifier,
		// 也不改原版潜伏虫群的回血。
		if (creature.Side != CombatSide.Enemy
			|| amount <= 0m
			|| runState == null
			|| modifier == null
			|| !modifier.IsModActiveForRun)
		{
			return false;
		}

		List<RegenerationSuppressionRune> suppressionRunes = runState.Players
			.Select(static player => player.GetRelic<RegenerationSuppressionRune>())
			.OfType<RegenerationSuppressionRune>()
			.ToList();
		if (!IsSkulkingColony(creature) && suppressionRunes.Count == 0)
		{
			return false;
		}

		if (!modifier.QueueEnemyHealingBlock(creature, amount))
		{
			return false;
		}

		foreach (RegenerationSuppressionRune rune in suppressionRunes)
		{
			rune.NotifyEnemyHealSuppressed(creature);
		}

		return true;
	}

	[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal), typeof(Creature), typeof(decimal), typeof(bool))]
	[HextechPatch("combat.heal", "治疗修正")]
	private static class HealPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Creature creature, ref decimal amount, ref Task __result, out HealPostState __state)
		{
			__state = default;
			// 进入本前缀时治疗量已不大于 0（原版或其他模组的前缀给的），不由我们跳过，照原版执行。
			if (amount <= 0m)
			{
				return true;
			}

			if (NearDeathFeastRune.IsDyingButAlive(creature) || HextechEnemyNearDeath.IsDyingButAlive(creature))
			{
				return SkipHeal(ref __result);
			}

			Player? player = creature.Player;
			if (DeathHarvestRune.BlocksHeal(player, creature))
			{
				return SkipHeal(ref __result);
			}

			if (player != null && creature == player.Creature)
			{
				amount *= HextechPlayerCoefficientHelper.GetHealingMultiplier(player);
			}

			if (player?.GetRelic<GlassCannonRune>() is GlassCannonRune glassCannonRune && creature == player.Creature)
			{
				amount = ClampHealAmountToCap(creature.CurrentHp, creature.MaxHp, amount, glassCannonRune.HealCapPercent);
				if (amount <= 0m)
				{
					return SkipHeal(ref __result);
				}
			}

			// 敌方回血修正与延迟格挡都记在本局 Modifier 上。
			RunState? currentRunState = creature.CombatState?.RunState as RunState;
			HextechMayhemModifier? modifier = null;
			bool isEnemyReviveHeal = IsEnemyReviveHeal(creature, amount);
			if (creature.Side == CombatSide.Enemy
				&& currentRunState != null
				&& !isEnemyReviveHeal
				&& HextechMayhemModifier.FindIn(currentRunState) is HextechMayhemModifier activeModifier)
			{
				modifier = activeModifier;
				amount = modifier.ModifyEnemyHealAmount(creature, amount);
				if (amount <= 0m)
				{
					return SkipHeal(ref __result);
				}
			}

			if (!isEnemyReviveHeal && TryQueueEnemyHealAsDelayedBlock(creature, amount, currentRunState, modifier))
			{
				return SkipHeal(ref __result);
			}

			if (amount <= 0m)
			{
				return SkipHeal(ref __result);
			}

			__state = new HealPostState(player, creature, creature.CurrentHp, ShouldProcess: true);
			return true;
		}

		// 原版 Heal 对 0 也播音效/特效，被本模组禁止或吃掉的回血只能跳过原方法（见 architecture.md 已裁决保留）。
		// 只跳过本前缀自己改到 0 的治疗；进来时就是 0 的治疗交给原版，不吞掉其他模组的处理。
		private static bool SkipHeal(ref Task result)
		{
			result = Task.CompletedTask;
			return false;
		}

		// 封顶必须是治疗前缀链的最后一环:别的模组(无尽/RitsuLib/BaseLib)的加减乘都算完,再按当前生命封顶。
		// 原版 Heal 对 0 也会播治疗音效与特效,所以"禁止回血"仍由上面的 Prefix 跳过原方法,这里只做数值封顶。
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Last)]
		[HarmonyAfter(EndlessModeHarmonyId, RitsuLibCoreHarmonyId, BaseLibHarmonyId)]
		private static void FinalCapPrefix(Creature creature, ref decimal amount)
		{
			if (amount <= 0m || IsEnemyReviveHeal(creature, amount))
			{
				return;
			}

			decimal? capPercent = null;
			Player? player = creature.Player;
			if (player?.GetRelic<GlassCannonRune>() is GlassCannonRune glassCannonRune
				&& creature == player.Creature)
			{
				capPercent = glassCannonRune.HealCapPercent;
			}
			else if (TryGetActiveEnemyHexModifier(creature, MonsterHexKind.GlassCannon, out _))
			{
				capPercent = GlassCannonEnemyHex.HealCapPercent;
			}

			if (capPercent.HasValue)
			{
				amount = ClampHealAmountToCap(creature.CurrentHp, creature.MaxHp, amount, capPercent.Value);
			}
		}

		[HarmonyPostfix]
		private static void Postfix(HealPostState __state, ref Task __result)
		{
			if (!__state.ShouldProcess)
			{
				return;
			}

			__result = HealAfterOriginal(__result, __state);
		}
	}
}
