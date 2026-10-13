namespace HextechRunes;

/// <summary>
/// 原版 ShouldDraw 只告诉监听者"要不要抽"，不带张数。纯粹主义者与敌方转出胜利要把整次抽牌按张数换成
/// 能量或下回合抽牌，所以在 CardPileCmd.Draw（以及本模组的选择抽牌）同步执行 ShouldDraw 的期间记下本次请求。
/// 只在这段同步调用里有效：其他模组在抽牌之外模拟查询 ShouldDraw 时读到 0，不会被拦截。
/// </summary>
internal static class HextechDrawRequest
{
	private static Player? _player;
	private static int _count;

	internal static int GetRequestedCount(Player player)
	{
		return ReferenceEquals(_player, player) ? _count : 0;
	}

	internal static void Begin(Player player, decimal count)
	{
		_player = player;
		_count = count > 0m ? (int)Math.Ceiling(count) : 0;
	}

	internal static void End()
	{
		_player = null;
		_count = 0;
	}
}
