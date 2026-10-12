using System.Reflection;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunes.Tests;

internal static partial class Program
{
	// 预设挑战已退役：旧局（进行中存档、历史记录）按保存的修饰 ID 解析模型，五个模型必须保留注册、ID 不变；
	// 向自定义模式列表追加挑战的补丁不能再装上。
	[HextechTest]
	private static void RetiredPresetChallengesKeepModelIdsWithoutListPatches()
	{
		(Type Type, string Entry)[] retired =
		[
			(typeof(StuffedToRuinChallengeModifier), "STUFFED_TO_RUIN_CHALLENGE_MODIFIER"),
			(typeof(DefenseCounterMasterChallengeModifier), "DEFENSE_COUNTER_MASTER_CHALLENGE_MODIFIER"),
			(typeof(BruteForceChallengeModifier), "BRUTE_FORCE_CHALLENGE_MODIFIER"),
			(typeof(EightPennyGateChallengeModifier), "EIGHT_PENNY_GATE_CHALLENGE_MODIFIER"),
			(typeof(ListlessChallengeModifier), "LISTLESS_CHALLENGE_MODIFIER")
		];
		SequenceEqual(retired.Select(static item => item.Type), HextechCustomModelRegistry.CustomChallengeModifierTypes, "retired challenge models stay registered in order");
		foreach ((Type type, string entry) in retired)
		{
			Expect(HextechCustomModelRegistry.AllCustomModifierTypes.Contains(type), $"{type.Name} stays in saved-property model registration");
			Equal(entry, ModelDb.GetId(type).Entry, $"{type.Name} keeps its saved modifier id");
		}

		string[] challengePatchIds = typeof(HextechPatchAttribute).Assembly.GetTypes()
			.Select(static type => type.GetCustomAttribute<HextechPatchAttribute>()?.Id)
			.OfType<string>()
			.Where(static id => id.StartsWith("custom-run.preset-challenges", StringComparison.Ordinal))
			.ToArray();
		Equal(0, challengePatchIds.Length, "preset challenge list patches are no longer installed");
	}
}
