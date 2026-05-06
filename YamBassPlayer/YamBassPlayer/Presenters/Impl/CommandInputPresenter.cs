using YamBassPlayer.Commands;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

/// <summary>
/// Мост между полем ввода команд и реестром команд.
/// Разбирает ввод, делегирует выполнение в <see cref="CommandRegistry"/>
/// и показывает результат в строке вывода.
/// </summary>
public sealed class CommandInputPresenter : ICommandInputPresenter
{
	private readonly ICommandInputView _view;
	private readonly CommandRegistry _registry;

	public CommandInputPresenter(ICommandInputView view, CommandRegistry registry)
	{
		_view = view;
		_registry = registry;

		_view.OnCommandSubmitted += HandleSubmitted;
	}

	public void HandleSubmitted(string raw)
	{
		var result = _registry.Execute(raw);
		_view.SetResult(result.Message);
	}
}
