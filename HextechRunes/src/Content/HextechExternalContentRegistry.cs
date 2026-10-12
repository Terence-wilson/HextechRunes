namespace HextechRunes;

internal static class HextechExternalContentRegistry
{
	internal const string SponsorPackModId = "HextechRunesSponsorPack";

	private static readonly object SyncRoot = new();
	private static readonly List<PlayerRuneRegistration> PlayerRuneRegistrations = new();
	private static readonly List<ForgeRegistration> ForgeRegistrations = new();
	private static readonly List<Type> EventRelicTypes = new();
	private static readonly Dictionary<ModelId, string> AssetModIdsByModelId = new();
	private static readonly Dictionary<ModelId, string> EnchantmentIconPathsByModelId = new();
	private static readonly Dictionary<ModelId, Func<Player, bool>> PlayerRuneAvailabilityByModelId = new();
	// 符文 → 所属角色（CharacterModel 的 ModelId），给原版角色池以外的角色用；进角色加权计算，改动递增 _version。
	private static readonly Dictionary<ModelId, ModelId> PlayerRuneCharacterIdsByModelId = new();
	// 以下两项只影响界面文字，不进候选池计算，改动不递增 _version。
	private static readonly Dictionary<ModelId, string> PlayerRunePoolLabelKeysByModelId = new();
	private static readonly Dictionary<string, string> ConfigSectionTitleKeysByAssetModId = new(StringComparer.Ordinal);
	private static int _version;

	internal static int Version
	{
		get
		{
			lock (SyncRoot)
			{
				return _version;
			}
		}
	}

	internal static void RegisterPlayerRune(
		PlayerRuneRegistration registration,
		string? assetModId,
		Func<Player, bool>? availability = null)
	{
		if (RejectBuiltInType(registration.Type, "player rune"))
		{
			return;
		}

		lock (SyncRoot)
		{
			int existingIndex = PlayerRuneRegistrations.FindIndex(
				existing => HextechModelTypeIdentity.IsSame(existing.Type, registration.Type));
			if (existingIndex < 0)
			{
				PlayerRuneRegistrations.Add(registration);
				TryStoreAssetModId(registration.Type, assetModId);
				TryStorePlayerRuneAvailability(registration.Type, availability);
				_version++;
				return;
			}

			PlayerRuneRegistration existing = PlayerRuneRegistrations[existingIndex];
			string? existingAssetModId = GetStoredAssetModId(registration.Type);
			if (!HasSamePlayerRuneMetadata(existing, registration))
			{
				HextechLog.Warn(
					"ExternalContent", $"Conflicting duplicate player rune registration for {registration.Type.FullName}; first metadata retained: "
					+ $"existing=({Describe(existing)}, assetModId={DescribeValue(existingAssetModId)}) "
					+ $"incoming=({Describe(registration)}, assetModId={DescribeValue(assetModId)}) "
					+ $"callerAssembly={DescribeCallerAssembly(registration.Type)}");
			}

			bool assetOwnerStored = TryStoreAssetModId(registration.Type, assetModId);
			bool availabilityStored = TryStorePlayerRuneAvailability(registration.Type, availability);
			if (assetOwnerStored || availabilityStored)
			{
				_version++;
			}
		}
	}

	internal static void RegisterEventRelic(Type relicType, string? assetModId)
	{
		lock (SyncRoot)
		{
			if (!EventRelicTypes.Any(existing => HextechModelTypeIdentity.IsSame(existing, relicType)))
			{
				EventRelicTypes.Add(relicType);
				TryStoreAssetModId(relicType, assetModId);
				_version++;
				return;
			}

			if (TryStoreAssetModId(relicType, assetModId))
			{
				_version++;
			}
		}
	}

	internal static void RegisterForge(ForgeRegistration registration, string? assetModId)
	{
		if (RejectBuiltInType(registration.Type, "forge"))
		{
			return;
		}

		lock (SyncRoot)
		{
			int existingIndex = ForgeRegistrations.FindIndex(
				existing => HextechModelTypeIdentity.IsSame(existing.Type, registration.Type));
			if (existingIndex < 0)
			{
				ForgeRegistrations.Add(registration);
				TryStoreAssetModId(registration.Type, assetModId);
				_version++;
				return;
			}

			ForgeRegistration existing = ForgeRegistrations[existingIndex];
			string? existingAssetModId = GetStoredAssetModId(registration.Type);
			if (existing.Rarity != registration.Rarity)
			{
				HextechLog.Warn(
					"ExternalContent", $"Conflicting duplicate forge registration for {registration.Type.FullName}; first metadata retained: "
					+ $"existing=(rarity={existing.Rarity}, assetModId={DescribeValue(existingAssetModId)}) "
					+ $"incoming=(rarity={registration.Rarity}, assetModId={DescribeValue(assetModId)}) "
					+ $"callerAssembly={DescribeCallerAssembly(registration.Type)}");
			}

			if (TryStoreAssetModId(registration.Type, assetModId))
			{
				_version++;
			}
		}
	}

