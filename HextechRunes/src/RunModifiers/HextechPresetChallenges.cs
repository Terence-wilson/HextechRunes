using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

// 已退役的预设挑战：不再出现在自定义模式列表里，也不再改变选择流程。只保留模型注册，
// 避免旧局（含进行中存档与历史记录）里的 modifier ID 无法解析；读入的旧挑战局按普通海克斯大乱斗继续。

public sealed class StuffedToRuinChallengeModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_STUFFED_TO_RUIN_CHALLENGE.title");

	public override LocString Description => new("modifiers", "HEXTECH_STUFFED_TO_RUIN_CHALLENGE.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/stuffedToRuinChallenge.png";
}

public sealed class DefenseCounterMasterChallengeModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_DEFENSE_COUNTER_MASTER_CHALLENGE.title");

	public override LocString Description => new("modifiers", "HEXTECH_DEFENSE_COUNTER_MASTER_CHALLENGE.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/defenseCounterMasterChallenge.png";
}

public sealed class BruteForceChallengeModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_BRUTE_FORCE_CHALLENGE.title");

	public override LocString Description => new("modifiers", "HEXTECH_BRUTE_FORCE_CHALLENGE.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/bruteForceChallenge.png";
}

public sealed class EightPennyGateChallengeModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_EIGHT_PENNY_GATE_CHALLENGE.title");

	public override LocString Description => new("modifiers", "HEXTECH_EIGHT_PENNY_GATE_CHALLENGE.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/eightPennyGateChallenge.png";
}

public sealed class ListlessChallengeModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_LISTLESS_CHALLENGE.title");

	public override LocString Description => new("modifiers", "HEXTECH_LISTLESS_CHALLENGE.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/listlessChallenge.png";
}
