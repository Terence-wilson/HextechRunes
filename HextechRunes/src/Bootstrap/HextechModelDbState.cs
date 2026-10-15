using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes;

/// <summary>
/// RitsuLib 等前置会补丁 ModelDb.GetEntry，在它们登记某个类型之后才改用带模组前缀的条目；
/// 外部模组可能先调用海克斯的注册接口、后登记内容，所以同一类型的 ModelId 在 ModelDb.Init 收录内容之前可能还会变。
/// 按 ModelId 建的缓存只有在收录之后才能长期保留，之前建的缓存下次访问时要重建。
/// </summary>
internal static class HextechModelDbState
{
	// 测试替身：测试进程里 ModelDb 早已收录内容，用它模拟"收录之前"。
	internal static Func<bool>? ContentRegisteredOverride;

	internal static bool IsContentRegistered => ContentRegisteredOverride?.Invoke() ?? ModelDb.Contains(typeof(WhisperingEarring));
}
