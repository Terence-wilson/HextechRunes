using MegaCrit.Sts2.Core.Models.Characters;

namespace HextechRunes;

internal static class HextechPlayerContextHelper
{
	// 两个取联机类型的入口口径一致：只兜住联机服务在拆除过程中的空引用，其余异常照常抛出。
	public static bool IsNetworkMultiplayerRun()
	{
		try
		{
			return RunManager.Instance?.NetService?.Type is NetGameType gameType && IsNetworkGameType(gameType);
		}
		catch (NullReferenceException)
		{
			return false;
		}
	}

	// 海克斯流程只分"联机（Host/Client）"和"单人"两种：原版对局重放（Replay）没有联机连接，
	// 按单人走，不能掉进需要同步通道的联机分支。
	public static bool IsNetworkGameType(NetGameType gameType)
	{
		return gameType is NetGameType.Host or NetGameType.Client;
	}

	public static bool IsSinglePlayerFlow(NetGameType gameType)
	{
		return !IsNetworkGameType(gameType);
	}

	/// <summary>当前是已连接的联机(房主或客户端)。断线后的联机局、单机/回放以及联机服务尚未建立时都返回 false。</summary>
	public static bool IsMultiplayerConnected()
	{
		INetGameService? netService = RunManager.Instance?.NetService;
		return netService != null && IsNetworkGameType(netService.Type) && netService.IsConnected;
	}

	public static bool IsClientRun(bool fallbackWhenUnavailable = false)
	{
		try
		{
			INetGameService? netService = RunManager.Instance?.NetService;
			return netService == null
				? fallbackWhenUnavailable
				: netService.Type == NetGameType.Client;
		}
		catch (NullReferenceException)
		{
			return fallbackWhenUnavailable;
		}
	}

	public static int GetActNumberForScaling(Player? owner)
	{
		if (HextechMayhemModifier.FindIn(owner?.RunState)?.IsEndlessLoopActive == true)
		{
			return 3;
		}

		return Math.Clamp((owner?.RunState.CurrentActIndex ?? 0) + 1, 1, 3);
	}

	public static bool IsDefectPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Defect>();
	}

	public static bool IsIroncladPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Ironclad>();
	}

	public static bool IsSilentPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Silent>();
	}

	public static bool IsRegentPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Regent>();
	}

	public static bool IsNecrobinderPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Necrobinder>();
	}

	public static ModelId GetCharacterId(PlayerRuneCharacterPool characterPool)
	{
		return characterPool switch
		{
			PlayerRuneCharacterPool.Ironclad => ModelDb.GetId<Ironclad>(),
			PlayerRuneCharacterPool.Silent => ModelDb.GetId<Silent>(),
			PlayerRuneCharacterPool.Regent => ModelDb.GetId<Regent>(),
			PlayerRuneCharacterPool.Defect => ModelDb.GetId<Defect>(),
			PlayerRuneCharacterPool.Necrobinder => ModelDb.GetId<Necrobinder>(),
			_ => throw new ArgumentOutOfRangeException(nameof(characterPool), characterPool, null)
		};
	}

	public static bool TryGetRuneCharacterPool(Player player, out PlayerRuneCharacterPool characterPool)
	{
		if (IsIroncladPlayer(player))
		{
			characterPool = PlayerRuneCharacterPool.Ironclad;
			return true;
		}

		if (IsSilentPlayer(player))
		{
			characterPool = PlayerRuneCharacterPool.Silent;
			return true;
		}

		if (IsRegentPlayer(player))
		{
			characterPool = PlayerRuneCharacterPool.Regent;
			return true;
		}

		if (IsDefectPlayer(player))
		{
			characterPool = PlayerRuneCharacterPool.Defect;
			return true;
		}

		if (IsNecrobinderPlayer(player))
		{
			characterPool = PlayerRuneCharacterPool.Necrobinder;
			return true;
		}

		characterPool = default;
		return false;
	}
}
