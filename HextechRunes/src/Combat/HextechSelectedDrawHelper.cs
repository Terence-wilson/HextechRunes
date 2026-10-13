using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

internal static class HextechSelectedDrawHelper
{
	private static readonly LocString SelectDrawPrompt = new("card_selection", "HEXTECH_TO_DRAW");

	internal static async Task<IEnumerable<CardModel>> DrawSelectedFromDrawPile(
		PlayerChoiceContext choiceContext,
		Player player,
		int requestedDraws,
		bool fromHandDraw)
	{
		if (requestedDraws <= 0 || CombatManager.Instance.IsOverOrEnding)
		{
			return Array.Empty<CardModel>();
		}

		ICombatState? combatState = player.Creature.CombatState;
		if (combatState == null)
		{
			return Array.Empty<CardModel>();
		}

		bool shouldDraw;
		AbstractModel? modifier;
		HextechDrawRequest.Begin(player, requestedDraws);
		try
		{
			shouldDraw = Hook.ShouldDraw(combatState, player, fromHandDraw, out modifier);
		}
		finally
		{
			HextechDrawRequest.End();
		}

		if (!shouldDraw)
		{
			if (modifier != null)
			{
				await Hook.AfterPreventingDraw(combatState, modifier);
			}

			return Array.Empty<CardModel>();
		}

		CardPile hand = PileType.Hand.GetPile(player);
		CardPile drawPile = PileType.Draw.GetPile(player);
		int handSpace = Math.Max(0, CardPile.MaxCardsInHand - hand.Cards.Count);
		if (handSpace == 0)
		{
			ShowDrawFailureThoughtBubble(player);
			return Array.Empty<CardModel>();
		}

		int cardsToSelect = Math.Min(requestedDraws, handSpace);
		if (!CanDrawAnyCards(player))
		{
			ShowDrawFailureThoughtBubble(player);
			return Array.Empty<CardModel>();
		}

		List<CardModel> drawn = new(cardsToSelect);
		await DrawSelectedRounds(
			cardsToSelect,
			async Task<IReadOnlyList<CardModel>> (int remaining) =>
			{
				await ShuffleIntoDrawPileIfShort(choiceContext, player, remaining);
				int count = Math.Min(remaining, Math.Min(drawPile.Cards.Count, CardPile.MaxCardsInHand - hand.Cards.Count));
				if (count <= 0 || CombatManager.Instance.IsOverOrEnding)
				{
					return Array.Empty<CardModel>();
				}

				CardSelectorPrefs prefs = new(SelectDrawPrompt, count);
				return (await CardSelectCmd.FromCombatPile(choiceContext, drawPile, player, prefs)).Take(count).ToList();
			},
			card => card.Pile == drawPile,
			() => !CombatManager.Instance.IsOverOrEnding && hand.Cards.Count < CardPile.MaxCardsInHand,
			async card =>
			{
				drawn.Add(card);
				await CardPileCmd.Add(card, hand);
				CombatManager.Instance.History.CardDrawn(combatState, card, fromHandDraw);
				await Hook.AfterCardDrawn(combatState, choiceContext, card, fromHandDraw);
				card.InvokeDrawn();
				NDebugAudioManager.Instance?.Play("card_deal.mp3", 0.25f, PitchVariance.Small);
			});
		if (drawn.Count == 0 && !CanDrawAnyCards(player))
		{
			ShowDrawFailureThoughtBubble(player);
		}

		return drawn;
	}

	/// <summary>
	/// 按轮选牌并逐张抽入，直到抽满 <paramref name="requested"/> 张或无法继续。
	/// 抽入某张牌时可能嵌套触发别的抽牌（如升级自动化），把同一轮里尚未抽入的已选牌提前抽走；
	/// 这些牌已经按抽牌结算过，这里跳过且不计入本次额度，下一轮从剩余抽牌堆补选差额。
	/// 一轮没有抽入任何牌（选择为空、手牌已满、牌堆耗尽）即停止。
	/// </summary>
	internal static async Task<int> DrawSelectedRounds(
		int requested,
		Func<int, Task<IReadOnlyList<CardModel>>> selectRound,
		Func<CardModel, bool> isStillInDrawPile,
		Func<bool> canDrawMore,
		Func<CardModel, Task> drawOne)
	{
		int remaining = requested;
		while (remaining > 0 && canDrawMore())
		{
			IReadOnlyList<CardModel> selected = await selectRound(remaining);
			int drawnThisRound = 0;
			foreach (CardModel card in selected)
			{
				if (remaining <= 0 || !canDrawMore())
				{
					break;
				}

				if (!isStillInDrawPile(card))
				{
					continue;
				}

				await drawOne(card);
				remaining--;
				drawnThisRound++;
			}

			if (drawnThisRound == 0)
			{
				break;
			}
		}

		return requested - remaining;
	}

	private static async Task ShuffleIntoDrawPileIfShort(PlayerChoiceContext choiceContext, Player player, int requestedDraws)
	{
		CardPile drawPile = PileType.Draw.GetPile(player);
		CardPile discardPile = PileType.Discard.GetPile(player);
		if (drawPile.Cards.Count < requestedDraws && discardPile.Cards.Any())
		{
			await CardPileCmd.Shuffle(choiceContext, player);
		}
	}

	private static bool CanDrawAnyCards(Player player)
	{
		return PileType.Draw.GetPile(player).Cards.Count + PileType.Discard.GetPile(player).Cards.Count > 0
			&& PileType.Hand.GetPile(player).Cards.Count < CardPile.MaxCardsInHand;
	}

	private static void ShowDrawFailureThoughtBubble(Player player)
	{
		string key = PileType.Hand.GetPile(player).Cards.Count >= CardPile.MaxCardsInHand
			? "HAND_FULL"
			: "NO_DRAW";
		ThinkCmd.Play(new LocString("combat_messages", key), player.Creature, 2.0);
	}
}
