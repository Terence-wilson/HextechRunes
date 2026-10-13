using MegaCrit.Sts2.Core.Context;

namespace HextechRunes;

/// <summary>
/// 嬗变系列：拾取时消耗自身，从候选池随机获得指定数量的海克斯。
/// </summary>
public abstract class TransmuteRuneBase : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	protected abstract IEnumerable<Type> CandidateTypes { get; }

	protected virtual int ObtainCount => 1;

	public override async Task AfterObtained()
	{
		Flash();
		await HextechRuneGrantHelper.ConsumeAndObtainRandomRunes(
			this,
			Owner,
			CandidateTypes,
			ObtainCount,
			LocalContext.IsMe(Owner) ? obtainedIds => HextechTransmuteNotice.ShowPlayer(this, obtainedIds) : null);
	}
}
