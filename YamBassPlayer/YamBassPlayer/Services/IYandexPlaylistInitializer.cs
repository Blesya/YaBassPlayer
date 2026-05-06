using System.Threading;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IYandexPlaylistInitializer
{
    Task<int> InitializeAsync(IReadOnlyList<Playlist> yandexPlaylists, CancellationToken ct = default);
}
