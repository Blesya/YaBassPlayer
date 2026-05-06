using System.Threading;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITreeBranchBuilder
{
    int Order { get; }
    bool IsStatic { get; }
    Task<PlaylistTreeItem?> BuildBranchAsync(IReadOnlyList<Playlist>? existingPlaylists = null, CancellationToken ct = default);
}
