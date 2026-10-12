using static HextechRunes.HextechRunePoolBuilder;
using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	/// <summary>按候选与权重给出本次重掷选中的下标;单机与联机只在这一步的随机来源不同。</summary>
	private delegate int RerollIndexRoller(IReadOnlyList<RelicModel> pool, IReadOnlyList<int> weights, int totalWeight, HextechRarityTier rarity);

	private static IReadOnlyList<RelicModel> RerollSingleOptionAndTrack(
		HextechMayhemModifier modifier,
		Player player,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex,
		HashSet<ModelId> seenOptionIds,
		HextechRarityTier? stageRarity,
		HextechRarityTier? rarityOverride = null,
		int chaosRerollOrdinal = 0)
	{
		RunState runState = (RunState)player.RunState;
		// 单机:候选按池原顺序,推进原版 Niche 随机流。
		IReadOnlyList<RelicModel> rerolled = RerollSingleOption(
			player,
			runState,
			currentOptions,
			slotIndex,
			seenOptionIds,
			modifier.IsEndlessLoopActive,
			stageRarity,
			rarityOverride,
			sortCandidates: false,
			(_, weights, totalWeight, _) => SelectWeightedIndex(weights, runState.Rng.Niche.NextInt(totalWeight)),
			transformStageIndex: -1,
			transformRerollOrdinal: chaosRerollOrdinal);
		if (!ReferenceEquals(rerolled, currentOptions))
		{
			ModelId rerolledId = rerolled[slotIndex].CanonicalId();
			seenOptionIds.Add(rerolledId);
			MarkRelicsSeen([ rerolled[slotIndex] ]);
			modifier.RecordSeenPlayerRunes(player, [ rerolled[slotIndex] ]);
		}

		return rerolled;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOptionAndTrackMultiplayer(
		HextechMayhemModifier modifier,
		Player player,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex,
		int selectionStageIndex,
		int rerollOrdinal,
		HashSet<ModelId> seenOptionIds,
		HashSet<ModelId> offeredOptionIds,
		HextechRarityTier? stageRarity,
		HextechRarityTier? rarityOverride = null)
	{
		RunState runState = (RunState)player.RunState;
		// 联机:候选按 ModelId 排序后用各端一致的稳定哈希抽取,不消耗共享 RNG。
		IReadOnlyList<RelicModel> rerolled = RerollSingleOption(
			player,
			runState,
			currentOptions,
			slotIndex,
			seenOptionIds,
			modifier.IsEndlessLoopActive,
			stageRarity,
			rarityOverride,
			sortCandidates: true,
			(pool, weights, totalWeight, rarity) => GetMultiplayerRerollIndex(player, pool, weights, totalWeight, rarity, slotIndex, selectionStageIndex, rerollOrdinal),
			transformStageIndex: selectionStageIndex,
			transformRerollOrdinal: rerollOrdinal);
		if (!ReferenceEquals(rerolled, currentOptions))
		{
			ModelId rerolledId = rerolled[slotIndex].CanonicalId();
			// 重随排除集合与完整候选历史分别记录;选定后同步历史,并由两端统一写入存档。
			seenOptionIds.Add(rerolledId);
			offeredOptionIds.Add(rerolledId);
			MarkRelicsSeen([ rerolled[slotIndex] ]);
			HextechLog.Info("Mayhem", $"RerollSingleOptionMultiplayer: player={player.NetId} slot={slotIndex} ordinal={rerollOrdinal} relic={rerolledId.Entry}");
		}

		return rerolled;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOption(
		Player player,
		RunState runState,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex,
		HashSet<ModelId> seenOptionIds,
		bool useEndlessTagWindow,
		HextechRarityTier? stageRarity,
		HextechRarityTier? rarityOverride,
		bool sortCandidates,
		RerollIndexRoller rollIndex,
		int transformStageIndex,
		int transformRerollOrdinal)
	{
		if (slotIndex < 0 || slotIndex >= currentOptions.Count)
		{
			return currentOptions;
		}

		HashSet<ModelId> currentOptionIds = currentOptions
			.Select(static relic => relic.CanonicalId())
			.ToHashSet();
		HashSet<ModelId> excludedIds = new(currentOptionIds);
		excludedIds.UnionWith(seenOptionIds);
		HextechRarityTier rarity = rarityOverride ?? GetRerollRarity(currentOptions, slotIndex, stageRarity);
		List<RelicModel> candidates = BuildRerollCandidates(player, rarity, runState, excludedIds, currentOptions, slotIndex, sortCandidates);
		if (candidates.Count == 0 && seenOptionIds.Count > 0)
		{
			// 池被「已见」清空:重置(清空)已见集,让重随能重新刷到此前见过的符文(仍排除当前选项)。
			seenOptionIds.Clear();
			candidates = BuildRerollCandidates(player, rarity, runState, currentOptionIds, currentOptions, slotIndex, sortCandidates);
		}

		if (candidates.Count == 0)
		{
			return currentOptions;
		}

		int characterWeight = HextechWeightedRuneOptions.GetWeight(currentOptions);
		Dictionary<string, int> tagCounts = BuildOwnedRuneTagCounts(player, useEndlessTagWindow);
		List<int> weights = BuildSelectionWeights(candidates, tagCounts, useEndlessTagWindow, GetRuneCharacterId(player), characterWeight, out int totalWeight);
		int selectedIndex = rollIndex(candidates, weights, totalWeight, rarity);
		List<RelicModel> updated = currentOptions.ToList();
		updated[slotIndex] = CreateSelectableRuneOption(player, candidates[selectedIndex]);
		return new HextechWeightedRuneOptions(
			HextechRuneGeneration.Transform(player, rarity, runState, transformStageIndex, updated, slotIndex, transformRerollOrdinal),
			AdvanceCharacterWeight(player, characterWeight, candidates[selectedIndex]));
	}

	private static List<RelicModel> BuildRerollCandidates(
		Player player,
		HextechRarityTier rarity,
		RunState runState,
		IReadOnlySet<ModelId> excludedIds,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex,
		bool sortCandidates)
	{
		bool upgradeAlreadyPresent = currentOptions
			.Where((_, index) => index != slotIndex)
			.Any(IsUpgradeRune);
		List<RelicModel> candidates = ConstrainCandidates(
			BuildSelectableRunePool(player, rarity, runState, excludedIds),
			upgradeAlreadyPresent);
		return sortCandidates
			? candidates.OrderBy(static relic => relic.CanonicalId().Entry, StringComparer.Ordinal).ToList()
			: candidates;
	}

	private static int GetMultiplayerRerollIndex(
		Player player,
		IReadOnlyList<RelicModel> pool,
		IReadOnlyList<int> weights,
		int totalWeight,
		HextechRarityTier rarity,
		int slotIndex,
		int selectionStageIndex,
		int rerollOrdinal)
	{
		RunState runState = (RunState)player.RunState;
		List<string> parts =
		[
			runState.Rng.StringSeed,
			"|act:",
			selectionStageIndex.ToString(),
			"|player:",
			HextechStableRandom.PlayerKey(player),
			"|rarity:",
			((int)rarity).ToString(),
			"|slot:",
			slotIndex.ToString(),
			"|ordinal:",
			rerollOrdinal.ToString()
		];
		for (int i = 0; i < pool.Count; i++)
		{
			parts.Add("|pool:");
			parts.Add(pool[i].CanonicalId().Entry);
			parts.Add(":");
			parts.Add(weights[i].ToString());
		}

		int roll = HextechStableRandom.IndexFromRawParts(totalWeight, parts.ToArray());
		return SelectWeightedIndex(weights, roll);
	}
}