	internal static void RegisterEnchantmentIcon(Type enchantmentType, string iconPath)
	{
		lock (SyncRoot)
		{
			if (TryStoreFirstWriter(
				EnchantmentIconPathsByModelId,
				ModelDb.GetId(enchantmentType),
				iconPath,
				"external-content.enchantment-icon-conflict",
				(existingPath, incomingPath) => $"Conflicting duplicate enchantment icon registration for {enchantmentType.FullName}; first path retained: "
					+ $"existingPath={DescribeValue(existingPath)} incomingPath={DescribeValue(incomingPath)} "
					+ $"callerAssembly={DescribeCallerAssembly(enchantmentType)}"))
			{
				_version++;
			}
		}
	}

	internal static IReadOnlyList<PlayerRuneRegistration> GetPlayerRuneRegistrations()
	{
		lock (SyncRoot)
		{
			return PlayerRuneRegistrations.ToArray();
		}
	}

	internal static IReadOnlyList<Type> GetEventRelicTypes()
	{
		lock (SyncRoot)
		{
			return EventRelicTypes.ToArray();
		}
	}

	internal static IReadOnlyList<ForgeRegistration> GetForgeRegistrations()
	{
		lock (SyncRoot)
		{
			return ForgeRegistrations.ToArray();
		}
	}

	internal static string? GetAssetModId(ModelId id)
	{
		lock (SyncRoot)
		{
			return AssetModIdsByModelId.TryGetValue(id, out string? modId)
				? modId
				: null;
		}
	}

	internal static void SetPlayerRunePoolLabel(Type runeType, string poolKey)
	{
		lock (SyncRoot)
		{
			TryStoreFirstWriter(
				PlayerRunePoolLabelKeysByModelId,
				ModelDb.GetId(runeType),
				poolKey,
				"external-content.pool-label-conflict",
				(existing, incoming) => DescribeStringConflict($"pool label for {runeType.FullName}", existing, incoming));
		}
	}

	internal static void RegisterConfigSectionTitle(string assetModId, string titleKey)
	{
		lock (SyncRoot)
		{
			TryStoreFirstWriter(
				ConfigSectionTitleKeysByAssetModId,
				assetModId,
				titleKey,
				"external-content.config-section-conflict",
				(existing, incoming) => DescribeStringConflict($"config section title for {assetModId}", existing, incoming));
		}
	}

	internal static void SetPlayerRuneCharacter(Type runeType, Type characterType)
	{
		lock (SyncRoot)
		{
			if (TryStoreFirstWriter(
				PlayerRuneCharacterIdsByModelId,
				ModelDb.GetId(runeType),
				ModelDb.GetId(characterType),
				"external-content.rune-character-conflict",
				(existing, incoming) => $"Conflicting duplicate character for {runeType.FullName}; first character retained: "
					+ $"existing={existing.Entry} incoming={incoming.Entry} callerAssembly={DescribeCallerAssembly(runeType)}"))
			{
				_version++;
			}
		}
	}

	internal static ModelId? GetPlayerRuneCharacterId(ModelId runeId)
	{
		lock (SyncRoot)
		{
			return PlayerRuneCharacterIdsByModelId.TryGetValue(runeId, out ModelId? characterId)
				? characterId
				: null;
		}
	}

	internal static bool HasPlayerRunesForCharacter(ModelId characterId)
	{
		lock (SyncRoot)
		{
			return PlayerRuneCharacterIdsByModelId.ContainsValue(characterId);
		}
	}

	internal static string? GetPlayerRunePoolLabel(ModelId id)
	{
		lock (SyncRoot)
		{
			return PlayerRunePoolLabelKeysByModelId.TryGetValue(id, out string? poolKey)
				? poolKey
				: null;
		}
	}

	internal static string? GetConfigSectionTitleKey(string assetModId)
	{
		lock (SyncRoot)
		{
			return ConfigSectionTitleKeysByAssetModId.TryGetValue(assetModId, out string? titleKey)
				? titleKey
				: null;
		}
	}

	internal static Func<Player, bool>? GetPlayerRuneAvailability(ModelId id)
	{
		lock (SyncRoot)
		{
			return PlayerRuneAvailabilityByModelId.TryGetValue(id, out Func<Player, bool>? availability)
				? availability
				: null;
		}
	}

	internal static string? GetEnchantmentIconPath(ModelId id)
	{
		lock (SyncRoot)
		{
			return EnchantmentIconPathsByModelId.TryGetValue(id, out string? path)
				? path
				: null;
		}
	}

