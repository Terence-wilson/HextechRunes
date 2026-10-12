using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace HextechRunes.Tests;

internal static partial class Program
{
	[HextechTest]
	private static void GuaranteedFirstPickPlacementAndRerollRarity()
	{
		Action[] restore =
		[
			CaptureStaticCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRuneRegistrations"),
			CaptureStaticCollectionRestore(typeof(HextechExternalContentRegistry), "AssetModIdsByModelId"),
			CaptureExternalRegistryVersionRestore()
		];
		try
		{
			Equal(PlayerRuneFlags.GuaranteedFirstPick, HextechRunesInterop.ParsePlayerRuneFlags(" guaranteedfirstpick "), "flag is parsed by name");
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(GuaranteedTestSilverRuneA), HextechRarityTier.Silver),
				"Compatibility.Guaranteed");
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(GuaranteedTestSilverRuneB), HextechRarityTier.Silver),
				"Compatibility.Guaranteed");
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(GuaranteedTestSilverRuneC), HextechRarityTier.Silver),
				"Compatibility.Guaranteed");
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(GuaranteedTestPrismaticRune), HextechRarityTier.Prismatic, PlayerRuneFlags.GuaranteedFirstPick),
				"Compatibility.Guaranteed");
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(GuaranteedTestGoldRune), HextechRarityTier.Gold, PlayerRuneFlags.GuaranteedFirstPick),
				"Compatibility.Guaranteed");

			RelicModel a = new GuaranteedTestSilverRuneA();
			RelicModel b = new GuaranteedTestSilverRuneB();
			RelicModel c = new GuaranteedTestSilverRuneC();
			RelicModel prismatic = new GuaranteedTestPrismaticRune();
			RelicModel gold = new GuaranteedTestGoldRune();
			Expect(HextechRunePoolBuilder.IsGuaranteedFirstPickRune(prismatic), "flagged rune is recognised");
			Expect(!HextechRunePoolBuilder.IsGuaranteedFirstPickRune(a), "unflagged rune is not guaranteed");

			SequenceEqual(new[] { a, b, prismatic }, HextechRunePoolBuilder.PlaceGuaranteedOption([a, b, c], prismatic), "full selection replaces the last slot");
			SequenceEqual(new[] { a, prismatic }, HextechRunePoolBuilder.PlaceGuaranteedOption([a], prismatic), "short selection appends");
			Equal(HextechRarityTier.Silver, HextechRunePoolBuilder.GetRarityForOptions(HextechRunePoolBuilder.PlaceGuaranteedOption([a, b, c], prismatic)),
				"selection rarity still comes from the act's first option");

			List<RelicModel> withGuaranteed = [a, b, prismatic];
			Expect(ReferenceEquals(withGuaranteed, HextechRunePoolBuilder.ApplyGuaranteedFirstPick(null!, null!, 0, null, withGuaranteed)),
				"a selection that already holds a guaranteed rune is left untouched");

			Equal(HextechRarityTier.Silver, HextechRunePoolBuilder.GetRerollRarity([a, b, prismatic], 2), "rerolling the guaranteed slot returns to the act rarity");
			Equal(HextechRarityTier.Silver, HextechRunePoolBuilder.GetRerollRarity([a, b, prismatic], 0), "regular slots keep their own rarity");
			Equal(HextechRarityTier.Prismatic, HextechRunePoolBuilder.GetRerollRarity([prismatic], 0), "a lone guaranteed option keeps its own rarity");
			Equal(HextechRarityTier.Silver, HextechRunePoolBuilder.GetRerollRarity([gold, a, prismatic], 2), "other guaranteed runes are skipped when finding the act rarity");

			HashSet<ModelId> seen = [prismatic.CanonicalId()];
			SequenceEqual(new[] { gold }, HextechRunePoolBuilder.SelectGuaranteedCandidates([prismatic, gold], seen, upgradeAlreadySelected: false), "unseen guaranteed runes are preferred");
			seen.Add(gold.CanonicalId());
			SequenceEqual(new[] { prismatic, gold }, HextechRunePoolBuilder.SelectGuaranteedCandidates([prismatic, gold], seen, upgradeAlreadySelected: false), "all seen falls back to the full set");
			Equal(0, HextechRunePoolBuilder.SelectGuaranteedCandidates([], seen, upgradeAlreadySelected: false).Count, "no available guaranteed rune skips the guarantee");
			RelicModel upgrade = new GuaranteedTestUpgradeRune();
			SequenceEqual(new[] { gold }, HextechRunePoolBuilder.SelectGuaranteedCandidates([upgrade, gold], null, upgradeAlreadySelected: true),
				"the one-upgrade-per-selection rule still applies");
		}
		finally
		{
			foreach (Action action in restore.Reverse())
			{
				action();
			}
		}
	}

	[HextechTest]
	private static void ExternalRuneCharacterGetsCharacterWeight()
	{
		Action[] restore =
		[
			CaptureStaticCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRuneRegistrations"),
			CaptureStaticCollectionRestore(typeof(HextechExternalContentRegistry), "AssetModIdsByModelId"),
			CaptureStaticCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRuneCharacterIdsByModelId"),
			CaptureExternalRegistryVersionRestore(),
			SuppressCompatibilityWarnings("external-content.rune-character-conflict")
		];
		try
		{
			Equal(2, HextechRunesInterop.ApiVersion, "guaranteed pick and rune character raise the contract version");
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(GuaranteedTestSilverRuneA), HextechRarityTier.Silver),
				"Compatibility.Guaranteed");
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(GuaranteedTestSilverRuneB), HextechRarityTier.Silver),
				"Compatibility.Guaranteed");
			ModelId silent = ModelDb.GetId<Silent>();
			ModelId ironclad = ModelDb.GetId<Ironclad>();
			Expect(!HextechExternalContentRegistry.HasPlayerRunesForCharacter(silent), "no external character runes before registration");

			int version = HextechExternalContentRegistry.Version;
			HextechRunesInterop.SetPlayerRuneCharacter(typeof(GuaranteedTestSilverRuneA), typeof(Silent));
			Expect(HextechExternalContentRegistry.Version > version, "character ownership changes weights, so it bumps the registry version");
			HextechRunesInterop.SetPlayerRuneCharacter(typeof(GuaranteedTestSilverRuneA), typeof(Ironclad));
			Expect(HextechExternalContentRegistry.HasPlayerRunesForCharacter(silent), "registered character has character runes");
			Expect(!HextechExternalContentRegistry.HasPlayerRunesForCharacter(ironclad), "first character registration wins");

			RelicModel owned = new GuaranteedTestSilverRuneA();
			RelicModel generic = new GuaranteedTestSilverRuneB();
			Expect(HextechRunePoolBuilder.IsRuneForCharacter(owned, silent), "external rune counts as the registered character's rune");
			Expect(!HextechRunePoolBuilder.IsRuneForCharacter(owned, ironclad), "other characters do not match");
			Expect(!HextechRunePoolBuilder.IsRuneForCharacter(generic, silent), "unmapped external rune stays generic");
			Expect(HextechRunePoolBuilder.IsRuneForCharacter(new BerserkRune(), ironclad), "vanilla character pools still match by character id");
			List<int> weights = HextechRunePoolBuilder.BuildSelectionWeights([owned, generic], new Dictionary<string, int>(), false, silent,
				HextechWeightedRuneOptions.InitialCharacterWeightPercent, out _);
			SequenceEqual(new[] { 15000, 10000 }, weights, "mapped external rune receives the 150% character weight");

			ExpectThrows<ArgumentException>(
				() => HextechRunesInterop.SetPlayerRuneCharacter(typeof(GuaranteedTestSilverRuneB), typeof(string)),
				"character type must be a CharacterModel");
			ExpectThrows<ArgumentException>(
				() => HextechRunesInterop.SetPlayerRuneCharacter(typeof(string), typeof(Silent)),
				"rune type must be a RelicModel");
			Equal(null, HextechExternalContentRegistry.GetPlayerRuneCharacterId(generic.CanonicalId()), "invalid registrations leave no state");
		}
		finally
		{
			foreach (Action action in restore.Reverse())
			{
				action();
			}
		}
	}

	private sealed class GuaranteedTestSilverRuneA : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Starter;
	}

	private sealed class GuaranteedTestSilverRuneB : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Starter;
	}

	private sealed class GuaranteedTestSilverRuneC : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Starter;
	}

	private sealed class GuaranteedTestPrismaticRune : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Starter;
	}

	private sealed class GuaranteedTestGoldRune : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Starter;
	}

	private sealed class GuaranteedTestUpgradeRune : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Starter;
	}
}
