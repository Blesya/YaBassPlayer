using YamBassPlayer.Models;

namespace YamBassPlayer.Plugins;

/// <summary>
/// Действие над текущим треком: кнопка в панели управления.
/// </summary>
public interface ITrackAction
{
    string Id { get; }

    /// <summary>Текст кнопки для конкретного трека, например «Яндекс ♡».</summary>
    string GetTitle(Track track);

    bool IsAvailable(Track track);

    Task ExecuteAsync(Track track);

    /// <summary>Состояние изменилось — хост перерисует кнопку.</summary>
    event Action? Changed;
}
