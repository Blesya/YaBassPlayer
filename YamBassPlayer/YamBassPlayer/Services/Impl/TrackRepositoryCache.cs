using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class TrackRepositoryCache : ITrackRepositoryCache
{
	private readonly Dictionary<string, List<string>> _customPlaylistCache = new();
	private readonly List<string> _favoriteTrackIds = [];
	private readonly List<string> _localSearchTrackIds = [];
	private readonly List<string> _yandexSearchTrackIds = [];
	private readonly List<string> _queueTrackIds = [];
	private readonly List<Track> _onSameWaveTracks = [];
	private readonly List<Track> _myWaveTracks = [];

	public IReadOnlyList<string> FavoriteTrackIds => _favoriteTrackIds;
	public IReadOnlyList<string> LocalSearchTrackIds => _localSearchTrackIds;
	public IReadOnlyList<string> YandexSearchTrackIds => _yandexSearchTrackIds;
	public IReadOnlyList<string> QueueTrackIds => _queueTrackIds;
	public IReadOnlyList<Track> OnSameWaveTracks => _onSameWaveTracks;
	public IReadOnlyList<Track> MyWaveTracks => _myWaveTracks;

	public void ReplaceFavoriteTrackIds(IEnumerable<string> trackIds)
	{
		_favoriteTrackIds.Clear();
		_favoriteTrackIds.AddRange(trackIds);
	}

	public void InsertFavoriteTrackId(string trackId)
	{
		if (!_favoriteTrackIds.Contains(trackId))
			_favoriteTrackIds.Insert(0, trackId);
	}

	public void RemoveFavoriteTrackId(string trackId)
		=> _favoriteTrackIds.Remove(trackId);

	public bool TryGetCustomPlaylistIds(string playlistName, out List<string> trackIds)
		=> _customPlaylistCache.TryGetValue(playlistName, out trackIds!);

	public void SetCustomPlaylistIds(string playlistName, List<string> trackIds)
		=> _customPlaylistCache[playlistName] = trackIds;

	public void ReplaceLocalSearchTracks(IEnumerable<Track> tracks)
	{
		_localSearchTrackIds.Clear();
		_localSearchTrackIds.AddRange(tracks.Select(t => t.Id));
	}

	public void ReplaceYandexSearchTracks(IEnumerable<Track> tracks)
	{
		_yandexSearchTrackIds.Clear();
		_yandexSearchTrackIds.AddRange(tracks.Select(t => t.Id));
	}

	public void ReplaceQueueTrackIds(IEnumerable<string> trackIds)
	{
		_queueTrackIds.Clear();
		_queueTrackIds.AddRange(trackIds);
	}

	public void ReplaceOnSameWaveTracks(IEnumerable<Track> tracks)
	{
		_onSameWaveTracks.Clear();
		_onSameWaveTracks.AddRange(tracks);
	}

	public void ReplaceMyWaveTracks(IEnumerable<Track> tracks)
	{
		_myWaveTracks.Clear();
		_myWaveTracks.AddRange(tracks);
	}

	public void AppendMyWaveTracks(IEnumerable<Track> tracks)
		=> _myWaveTracks.AddRange(tracks);
}
