using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class PlaylistTreeComposer(
	IEnumerable<ITreeBranchBuilder> branchBuilders)
	: IPlaylistTreeComposer
{
	private Dictionary<Type, PlaylistTreeItem>? _staticCache;

	/// <inheritdoc />
	public async Task<IReadOnlyList<PlaylistTreeItem>> ComposeAsync(
		IReadOnlyList<Playlist> playlists, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(playlists);

		var result = new List<PlaylistTreeItem>();

		foreach (var builder in branchBuilders.OrderBy(b => b.Order))
		{
			if (builder.IsStatic && _staticCache?.TryGetValue(builder.GetType(), out var cached) == true)
			{
				result.Add(cached);
			}
			else
			{
				var branch = await builder.BuildBranchAsync(playlists, ct);
				if (branch is not null)
				{
					result.Add(branch);
					if (builder.IsStatic)
						(_staticCache ??= new())[builder.GetType()] = branch;
				}
			}
		}

		result.AddRange(playlists
			.Where(IsApplicationRootPlaylist)
			.Select(PlaylistTreeItem.FromPlaylist));

		return result;
	}

	public void InvalidateCache() => _staticCache = null;

	private static bool IsApplicationRootPlaylist(Playlist p)
		=> p.SourceId is null && p.Type.GetCategory() != PlaylistCategory.Entity
		   || p.Type.GetCategory() == PlaylistCategory.Computed;
}
