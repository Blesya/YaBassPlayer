using YamBassPlayer.Commands;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;

namespace YamBassPlayer.Plugins;

/// <summary>
/// <c>likeyandex</c>: лайк текущего трека в Яндекс.Музыке. Это API-лайк плагина,
/// а не локальное избранное — для него есть <c>likelocal</c>.
/// </summary>
public sealed class YandexLikeCommand(IPlaybackQueue playback, IYandexFavoriteService favorites) : ICommand
{
    public string Name => "likeyandex";

    public string Description => "likeyandex|ly — лайк/анлайк текущего трека в Яндекс.Музыке.";

    public IReadOnlyList<string> Aliases => ["ly"];

    public CommandResult Execute(string[] args)
    {
        if (playback.CurrentTrack is not { } key)
            return CommandResult.Error("Нет текущего трека. Сначала начните воспроизведение");

        if (key.SourceId != YandexMusicSourceId.Value)
            return CommandResult.Error("Текущий трек не из Яндекс.Музыки");

        bool wasFavorite = favorites.IsTrackFavorite(key.TrackId);
        _ = ToggleAsync(key);
        return CommandResult.Ok(wasFavorite ? "Лайк снят" : "Лайк поставлен");
    }

    private async Task ToggleAsync(TrackKey key)
    {
        try
        {
            if (favorites.IsTrackFavorite(key.TrackId))
                await favorites.RemoveFromFavorites(key.TrackId);
            else
                await favorites.AddToFavorites(key.TrackId);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Команда likeyandex: не удалось переключить лайк {Key}", key);
        }
    }
}

/// <summary><c>ysearch</c>: открыть окно поиска по Яндекс.Музыке.</summary>
public sealed class YandexSearchCommand(Func<Task> showSearch) : ICommand
{
    public string Name => "ysearch";

    public string Description => "ysearch|ys — открыть поиск по Яндекс.Музыке.";

    public IReadOnlyList<string> Aliases => ["ys"];

    public CommandResult Execute(string[] args)
    {
        _ = OpenAsync();
        return CommandResult.Ok("Поиск по Яндекс.Музыке открыт");
    }

    private async Task OpenAsync()
    {
        try
        {
            await showSearch();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Команда ysearch: не удалось открыть поиск");
        }
    }
}
