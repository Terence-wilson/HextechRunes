using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes;

namespace HextechRunes;

/// <summary>
/// 质变提示：仿英雄联盟海克斯大乱斗，质变海克斯转变时在屏幕中央逐行显示"X 已转变为 Y。"，数秒后淡出。
/// 纯本地表现，不进存档和联机状态；我方只提示本机玩家自己的质变，敌方质变各端都提示。
/// </summary>
internal static class HextechTransmuteNotice
{
	private const string PlayerKey = "HEXTECH_TRANSMUTE_NOTICE";
	private const string EnemyKey = "HEXTECH_ENEMY_TRANSMUTE_NOTICE";
	private const string FallbackTemplate = "{From} -> {To}";
	// 高于选择界面退场层（100），选完质变时提示压在淡出的选择界面上方。
	private const int CanvasLayerIndex = 110;
	private const int FontSize = 26;
	private const int MaxLines = 8;
	private const double FadeInSeconds = 0.15;
	private const double HoldSeconds = 4.0;
	private const double FadeOutSeconds = 0.6;
	private static readonly Color TextColor = new(0.96f, 0.95f, 0.92f, 1f);
	private static readonly Color OutlineColor = new(0f, 0f, 0f, 0.85f);

	private static CanvasLayer? _layer;
	private static VBoxContainer? _lines;

	internal static void ShowPlayer(RelicModel transmute, IReadOnlyList<ModelId> obtainedIds)
	{
		ShowLines(PlayerKey, transmute.Title.GetFormattedText(), obtainedIds.Select(static id => ModelDb.GetById<RelicModel>(id).Title.GetFormattedText()));
	}

	internal static void ShowEnemy(MonsterHexKind transmute, MonsterHexKind gained)
	{
		ShowLines(EnemyKey, GetEnemyHexTitle(transmute), [ GetEnemyHexTitle(gained) ]);
	}

	internal static string Format(string template, string from, string to)
	{
		return template.Replace("{From}", from).Replace("{To}", to);
	}

	private static string GetEnemyHexTitle(MonsterHexKind hex)
	{
		return MonsterHexCatalog.GetIconRelicForMonsterHex(hex).Title.GetFormattedText();
	}

	private static void ShowLines(string templateKey, string from, IEnumerable<string> toTitles)
	{
		// 测试进程和启动早期没有场景树，直接不显示。
		if (NGame.Instance?.GetTree()?.Root is not Window root)
		{
			return;
		}

		try
		{
			string template = HextechRuneLabels.TryGetText(templateKey) ?? FallbackTemplate;
			VBoxContainer lines = EnsureLines(root);
			foreach (string to in toTitles)
			{
				AddLine(lines, Format(template, from, to));
			}
		}
		catch (Exception ex)
		{
			HextechLog.Warn("TransmuteNotice", $"Show failed: key={templateKey} from={from} error={ex.Message}");
		}
	}

	private static VBoxContainer EnsureLines(Window root)
	{
		if (_layer != null && GodotObject.IsInstanceValid(_layer) && _lines != null && GodotObject.IsInstanceValid(_lines))
		{
			return _lines;
		}

		_layer = new CanvasLayer
		{
			Name = "HextechTransmuteNoticeLayer",
			Layer = CanvasLayerIndex
		};
		Control frame = new()
		{
			Name = "Frame",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		frame.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_lines = new VBoxContainer
		{
			Name = "Lines",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Center,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.38f,
			AnchorBottom = 0.38f,
			OffsetLeft = -720f,
			OffsetRight = 720f,
			GrowVertical = Control.GrowDirection.Both
		};
		_lines.AddThemeConstantOverride("separation", 4);
		frame.AddChild(_lines);
		_layer.AddChild(frame);
		root.AddChild(_layer);
		return _lines;
	}

	private static void AddLine(VBoxContainer lines, string bbcode)
	{
		while (lines.GetChildCount() >= MaxLines)
		{
			Node oldest = lines.GetChild(0);
			lines.RemoveChild(oldest);
			oldest.QueueFree();
		}

		MegaRichTextLabel label = new()
		{
			BbcodeEnabled = true,
			FitContent = true,
			AutoSizeEnabled = false,
			ScrollActive = false,
			AutowrapMode = TextServer.AutowrapMode.Off,
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = HextechUiTheme.TransparentWhite
		};
		ApplyFont(label);
		lines.AddChild(label);
		label.Text = bbcode;

		Tween tween = label.CreateTween();
		tween.TweenProperty(label, "modulate:a", 1f, FadeInSeconds);
		tween.TweenInterval(HoldSeconds);
		tween.TweenProperty(label, "modulate:a", 0f, FadeOutSeconds);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}

	private static void ApplyFont(MegaRichTextLabel label)
	{
		Font font = label.GetThemeDefaultFont();
		if (font != null)
		{
			label.AddThemeFontOverride("normal_font", font);
			label.AddThemeFontOverride("bold_font", font);
		}

		label.AddThemeFontSizeOverride("normal_font_size", FontSize);
		label.AddThemeFontSizeOverride("bold_font_size", FontSize);
		label.AddThemeColorOverride("default_color", TextColor);
		label.AddThemeColorOverride("font_outline_color", OutlineColor);
		label.AddThemeConstantOverride("outline_size", 8);
	}
}
