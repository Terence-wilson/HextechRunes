namespace HextechRunes;

internal sealed class IInspectEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.IInspect;

	// 只读：抽牌查询可能在真实抽牌之外被调用，已阻止次数在 AfterPreventingDraw 里记。
	internal override bool ShouldDraw(HextechEnemyHexContext context, Player player, bool fromHandDraw)
	{
		return !IsActiveFor(context, player)
			|| !ShouldPreventExtraDraw(context.Tracking, player.NetId, Limit(context), fromHandDraw);
	}

	internal override Task<bool> AfterPreventingDraw(HextechEnemyHexContext context, Player player, bool fromHandDraw, int requestedCount)
	{
		if (!IsActiveFor(context, player)
			|| !ShouldPreventExtraDraw(context.Tracking, player.NetId, Limit(context), fromHandDraw))
		{
			return Task.FromResult(false);
		}

		RecordPreventedExtraDraw(context.Tracking, player.NetId);
		return Task.FromResult(true);
	}

	internal static bool ShouldPreventExtraDraw(
		HextechMayhemCombatTrackingState tracking,
		ulong playerId,
		int limit,
		bool fromHandDraw)
	{
		return !fromHandDraw
			&& !tracking.IsPlayerTurnStart(playerId)
			&& limit > 0
			&& tracking.InspectExtraDrawsPreventedThisTurn.GetValueOrDefault(playerId) < limit;
	}

	internal static void RecordPreventedExtraDraw(HextechMayhemCombatTrackingState tracking, ulong playerId)
	{
		tracking.InspectExtraDrawsPreventedThisTurn[playerId] = tracking.InspectExtraDrawsPreventedThisTurn.GetValueOrDefault(playerId) + 1;
	}

	private static bool IsActiveFor(HextechEnemyHexContext context, Player player)
	{
		return player.Creature.Side == CombatSide.Player
			&& !player.Creature.IsDead
			&& player.Creature.CombatState?.RunState == context.RunState;
	}

	private int Limit(HextechEnemyHexContext context)
	{
		return context.TierValue(Kind, 0, 1, 2);
	}
}
