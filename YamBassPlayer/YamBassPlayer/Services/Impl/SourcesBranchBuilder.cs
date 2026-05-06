using System.Threading;
using Terminal.Gui.Trees;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class SourcesBranchBuilder(
    IMusicSourceRegistry musicSourceRegistry,
    ILocalLibraryService localLibraryService) : ITreeBranchBuilder
{
    public int Order => TreeBranchOrder.Sources;
    public bool IsStatic => false;

    public async Task<PlaylistTreeItem?> BuildBranchAsync(IReadOnlyList<Playlist>? existingPlaylists = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var yandexSource = musicSourceRegistry.GetRequired(SourceIds.Yandex);
        var localSource = musicSourceRegistry.GetRequired(SourceIds.Local);

        var yandexPlaylists = existingPlaylists?
            .Where(p => p.SourceId == SourceIds.Yandex)
            .ToList()
            ?? (await yandexSource.GetPlaylistsAsync(ct)).ToList();

        var localPlaylists = (await localSource.GetPlaylistsAsync(ct)).ToList();

        var yandexNode = PlaylistTreeItem.FromGroup(new PlaylistGroup(yandexSource.DisplayName, yandexPlaylists, isExpanded: false));
        yandexNode.Tag = SourceIds.Yandex;

        var localNode = await BuildLocalMusicRootAsync(localSource, localPlaylists);

        var sourcesRoot = new PlaylistTreeItem
        {
            Label = "Источники",
            Children = [yandexNode, localNode],
            Tag = "sources-root",
            IsExpandedByDefault = true
        };

        sourcesRoot.UpdateText();
        return sourcesRoot;
    }

    private async Task<PlaylistTreeItem> BuildLocalMusicRootAsync(IMusicSource localSource, List<Playlist> localPlaylists)
    {
        var folderPlaylists = localPlaylists.Where(p => p.Type == PlaylistType.LocalFolder).ToList();
        var allLocalPlaylist = localPlaylists.FirstOrDefault(p => p.Type == PlaylistType.LocalSearch);

        List<Playlist> sourcePlaylists = allLocalPlaylist is not null
            ? [allLocalPlaylist, ..folderPlaylists]
            : folderPlaylists;

        var children = new List<ITreeNode>(sourcePlaylists
            .Select(PlaylistTreeItem.FromPlaylist)
            .Cast<ITreeNode>());

        var localArtists = await localLibraryService.GetLocalArtistsAsync();
        if (localArtists.Count > 0)
        {
            children.Add(await BuildLocalArtistsRootAsync(localArtists));

            var localAlbumsRoot = await BuildLocalAlbumsRootAsync();
            if (localAlbumsRoot is not null)
            {
                children.Add(localAlbumsRoot);
            }
        }

        var localMusicGroup = new PlaylistGroup(localSource.DisplayName, sourcePlaylists, isExpanded: false);
        var item = new PlaylistTreeItem
        {
            Label = localMusicGroup.Name,
            Group = localMusicGroup,
            Children = children,
            Tag = SourceIds.Local
        };

        item.UpdateText();
        return item;
    }

    private async Task<PlaylistTreeItem> BuildLocalArtistsRootAsync(
        IReadOnlyList<(string artistName, int trackCount)> localArtists)
    {
        var artistTasks = localArtists.Select(async (artist) =>
        {
            var (artistName, trackCount) = artist;
            var artistPlaylist = new Playlist(artistName, PlaylistType.LocalArtist)
            {
                TrackCount = trackCount,
                SourceId = SourceIds.Local
            };
            var artistNode = PlaylistTreeItem.FromPlaylist(artistPlaylist);

            var albums = await localLibraryService.GetLocalAlbumsAsync(artistName);
            foreach (var (albumName, albumTrackCount) in albums)
            {
                var albumPlaylist = new Playlist(albumName, PlaylistType.LocalAlbum)
                {
                    TrackCount = albumTrackCount,
                    Description = $"{artistName}\n{albumName}",
                    SourceId = SourceIds.Local
                };
                artistNode.Children.Add(PlaylistTreeItem.FromPlaylist(albumPlaylist));
            }

            return artistNode;
        });

        var artistItems = (await Task.WhenAll(artistTasks))
            .Cast<ITreeNode>()
            .ToList();

        var item = new PlaylistTreeItem
        {
            Label = "Исполнители",
            Children = artistItems,
            Tag = "local-artists"
        };

        item.UpdateText();
        return item;
    }

    private async Task<PlaylistTreeItem?> BuildLocalAlbumsRootAsync()
    {
        var allAlbums = await localLibraryService.GetAllLocalAlbumsAsync();
        if (allAlbums.Count == 0)
        {
            return null;
        }

        var item = new PlaylistTreeItem
        {
            Label = "Альбомы",
            Children = allAlbums
                .Select(a => PlaylistTreeItem.FromPlaylist(
                    new Playlist(a.albumName, PlaylistType.LocalAlbum)
                    {
                        TrackCount = a.trackCount,
                        Description = $"\n{a.albumName}",
                        SourceId = SourceIds.Local
                    }))
                .Cast<ITreeNode>()
                .ToList(),
            Tag = "local-albums"
        };

        item.UpdateText();
        return item;
    }
}
