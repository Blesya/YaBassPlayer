using System.Threading;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IAppPlaylistProvider
{
    Task<IReadOnlyList<Playlist>> GetAppPlaylistsAsync(CancellationToken ct = default);
}
