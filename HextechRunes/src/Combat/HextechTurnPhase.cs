namespace HextechRunes;

/// <summary>
/// "回合内"的统一口径：玩家侧回合、且该玩家已进入出牌阶段（回合开始的各类结算已经结束）。
/// 回合开始时的手牌抽取、下回合抽牌/能量的兑现都不算回合内，避免被再次推迟或转换。
/// </summary>
internal static class HextechTurnPhase
{
	internal static bool IsDuringOwnTurn(Player player)
	{
		if (player.Creature.IsDead
			|| player.Creature.CombatState is not { } combatState
			|| combatState.CurrentSide != CombatSide.Player
			|| CombatManager.Instance?.IsInProgress != true)
		{
			return false;
		}

		return HextechMayhemModifier.FindIn(combatState.RunState)?.CombatTracking.IsPlayerTurnStart(player.NetId) != true;
	}
}
