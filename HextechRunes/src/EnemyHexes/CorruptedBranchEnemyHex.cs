namespace HextechRunes;

internal sealed class CorruptedBranchEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.CorruptedBranch;

	internal override async Task AfterCardExhausted(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
	{
		Player? owner = card.Owner;
		if (owner?.Creature.Side != CombatSide.Player || owner.Creature.IsDead
			|| owner.Creature.CombatState is not HextechCombatState combatState
			|| combatState.RunState != context.RunState)
		{
			return;
		}

		int ordinal = HextechCombatProcTracker.ConsumePlayerRuneProcInCombat(context.Tracking, owner, nameof(CorruptedBranchEnemyHex));
		int index = HextechStableRandom.Index(context.RunState, HextechEnemyStatusCards.Count,
			"enemy-corrupted-branch-status", HextechStableRandom.PlayerKey(owner), ordinal.ToString());
		await HextechCardGeneration.AddGeneratedCardToCombat(
			HextechEnemyStatusCards.Create(combatState, owner, index), PileType.Discard,
			addedByPlayer: false, CardPilePosition.Random);
	}
}