	/// <summary>
	/// 外部 API 不能重新登记本体内置的符文/锻造:内置与外部列表会被拼接后按类型 ToDictionary,
	/// 同一类型出现两次会让注册表查找永久抛 ArgumentException。前置的 ModelId 检查按 IsSame 放行同一类型,
	/// 所以必须对内置清单单独拦截。<see cref="HextechRunesApi"/> 在池登记与 SavedProperty 注入之前先调用一次,
	/// 这里的最终登记处再兜底一次。
	/// </summary>
	internal static bool RejectBuiltInType(Type modelType, string kind)
	{
		bool isBuiltIn = HextechPlayerRuneRegistry.Registrations.Any(builtIn => HextechModelTypeIdentity.IsSame(builtIn.Type, modelType))
			|| HextechForgeRegistry.Registrations.Any(builtIn => HextechModelTypeIdentity.IsSame(builtIn.Type, modelType));
		if (!isBuiltIn)
		{
			return false;
		}

		if (HextechRunLogBudget.TryConsume("external-content.built-in-rejected", 12))
		{
			HextechLog.Warn(
				"ExternalContent", $"Rejected external {kind} registration for built-in type {modelType.FullName}; built-in metadata retained.");
		}

		return true;
	}

	private static bool TryStoreAssetModId(Type modelType, string? assetModId)
	{
		if (string.IsNullOrWhiteSpace(assetModId))
		{
			return false;
		}

		return TryStoreFirstWriter(
			AssetModIdsByModelId,
			ModelDb.GetId(modelType),
			assetModId,
			"external-content.asset-owner-conflict",
			(existing, incoming) => $"Conflicting asset owner registration for {modelType.FullName}; first owner retained: "
				+ $"existingAssetModId={DescribeValue(existing)} "
				+ $"incomingAssetModId={DescribeValue(incoming)} "
				+ $"callerAssembly={DescribeCallerAssembly(modelType)}");
	}

	// 首个非空可用性委托生效，冲突登记只告警，不覆盖先前的委托。
	private static bool TryStorePlayerRuneAvailability(Type runeType, Func<Player, bool>? availability)
	{
		if (availability == null)
		{
			return false;
		}

		return TryStoreFirstWriter(
			PlayerRuneAvailabilityByModelId,
			ModelDb.GetId(runeType),
			availability,
			"external-content.availability-conflict",
			(_, _) => $"Conflicting duplicate availability predicate for {runeType.FullName}; first predicate retained: "
				+ $"callerAssembly={DescribeCallerAssembly(runeType)}");
	}

	// 首个写入者生效：同键再次登记不同的值只按预算告警、不覆盖，登记相同的值静默忽略。
	// 字符串按 Ordinal 比较，委托按目标与方法比较（EqualityComparer.Default 与原先的 == / != 一致）。
	private static bool TryStoreFirstWriter<TKey, TValue>(
		Dictionary<TKey, TValue> store,
		TKey key,
		TValue value,
		string logBudgetKey,
		Func<TValue, TValue, string> describeConflict)
		where TKey : notnull
	{
		if (store.TryGetValue(key, out TValue? existing))
		{
			if (!EqualityComparer<TValue>.Default.Equals(existing, value)
				&& HextechRunLogBudget.TryConsume(logBudgetKey, 12))
			{
				HextechLog.Warn("ExternalContent", describeConflict(existing, value));
			}

			return false;
		}

		store.Add(key, value);
		return true;
	}

	private static string DescribeStringConflict(string description, string existing, string incoming)
	{
		return $"Conflicting duplicate {description}; first value retained: "
			+ $"existing={DescribeValue(existing)} incoming={DescribeValue(incoming)}";
	}

	private static string DescribeCallerAssembly(Type type)
	{
		return type.Assembly.GetName().Name ?? "<unknown>";
	}

	private static string? GetStoredAssetModId(Type modelType)
	{
		return AssetModIdsByModelId.TryGetValue(ModelDb.GetId(modelType), out string? assetModId)
			? assetModId
			: null;
	}

	private static bool HasSamePlayerRuneMetadata(
		PlayerRuneRegistration existing,
		PlayerRuneRegistration incoming)
	{
		return existing.Rarity == incoming.Rarity
			&& existing.Flags == incoming.Flags
			&& existing.CharacterPool == incoming.CharacterPool
			&& existing.CharacterOrder == incoming.CharacterOrder
			&& string.Equals(existing.TagKey, incoming.TagKey, StringComparison.Ordinal);
	}

	private static string Describe(PlayerRuneRegistration registration)
	{
		return $"rarity={registration.Rarity}, flags={registration.Flags}, "
			+ $"characterPool={registration.CharacterPool?.ToString() ?? "none"}, "
			+ $"characterOrder={registration.CharacterOrder}, tagKey={DescribeValue(registration.TagKey)}";
	}

	private static string DescribeValue(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? "<none>" : value;
	}
}
