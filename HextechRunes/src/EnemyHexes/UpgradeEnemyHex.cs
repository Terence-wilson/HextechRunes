namespace HextechRunes;

internal sealed class UpgradeEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Upgrade;

	internal override Task AfterCardPlayed(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (!context.IsManualPlayerCardPlay(cardPlay, out _, out _)
			|| !cardPlay.Card.IsUpgraded)
		{
			return Task.CompletedTask;
		}

		// 放开上限的多级打击/防御只降一级（升级：打击/防御）；其余牌照原版清零。
		HextechStarterUpgradeHooks.DowngradeOneLevel(cardPlay.Card);
		return Task.CompletedTask;
	}
}
