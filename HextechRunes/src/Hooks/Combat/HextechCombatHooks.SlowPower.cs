namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	// 原版 GetTypeForAmount 对 Counter+AllowNegative 的负层数一律判 Debuff，且不是虚方法、没有 Hook。
	// 原版是纯计算，照常执行后，只对本模组的两种缓慢能力把结果改为 None（缓慢的正负层只表达受伤倍率方向）。
	[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.GetTypeForAmount), typeof(decimal))]
	[HextechPatch("combat.power-type-for-amount", "中性能力类型")]
	private static class PowerTypeForAmountPatch
	{
		[HarmonyPostfix]
		private static void Postfix(
			PowerModel __instance,
			ref PowerType __result)
		{
			if (__instance is HextechPlayerSlowPower or HextechTemporarySlowPower)
			{
				__result = PowerType.None;
			}
		}
	}
}
