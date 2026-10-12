namespace HextechRunes;

/// <summary>
/// 外部表现层：由其他模组接管海克斯三选一的界面。选择的状态、重随、敌方海克斯、存档与联机同步仍由本体负责，
/// 表现层只负责显示，并通过 <see cref="HextechSelectionSession"/> 发起操作。
/// 只对普通的玩家海克斯选择生效；“仅继续”、自选模式和锻造器等其他选择仍使用本体界面。
/// 用 <see cref="HextechRunesApi.RegisterSelectionPresenter"/> 登记。
/// </summary>
public interface IHextechSelectionPresenter
{
	/// <summary>
	/// 为 true 时，本幕开始选择前不关闭地图（表现层自己处理地图），接管的界面隐藏后也不进入只读地图预览。
	/// 只对表现层实际接管的界面生效：拒绝接管的界面以及“仅继续”、自选、锻造器等本体界面，显示前仍会关闭地图，
	/// 选择结束后照常重新打开。
	/// </summary>
	bool KeepsMapOpen { get; }

	/// <summary>
	/// 选择界面创建时调用。返回 true 表示接管：本体不构建自己的界面，也不使用共享的暗色背景。
	/// 只能读取 <paramref name="session"/> 的状态，不要在这里发起操作。
	/// </summary>
	bool ShouldPresent(HextechSelectionSession session);

	/// <summary>
	/// 接管后，选择界面进入场景树时调用。表现层可以把自己的节点挂在 <see cref="HextechSelectionSession.Screen"/> 下，
	/// 或挂在别处并在 <see cref="HextechSelectionSession.Closed"/> 时清理。
	/// </summary>
	void Present(HextechSelectionSession session);
}
