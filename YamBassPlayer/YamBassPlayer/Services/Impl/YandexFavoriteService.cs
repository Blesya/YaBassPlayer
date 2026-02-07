using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer.Services.Impl;

public sealed class YandexFavoriteService : IYandexFavoriteService
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly HashSet<string> _likedTrackIds = new();
	private bool _isProcessing;

	public event Action<string>? OnFavoriteAdded;
	public event Action<string>? OnFavoriteRemoved;

	public YandexFavoriteService(YandexMusicApi api, AuthStorage storage)
	{
		_api = api;
		_storage = storage;
	}

	public void Initialize(IEnumerable<string> likedIds)
	{
		_likedTrackIds.Clear();
		foreach (var id in likedIds)
		{
			_likedTrackIds.Add(id);
		}
	}

	public bool IsTrackFavorite(string trackId)
	{
		return _likedTrackIds.Contains(trackId);
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
			OnFavoriteRemoved?.Invoke(trackId);
		}
		finally
		{
			_isProcessing = false;
		}
	}
}
