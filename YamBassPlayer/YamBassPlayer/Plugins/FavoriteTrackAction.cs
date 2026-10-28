using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;

namespace YamBassPlayer.Plugins;

/// <summary>
/// Локальное избранное: один список для треков любого источника.
/// Источник не важен — важно только, что пользователь сам добавил трек.
/// </summary>
public sealed class FavoriteTrackAction(ILocalFavoriteService favorites) : ITrackAction
{
    private const string LikedMark = "♥";
    private const string NotLikedMark = "♡";

    public string Id => "favorite";

    public event Action? Changed;

    public bool IsAvailable(Track track) => !string.IsNullOrWhiteSpace(track.Id);

    public string GetTitle(Track track)
        => favorites.IsTrackFavorite(track.Key) ? $"Локально {LikedMark}" : $"Локально {NotLikedMark}";

    public async Task ExecuteAsync(Track track)
    {
        if (favorites.IsTrackFavorite(track.Key))
            await favorites.RemoveFromFavorites(track.Key);
        else
            await favorites.AddToFavorites(track.Key);

        Changed?.Invoke();
    }
}
