using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;

namespace YamBassPlayer.Services.Impl;

public sealed class YandexFavoriteService : IYandexFavoriteService
{
	private const string FavoriteTrackIdsKey = "favoriteTrackIds";

	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly IPluginStateStore _state;
	private readonly HashSet<string> _likedTrackIds = new();
	private bool _isProcessing;
	public SourceId SourceId => YandexMusicSourceId.Value;

	public event Action<string>? OnFavoriteAdded;
	public event Action<string>? OnFavoriteRemoved;

	public YandexFavoriteService(YandexMusicApi api, AuthStorage storage, IPluginStateStore state)
	{
		_api = api;
		_storage = storage;
		_state = state;

		// Избранное прошлого запуска доступно до первого ответа API.
		foreach (var id in _state.Get<string[]>(FavoriteTrackIdsKey) ?? [])
		{
			if (!string.IsNullOrEmpty(id))
				_likedTrackIds.Add(id);
		}
	}

	public void Initialize(IEnumerable<string> likedIds)
	{
		_likedTrackIds.Clear();
		foreach (var id in likedIds)
		{
			_likedTrackIds.Add(id);
		}

		PersistFavoriteTrackIds();
	}

	public bool IsTrackFavorite(string trackId)
	{
		return _likedTrackIds.Contains(trackId);
	}

	public Task<IReadOnlyList<string>> GetAllFavoriteTrackIds()
	{
		lock (_likedTrackIds)
			return Task.FromResult<IReadOnlyList<string>>(_likedTrackIds.ToList());
	}

	public async Task AddToFavorites(string trackId)
	{
		if (_isProcessing || string.IsNullOrEmpty(trackId))
			return;

		if (_likedTrackIds.Contains(trackId))
			return;

		_isProcessing = true;
		try
		{
			var response = await _api.Track.GetAsync(_storage, trackId);
			var yTrack = response.Result.FirstOrDefault();
			if (yTrack == null)
				return;

			await _api.Library.AddTrackLikeAsync(_storage, yTrack);

			_likedTrackIds.Add(trackId);
			PersistFavoriteTrackIds();
			OnFavoriteAdded?.Invoke(trackId);
		}
		finally
		{
			_isProcessing = false;
		}
	}

	public async Task RemoveFromFavorites(string trackId)
	{
		if (_isProcessing || string.IsNullOrEmpty(trackId))
			return;

		if (!_likedTrackIds.Contains(trackId))
			return;

		_isProcessing = true;
		try
		{
			var response = await _api.Track.GetAsync(_storage, trackId);
			var yTrack = response.Result.FirstOrDefault();
			if (yTrack == null)
				return;

			await _api.Library.RemoveTrackLikeAsync(_storage, yTrack);

			_likedTrackIds.Remove(trackId);
			PersistFavoriteTrackIds();
			OnFavoriteRemoved?.Invoke(trackId);
		}
		finally
		{
			_isProcessing = false;
		}
	}

	private void PersistFavoriteTrackIds() =>
		_state.Set(FavoriteTrackIdsKey, _likedTrackIds.ToArray());
}
