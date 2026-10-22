using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Commands;

/// <summary>
/// search — поиск по локальной медиатеке. Найденное (до 50) показывается
/// в transient-плейлисте результатов.
/// </summary>
public sealed class SearchCommand(
	IEventBus eventBus,
	SourceId localSourceId) : ICommand
{
	public string Name => "search";
	public string Description => "search|find <текст> - поиск по локальной медиатеке.";
	public IReadOnlyList<string> Aliases => ["find"];

	public CommandResult Execute(string[] args)
	{
		if (args.Length == 0)
			return CommandResult.Error("Укажите текст: search <текст>");

		string query = string.Join(' ', args).Trim();
		if (query.Length == 0)
			return CommandResult.Error("Укажите текст для поиска");

		eventBus.Publish(new SearchCommandEvent(localSourceId, query));
		return CommandResult.Ok($"Ищу треков по запросу <{query}> ...");
	}
}
