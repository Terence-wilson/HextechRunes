using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace HextechRunes;

/// <summary>战斗特效挂在原版节点生命周期上的补丁；特效本体在 <see cref="HextechCombatVfx"/>。</summary>
internal static class HextechCombatVfxHooks
{
	/// <summary>
	/// 吞噬灵魂的特效在死亡动画开始的瞬间派发(真死亡分支必经点),而不是等 rune 的 AfterDeath:
	/// Hook.AfterDeath 是逐监听器顺序 await 的链条,排在前面的监听器等待死亡动画会让魂"卡一下"
	/// 才飞出(最后一只怪死亡时链条提前收尾所以不卡)。此处仅派发表现,数值仍在 rune 内结算。
	/// </summary>
	[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim), typeof(bool))]
	[HextechPatch("visual.combat-vfx.soul-drain", "吞噬灵魂特效")]
	private static class StartDeathAnimPatch
	{
		[HarmonyPostfix]
		private static void Postfix(NCreature __instance) => DispatchSoulDrainOnDeathAnim(__instance);
	}

	// 同时飞入的能力牌相对出牌区正中的横向偏移（固态时间开战、形态牌开局批量打出登记）；节点释放后自动消失。
	private static readonly ConditionalWeakTable<NCard, StrongBox<float>> PowerFlyFanOffsets = new();

	/// <summary>登记一张横向展开的能力牌相对出牌区正中的偏移，飞入时据此缩放路径（见 <see cref="PowerFlyReadyPatch"/>）。</summary>
	internal static void RegisterPowerFlyFanOffset(NCard? card, float offsetX)
	{
		if (card != null && offsetX != 0f)
		{
			PowerFlyFanOffsets.AddOrUpdate(card, new StrongBox<float>(offsetX));
		}
	}

	/// <summary>
	/// 原版能力牌飞入特效的 SwooshPath 曲线是场景里的共享资源，每个实例在 _Ready 里改写同一条曲线的终点。
	/// 原版一次只飞一张，看不出来；同时飞多张时，最后一个实例写入的“角色位置 - 自身起点”被所有实例共用，
	/// 横向错开的牌终点也跟着错开，排成一条直线而不是汇向角色。在实例进树前给它一份私有曲线。
	/// </summary>
	[HarmonyPatch(typeof(NCardFlyPowerVfx), nameof(NCardFlyPowerVfx.Create))]
	[HextechPatch("visual.combat-vfx.power-fly-curve", "能力牌飞入特效")]
	private static class PowerFlyCurvePatch
	{
		[HarmonyPostfix]
		private static void Postfix(NCardFlyPowerVfx? __result)
		{
			try
			{
				if (__result?.GetNodeOrNull<Path2D>("SwooshPath") is { Curve: { } curve } path)
				{
					path.Curve = (Curve2D)curve.Duplicate();
				}
			}
			catch (Exception ex)
			{
				HextechLog.Warn("CombatVfx", $"Power fly curve isolation failed: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// 曲线中间的弯曲点是按“出牌区正中飞向角色”设计的固定值，原版只改终点。横向展开后，离角色近的牌仍按原弧线绕，
	/// 会先越过角色再折回。原版 _Ready 写好终点后，把整条曲线按正中那张牌的路径做旋转与等比缩放，对齐到本牌的
	/// 起点到终点；没有登记偏移的单张飞行保持原样。
	/// </summary>
	[HarmonyPatch(typeof(NCardFlyPowerVfx), nameof(NCardFlyPowerVfx._Ready))]
	[HextechPatch("visual.combat-vfx.power-fly-fan", "能力牌飞入特效")]
	private static class PowerFlyReadyPatch
	{
		[HarmonyPostfix]
		private static void Postfix(NCardFlyPowerVfx __instance)
		{
			try
			{
				if (__instance.CardNode is not { } card
					|| !PowerFlyFanOffsets.TryGetValue(card, out StrongBox<float>? offset)
					|| __instance.GetNodeOrNull<Path2D>("SwooshPath")?.Curve is not { PointCount: >= 2 } curve)
				{
					return;
				}

				FitCurveToFannedCard(curve, offset.Value);
			}
			catch (Exception ex)
			{
				HextechLog.Warn("CombatVfx", $"Power fly fan fit failed: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// 曲线起点在本牌位置、终点已是“角色 - 本牌起点”。正中那张牌的终点是它加回横向偏移；
	/// 求把正中终点变成本牌终点的旋转与缩放，套到中间各点及其控制柄上。
	/// </summary>
	internal static void FitCurveToFannedCard(Curve2D curve, float offsetX)
	{
		int last = curve.PointCount - 1;
		Vector2 end = curve.GetPointPosition(last);
		Vector2 centerEnd = end + new Vector2(offsetX, 0f);
		if (centerEnd.LengthSquared() < 1f || end.LengthSquared() < 1f)
		{
			return;
		}

		float angle = end.Angle() - centerEnd.Angle();
		float scale = end.Length() / centerEnd.Length();
		Vector2 Map(Vector2 v) => v.Rotated(angle) * scale;
		for (int i = 1; i < last; i++)
		{
			curve.SetPointPosition(i, Map(curve.GetPointPosition(i)));
			curve.SetPointIn(i, Map(curve.GetPointIn(i)));
			curve.SetPointOut(i, Map(curve.GetPointOut(i)));
		}

		curve.SetPointIn(last, Map(curve.GetPointIn(last)));
		curve.SetPointOut(0, Map(curve.GetPointOut(0)));
	}

	private static void DispatchSoulDrainOnDeathAnim(NCreature deadNode)
	{
		try
		{
			Creature? dead = deadNode.Entity;
			if (dead is not { Side: CombatSide.Enemy } || dead.CombatState is not { } combatState
				|| !HextechMonsterInteractionPolicy.IsTrueCombatDeath(dead))
			{
				return;
			}

			foreach (Player player in combatState.Players)
			{
				if (player.Creature is { IsDead: false } collector && player.GetRelic<SoulEaterRune>() != null)
				{
					// 缕数必须在此刻(死亡瞬间)按身份算好:特效延迟一帧执行时死者已被移出战斗,
					// CombatState 为 null、按小怪兜底,导致精英/BOSS 也只掉 1-2 缕。
					HextechCombatVfx.SoulDrain(dead, collector, HextechCombatVfx.GetSoulWispCount(dead));
				}
			}
		}
		catch (Exception ex)
		{
			HextechLog.Warn("CombatVfx", $"Soul drain dispatch on death anim failed: {ex.Message}");
		}
	}
}
