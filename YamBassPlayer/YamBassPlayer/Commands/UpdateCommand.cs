using YamBassPlayer.Presenters;

namespace YamBassPlayer.Commands;

/// <summary>Ручная проверка обновлений — то же, что пункт меню «Файл → Проверка обновлений…».</summary>
public sealed class UpdateCommand(IUpdatePresenter updatePresenter) : ICommand
{
	public string Name => "update";

	public string Description => "Проверить обновления";

	public IReadOnlyList<string> Aliases => ["upd"];

	public CommandResult Execute(string[] args)
	{
		updatePresenter.CheckForUpdates(manual: true);
		return CommandResult.Ok("Проверка обновлений запущена");
	}
}
