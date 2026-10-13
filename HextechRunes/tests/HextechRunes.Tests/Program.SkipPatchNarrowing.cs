using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes.Tests;

internal static partial class Program
{
	// 跳过型补丁收窄：卡牌奖励备选项只在持有浮木或佩尔之翼时接管；升级自动化只在原版本次会触发时追加抽牌。
	[HextechTest]
	private static void SkipPatchNarrowingKeepsVanillaPathByDefault()
	{
		using IDisposable models = InjectMissingModels(typeof(Driftwood), typeof(PaelsWing), typeof(Vajra));
		RelicModel Relic<T>() where T : RelicModel => ModelDb.GetById<RelicModel>(ModelDb.GetId<T>()).ToMutable();

		Expect(!HextechRewardSafetyHooks.NeedsCardRewardAlternativesTakeover(Array.Empty<RelicModel>()), "no relics: vanilla card reward alternatives");
		Expect(!HextechRewardSafetyHooks.NeedsCardRewardAlternativesTakeover([ Relic<Vajra>() ]), "unrelated relics: vanilla");
		Expect(HextechRewardSafetyHooks.NeedsCardRewardAlternativesTakeover([ Relic<Driftwood>() ]), "driftwood rerolls are counted by the takeover");
		Expect(HextechRewardSafetyHooks.NeedsCardRewardAlternativesTakeover([ Relic<PaelsWing>() ]), "Pael's Wing can exceed the vanilla limit");

		Expect(AutomationUpgradeRune.WillTrigger(1), "the draw that takes the counter from 1 to 0 triggers");
		Expect(AutomationUpgradeRune.WillTrigger(0), "an already exhausted counter triggers");
		Expect(!AutomationUpgradeRune.WillTrigger(2), "counter above 1 does not trigger yet");
	}
}
