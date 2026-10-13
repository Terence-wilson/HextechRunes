namespace HextechRunes.Tests;

internal static partial class Program
{
	// 敌方质变：在原位置换成 2 个目标品阶的海克斯，跳过已拥有的；抽到质变时继续展开；候选不足时少给。
	[HextechTest]
	private static void EnemyTransmuteExpandsInPlaceAndChains()
	{
		MonsterHexKind[] silverPool = [ MonsterHexKind.HeavyHitter, MonsterHexKind.EscapePlan, MonsterHexKind.Slap ];
		MonsterHexKind[] goldPool = [ MonsterHexKind.TransmuteSilver, MonsterHexKind.Sturdy ];
		IReadOnlyList<MonsterHexKind> Pool(HextechRarityTier rarity) => rarity == HextechRarityTier.Silver ? silverPool : goldPool;
		List<int> steps = [];
		int FirstCandidate(IReadOnlyList<MonsterHexKind> pool, int step)
		{
			steps.Add(step);
			return 0;
		}

		List<(MonsterHexKind From, MonsterHexKind To)> notices = [];
		IReadOnlyList<MonsterHexKind> chained = HextechEnemyTransmute.Expand(
			[ MonsterHexKind.Thornmail, MonsterHexKind.TransmuteGold, MonsterHexKind.BadTaste ],
			Pool,
			FirstCandidate,
			(from, to) => notices.Add((from, to)));
		SequenceEqual(
			[ MonsterHexKind.Thornmail, MonsterHexKind.Sturdy, MonsterHexKind.Slap, MonsterHexKind.EscapePlan, MonsterHexKind.BadTaste ],
			chained,
			"gold transmute yields two gold hexes in place; the silver transmute it rolled expands into two silver hexes");
		SequenceEqual([ 0, 1, 2, 3 ], steps, "each pick gets its own step for the stable hash");
		SequenceEqual(
			[
				(MonsterHexKind.TransmuteGold, MonsterHexKind.Sturdy),
				(MonsterHexKind.TransmuteGold, MonsterHexKind.TransmuteSilver),
				(MonsterHexKind.TransmuteSilver, MonsterHexKind.Slap),
				(MonsterHexKind.TransmuteSilver, MonsterHexKind.EscapePlan)
			],
			notices,
			"transmute notices follow the chain in pick order");
		Equal("质变：黄金阶[color=#F0C850]已转变为[/color]坚若磐石。", HextechTransmuteNotice.Format("{From}[color=#F0C850]已转变为[/color]{To}。", "质变：黄金阶", "坚若磐石"), "notice template fills both titles");

		IReadOnlyList<MonsterHexKind> skipsOwned = HextechEnemyTransmute.Expand(
			[ MonsterHexKind.Slap, MonsterHexKind.TransmuteSilver ],
			Pool,
			(_, _) => 0);
		SequenceEqual(
			[ MonsterHexKind.Slap, MonsterHexKind.EscapePlan, MonsterHexKind.HeavyHitter ],
			skipsOwned,
			"already active hexes are not rolled again");

		IReadOnlyList<MonsterHexKind> exhausted = HextechEnemyTransmute.Expand(
			[ MonsterHexKind.Slap, MonsterHexKind.EscapePlan, MonsterHexKind.TransmuteSilver ],
			Pool,
			(_, _) => 0);
		SequenceEqual(
			[ MonsterHexKind.Slap, MonsterHexKind.EscapePlan, MonsterHexKind.HeavyHitter ],
			exhausted,
			"a short pool grants fewer hexes instead of borrowing another rarity");

		IReadOnlyList<MonsterHexKind> plain = [ MonsterHexKind.Slap, MonsterHexKind.Sturdy ];
		SequenceEqual(plain, HextechEnemyTransmute.Expand(plain, Pool, (_, _) => throw new InvalidOperationException("no roll without transmute")), "lists without transmute are unchanged");

		Equal(HextechRarityTier.Gold, HextechMonsterHexRegistry.Registrations.Single(row => row.Kind == MonsterHexKind.TransmuteSilver).Rarity, "Transmute: Silver is a gold enemy hex");
		Equal(HextechRarityTier.Prismatic, HextechMonsterHexRegistry.Registrations.Single(row => row.Kind == MonsterHexKind.TransmuteGold).Rarity, "Transmute: Gold is a prismatic enemy hex");
	}
}
