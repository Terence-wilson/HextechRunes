namespace HextechRunes;

public abstract class AttributeConversionRelicBase : HextechRelicBase
{
	private bool _isConverting;
	private PowerModel? _armedPower;
	private decimal? _pendingAmount;
	private Creature? _pendingApplier;

	/// <summary>
	/// 是否转换这种属性。同时用于施加前的规范 Power（TryModifyPowerAmountReceived）与已施加的实例（AfterPowerAmountChanged）。
	/// </summary>
	protected abstract bool ShouldConvert(PowerModel power);

	protected abstract Task ApplyConvertedPower(Creature owner, decimal amount, Creature? applier, CardModel? cardSource);

	protected abstract Task RevertOriginalPower(Creature owner, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource);

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_armedPower = null;
		_pendingAmount = null;
		_pendingApplier = null;
		_isConverting = false;
		return Task.CompletedTask;
	}

	// 原版施加管线（PowerCmd.Apply / ModifyAmount）先调 BeforePowerAmountChanged 再问接收修正，传的是同一个能力实例；
	// 随机数预测等模组只模拟查询钩子、不调本模组的 Before 钩子。在这里登记真实施加的实例，查询钩子只认登记过的那一次。
	public override Task BeforePowerAmountChanged(PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource)
	{
		if (!_isConverting && target == Owner.Creature && amount != 0m && ShouldConvert(power))
		{
			_armedPower = power;
		}

		return Task.CompletedTask;
	}

	public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
	{
		modifiedAmount = amount;
		if (_isConverting || target != Owner.Creature || amount == 0m || !ShouldConvert(canonicalPower))
		{
			return false;
		}

		// 施加管线结束后再把原属性换成转换后的属性；这条路径拿不到来源卡牌。
		// 查询钩子会在真实施加之外被调用（悬停预览、回合结束预测），而原版在问完修正后、After 回调前还有一段等待：
		// 只有登记过的这次施加记待转换量，并立刻撤销登记，等待期间的模拟调用改写不了它。
		if (ReferenceEquals(_armedPower, canonicalPower))
		{
			_armedPower = null;
			_pendingAmount = amount;
			_pendingApplier = applier;
		}

		modifiedAmount = 0m;
		return true;
	}

	public override async Task AfterModifyingPowerAmountReceived(PowerModel power)
	{
		if (_pendingAmount is not decimal amount)
		{
			return;
		}

		Creature? applier = _pendingApplier;
		_pendingAmount = null;
		_pendingApplier = null;
		_isConverting = true;
		try
		{
			Flash();
			await ApplyConvertedPower(Owner.Creature, amount, applier, null);
		}
		finally
		{
			_isConverting = false;
		}
	}

	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (_isConverting || amount == 0m || power.Owner != Owner.Creature || !ShouldConvert(power))
		{
			return;
		}

		_isConverting = true;
		try
		{
			Flash();
			await RevertOriginalPower(Owner.Creature, power, amount, applier, cardSource);
			await ApplyConvertedPower(Owner.Creature, amount, applier, cardSource);
		}
		finally
		{
			_isConverting = false;
		}
	}
}
