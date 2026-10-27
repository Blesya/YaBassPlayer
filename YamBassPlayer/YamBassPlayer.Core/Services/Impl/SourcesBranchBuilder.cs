using System.Threading;
using Terminal.Gui.Trees;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;

namespace YamBassPlayer.Services.Impl;

public sealed class SourcesBranchBuilder(
    IMusicSourceRegistry musicSourceRegistry,
    ILocalLibraryService localLibraryService,
    IPlaylistRefreshStatus refreshStatus) : ITreeBranchBuilder
{
    public int Order => TreeBranchOrder.Sources;
    public bool IsStatic => false;

    public async Task<PlaylistTreeItem?> BuildBranchAsync(IReadOnlyList<Playlist>? existingPlaylists = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sourcesRoot = new PlaylistTreeItem
        {
            Label = "Источники",
            Tag = "sources-root",
            IsExpandedByDefault = true
        };

        foreach (IMusicSource source in musicSourceRegistry.Sources)
        {
            // Дерево собирается только из уже известного: сеть здесь не трогаем.
            // Для неготового источника показываем его плейлисты из снимка, а не
            // подсказку — иначе всё, что пользователь видел при закрытии, исчезает
            // на время входа, и прошлый плейлист не восстанавливается.
            IReadOnlyList<Playlist> playlists = existingPlaylists is null
                ? []
                : existingPlaylists
                    .Where(p => p.SourceId == source.Id && !IsLegacyLocalFavorite(source, p))
                    .ToList();

            if (source.State != SourceState.Ready && playlists.Count == 0)
            {
                sourcesRoot.Children.Add(BuildStateNode(source));
                continue;
            }

            bool stale = source.State != SourceState.Ready || refreshStatus.IsStale(source.Id);

            var children = BuildGroupedChildren(playlists);

            if (source.Id == LocalMusicSource.Id)
                children.AddRange(await BuildLocalEntityNodesAsync(ct));

            var node = new PlaylistTreeItem
            {
                Label = source.DisplayName,
                Group = new PlaylistGroup(source.DisplayName, playlists.ToList(), isExpanded: false),
                Children = children.Cast<ITreeNode>().ToList(),
                Tag = source.Id.Value,
                IsExpandedByDefault = false,
                IsStale = stale
            };

            node.UpdateText();
            sourcesRoot.Children.Add(node);
        }

        sourcesRoot.UpdateText();
        return sourcesRoot;
    }

    /// <summary>
    /// Избранное теперь одно на все источники: старый локальный узел из снимка
    /// не показываем, иначе он висит до первого обновления дерева.
    /// </summary>
    private static bool IsLegacyLocalFavorite(IMusicSource source, Playlist playlist)
        => source.Id == LocalMusicSource.Id && playlist.Kind == "favorite";

    /// <summary>
    /// Дети узла источника: плейлисты без группы идут первыми, затем группы
    /// в порядке первого появления.
    /// </summary>
    private static PlaylistTreeItem BuildStateNode(IMusicSource source)
    {
        string hint = source.State == SourceState.RequiresSetup
            ? "Настройте источник"
            : "Источник недоступен";

        var hintNode = new PlaylistTreeItem
        {
            Label = hint,
            Tag = "source-state:" + source.Id.Value
        };
        hintNode.UpdateText();

        var node = new PlaylistTreeItem
        {
            Label = source.DisplayName,
            Children = [hintNode],
            Tag = source.Id.Value,
            IsExpandedByDefault = true
        };
        node.UpdateText();
        return node;
    }

    private static List<PlaylistTreeItem> BuildGroupedChildren(IReadOnlyList<Playlist> playlists)
    {
        var children = new List<PlaylistTreeItem>();
        var groups = new List<(string Name, List<Playlist> Items)>();
        var groupIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (Playlist playlist in playlists)
        {
            if (string.IsNullOrWhiteSpace(playlist.Group))
            {
                children.Add(PlaylistTreeItem.FromPlaylist(playlist));
                continue;
            }

            if (!groupIndex.TryGetValue(playlist.Group, out int index))
            {
                index = groups.Count;
                groupIndex[playlist.Group] = index;
                groups.Add((playlist.Group, []));
            }

            groups[index].Items.Add(playlist);
        }

        foreach ((string name, List<Playlist> items) in groups)
        {
            var groupNode = new PlaylistTreeItem
            {
                Label = name,
                Children = items.Select(PlaylistTreeItem.FromPlaylist).Cast<ITreeNode>().ToList(),
                Tag = "source-group:" + name
            };
            groupNode.UpdateText();
            children.Add(groupNode);
        }

        return children;
    }

    private async Task<IReadOnlyList<PlaylistTreeItem>> BuildLocalEntityNodesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        IReadOnlyList<(string artistName, int trackCount)> artists = await localLibraryService.GetLocalArtistsAsync();
        if (artists.Count == 0)
            return [];

        var artistNodes = new List<PlaylistTreeItem>();
        foreach ((string artistName, int trackCount) in artists)
        {
            var artistPlaylist = new Playlist("artist", artistName)
            {
                SourceId = LocalMusicSource.Id,
                Category = PlaylistCategory.Entity,
                TrackCount = trackCount
            };
            var artistNode = PlaylistTreeItem.FromPlaylist(artistPlaylist);

            foreach ((string albumName, int albumTrackCount) in await localLibraryService.GetLocalAlbumsAsync(artistName))
            {
                var albumPlaylist = new Playlist("album", albumName)
                {
                    SourceId = LocalMusicSource.Id,
                    Category = PlaylistCategory.Entity,
                    Description = artistName,
                    TrackCount = albumTrackCount,
                    Payload = new Dictionary<string, string>
                    {
                        ["artist"] = artistName,
                        ["album"] = albumName
                    }
                };
                artistNode.Children.Add(PlaylistTreeItem.FromPlaylist(albumPlaylist));
            }

            artistNodes.Add(artistNode);
        }

        var result = new List<PlaylistTreeItem>();

        var artistsRoot = new PlaylistTreeItem
        {
            Label = "Исполнители",
            Children = artistNodes.Cast<ITreeNode>().ToList(),
            Tag = "local-artists"
        };
        artistsRoot.UpdateText();
        result.Add(artistsRoot);

        IReadOnlyList<(string albumName, int trackCount)> albums = await localLibraryService.GetAllLocalAlbumsAsync();
        if (albums.Count > 0)
        {
            var albumsRoot = new PlaylistTreeItem
            {
                Label = "Альбомы",
                Children = albums.Select(a => PlaylistTreeItem.FromPlaylist(
                        new Playlist("album", a.albumName)
                        {
                            SourceId = LocalMusicSource.Id,
                            Category = PlaylistCategory.Entity,
                            TrackCount = a.trackCount,
                            Payload = new Dictionary<string, string> { ["album"] = a.albumName }
                        }))
                    .Cast<ITreeNode>()
                    .ToList(),
                Tag = "local-albums"
            };
            albumsRoot.UpdateText();
            result.Add(albumsRoot);
        }

        return result;
    }
}
