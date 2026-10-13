using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HextechRunes.Tests;

internal static partial class Program
{
	// 纯粹主义者/敌方转出胜利按"本次抽牌请求的张数"转换：请求只在 Draw 同步期间对同一名玩家可见。
	[HextechTest]
	private static void DrawRequestIsScopedToTheDrawingPlayer()
	{
		Player drawing = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
		Player other = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));

		Equal(0, HextechDrawRequest.GetRequestedCount(drawing), "no request outside a draw");
		HextechDrawRequest.Begin(drawing, 2.5m);
		Equal(3, HextechDrawRequest.GetRequestedCount(drawing), "fractional requests round up like vanilla");
		Equal(0, HextechDrawRequest.GetRequestedCount(other), "another player's query sees no request");
		HextechDrawRequest.End();
		Equal(0, HextechDrawRequest.GetRequestedCount(drawing), "request ends with the draw call");

		Equal(HextechRarityTier.Prismatic, HextechMonsterHexRegistry.Registrations.Single(row => row.Kind == MonsterHexKind.SpinToWin).Rarity, "enemy Spin to Win is prismatic");
		SequenceEqual(
			[ typeof(DrawCardsNextTurnPower), typeof(EnergyNextTurnPower) ],
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.SpinToWin),
			"enemy Spin to Win explains next-turn draw and energy");
	}
}
