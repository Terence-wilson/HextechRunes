using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HextechRunes;

public sealed class CompactUpgradeRune : CardUpgradeRuneBase<Compact>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsDefectPlayer(player);
	}

	internal static bool ShouldUseUpgradedPlay(CardModel card)
	{
		return card is Compact && card.Owner?.GetRelic<CompactUpgradeRune>() != null;
	}

	// 原版只把手牌里的状态牌转成燃料；原版结算（动画、格挡、手牌转化）完成后，把其余牌堆的状态牌也转成燃料。
	internal static async Task TransformRemainingStatusesAfterVanilla(Task vanilla, Compact card)
	{
		await vanilla;
		Player owner = card.Owner;
		if (card.CombatState is not { } combatState || owner.PlayerCombatState is not { } playerCombatState)
		{
			return;
		}

		List<CardTransformation> transformations = new();
		foreach (CardPile pile in playerCombatState.AllPiles)
		{
			foreach (CardModel statusCard in pile.Cards)
			{
				if (!statusCard.IsTransformable || statusCard.Type != CardType.Status)
				{
					continue;
				}

				CardModel fuel = combatState.CreateCard<Fuel>(owner);
				if (card.IsUpgraded)
				{
					CardCmd.Upgrade(fuel);
				}

				transformations.Add(new CardTransformation(statusCard, fuel));
			}
		}

		if (transformations.Count == 0)
		{
			return;
		}

		owner.GetRelic<CompactUpgradeRune>()?.Flash();
		await CardCmd.Transform(transformations, null, CardPreviewStyle.None);
	}

	// 不跳过原版：原版照常结算，后缀接在原版任务之后补转其余牌堆。
	[HarmonyPatch(typeof(Compact), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.compact", "升级压缩", Rune = typeof(CompactUpgradeRune))]
	private static class CompactPatch
	{
		[HarmonyPostfix]
		private static void Postfix(Compact __instance, ref Task __result)
		{
			if (CompactUpgradeRune.ShouldUseUpgradedPlay(__instance))
			{
				__result = CompactUpgradeRune.TransformRemainingStatusesAfterVanilla(__result, __instance);
			}
		}
	}
}
