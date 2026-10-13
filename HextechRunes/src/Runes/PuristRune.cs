namespace HextechRunes;

/// <summary>
/// 纯粹主义者：回合内触发的抽牌改为获得等量能量（按本次请求的张数，回合开始的手牌抽取不算）。
/// 我方遗物在原版监听顺序里排在 Mayhem 之前，所以先于敌方我细看/转出胜利拦下这次抽牌；
/// 换来的能量仍是回合内获得的能量，敌方转出胜利会把它推迟到下回合。
/// </summary>
public sealed class PuristRune : HextechRelicBase
{
	// ShouldDraw 是查询钩子；原版拦下抽牌后同步紧接着调本模型的 AfterPreventingDraw，中间没有等待。
	// 每次查询都重写，所以未被采用的查询留下的值不会被用到。不保存。
	private int _pendingEnergy;

	public override bool ShouldDraw(Player player, bool fromHandDraw)
	{
		_pendingEnergy = player == Owner && !fromHandDraw && HextechTurnPhase.IsDuringOwnTurn(player)
			? HextechDrawRequest.GetRequestedCount(player)
			: 0;
		return _pendingEnergy <= 0;
	}

	public override async Task AfterPreventingDraw()
	{
		int energy = _pendingEnergy;
		_pendingEnergy = 0;
		if (energy <= 0 || Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await PlayerCmd.GainEnergy(energy, Owner);
	}
}
