using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace HextechRunes.Tests;

internal static partial class Program
{
	// RitsuLib 等前置在外部模组调用注册接口之后才把条目改成带模组前缀的形式：注册时算出的 ModelId 不能当作查询键，
	// ModelDb 收录内容之前建的索引也不能长期缓存，否则所属角色、可用性、池标签、资源归属都查不到。
	[HextechTest]
	private static void ExternalMetadataFollowsModelIdAssignedAfterRegistration()
	{
		Type runeType = typeof(GuaranteedTestSilverRuneA);
		bool lateEntryAssigned = false;
		bool contentRegistered = false;
		ModelId registrationId = ModelDb.GetId(runeType);
		ModelId lateId = new(registrationId.Category, "LATE_MOD_RELIC_GUARANTEED_TEST_SILVER_RUNE_A");
		Func<Type, ModelId> originalResolver = HextechExternalContentRegistry.ModelIdResolver;
		Action[] restore =
		[
			CaptureStaticCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRuneRegistrations"),
			CaptureStaticCollectionRestore(typeof(HextechExternalContentRegistry), "AssetModIdsByType"),
			CaptureStaticCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRuneAvailabilityByType"),
			CaptureStaticCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRuneCharacterTypesByType"),
			CaptureStaticCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRunePoolLabelKeysByType"),
			CaptureExternalRegistryVersionRestore()
		];
		try
		{
			HextechModelDbState.ContentRegisteredOverride = () => contentRegistered;
			HextechExternalContentRegistry.ModelIdResolver = type => lateEntryAssigned && type == runeType ? lateId : originalResolver(type);

			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(runeType, HextechRarityTier.Silver),
				"Compatibility.LateEntry",
				static _ => true);
			HextechExternalContentRegistry.SetPlayerRuneCharacter(runeType, typeof(Silent));
			HextechExternalContentRegistry.SetPlayerRunePoolLabel(runeType, "LATE_POOL");
			Equal("LATE_POOL", HextechExternalContentRegistry.GetPlayerRunePoolLabel(registrationId), "lookups work with the entry at registration time");

			// 前置登记内容、改写条目，随后 ModelDb 收录内容。
			lateEntryAssigned = true;
			contentRegistered = true;
			ModelId silent = ModelDb.GetId<Silent>();
			Equal(silent, HextechExternalContentRegistry.GetPlayerRuneCharacterId(lateId), "character ownership follows the final entry");
			Equal("LATE_POOL", HextechExternalContentRegistry.GetPlayerRunePoolLabel(lateId), "pool label follows the final entry");
			Equal("Compatibility.LateEntry", HextechExternalContentRegistry.GetAssetModId(lateId), "asset owner follows the final entry");
			Expect(HextechExternalContentRegistry.GetPlayerRuneAvailability(lateId) != null, "availability follows the final entry");
			Expect(HextechExternalContentRegistry.HasPlayerRunesForCharacter(silent), "the character still has external runes");
			Equal(null, HextechExternalContentRegistry.GetPlayerRuneCharacterId(registrationId), "the stale registration-time entry no longer matches");
		}
		finally
		{
			HextechModelDbState.ContentRegisteredOverride = null;
			HextechExternalContentRegistry.ModelIdResolver = originalResolver;
			foreach (Action action in restore.Reverse())
			{
				action();
			}
		}
	}
}
