using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class GlobalArtistsBranchBuilder(ITrackInfoProvider trackInfoProvider) : ITreeBranchBuilder
{
    public int Order => TreeBranchOrder.GlobalArtists;
    public bool IsStatic => true;

    public async Task<PlaylistTreeItem?> BuildBranchAsync(IReadOnlyList<Playlist>? existingPlaylists = null, CancellationToken ct = default)
    {
        var artists = await trackInfoProvider.GetArtistsWithTrackCountAsync();
        var artistPlaylists = artists
            .Select(a => new Playlist(a.artistName, PlaylistType.Artist)
            {
                TrackCount = a.trackCount
            })
            .ToList();

        return PlaylistTreeItem.FromGroup(new PlaylistGroup("Исполнители", artistPlaylists, isExpanded: false));
    }
}
