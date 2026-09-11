using Terminal.Gui;

namespace YamBassPlayer.Services.Impl;

/// <summary>Реализация <see cref="IUiDispatcher"/> поверх статического <see cref="Application.MainLoop"/>.</summary>
public sealed class UiDispatcher : IUiDispatcher
{
	public void Invoke(Action action) => Application.MainLoop?.Invoke(action);

	public void StartTimer(TimeSpan interval, Func<bool> onTick)
	{
		Application.MainLoop?.AddTimeout(interval, _ => onTick());
	}
}
