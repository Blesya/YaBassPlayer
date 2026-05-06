using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Commands;

/// <summary>
/// search — поиск по медиатеке. Без префикса ищет в локальной медиатеке,
/// с префиксом ya/yandex — в Яндекс.Музыке. Результаты (до 50) показываются
/// в transient-плейлисте поиска.
/// Режим выдачи задаётся флагом (по умолчанию -t): -t — треки,
/// -ar — треки первого найденного исполнителя, -alb — треки первого найденного альбома.
/// Флаги -ar/-alb доступны только в режиме ya.
/// </summary>
public sealed class SearchCommand(IEventBus eventBus) : ICommand
{
	public string Name => "search";
	public string Description => "search|find [ya] [-t|-ar|-alb] <текст> — поиск (по умолчанию локально; ya — Яндекс.Музыка; -t — треки (по умолчанию), -ar/-alb — треки первого исполнителя/альбома).";
	public IReadOnlyList<string> Aliases => ["find"];

	public CommandResult Execute(string[] args)
	{
		if (args.Length == 0)
			return CommandResult.Error("Укажите текст: search <текст> или search ya <текст>");

		string source = SourceIds.Local;
		var kind = SearchEntityKind.Tracks;
		var rest = new List<string>();

		foreach (string arg in args)
		{
			if (string.Equals(arg, "ya", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(arg, "yandex", StringComparison.OrdinalIgnoreCase))
			{
				source = SourceIds.Yandex;
			}
			else if (string.Equals(arg, "-t", StringComparison.OrdinalIgnoreCase))
			{
				kind = SearchEntityKind.Tracks;
			}
			else if (string.Equals(arg, "-ar", StringComparison.OrdinalIgnoreCase))
			{
				kind = SearchEntityKind.Artist;
			}
			else if (string.Equals(arg, "-alb", StringComparison.OrdinalIgnoreCase))
			{
				kind = SearchEntityKind.Album;
			}
			else
			{
				rest.Add(arg);
			}
		}

		if (kind != SearchEntityKind.Tracks
			&& !string.Equals(source, SourceIds.Yandex, StringComparison.OrdinalIgnoreCase))
			return CommandResult.Error("Аргументы -ar/-alb доступны только в режиме ya (Яндекс.Музыка)");

		string query = string.Join(' ', rest).Trim();
		if (query.Length == 0)
			return CommandResult.Error("Укажите текст для поиска");

		eventBus.Publish(new SearchCommandEvent(source, query, kind));
		string entityLabel = kind switch
		{
			SearchEntityKind.Artist => "исполнителя",
			SearchEntityKind.Album => "альбом",
			_ => "треки"
		};
		return CommandResult.Ok($"Ищу {entityLabel} по запросу «{query}» ...");
	}
}
