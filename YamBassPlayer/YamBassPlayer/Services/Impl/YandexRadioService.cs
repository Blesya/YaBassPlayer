using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Radio;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

public sealed class YandexRadioService : IYandexRadioService
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;

	private YStation? _station;
	private string? _currentBatchId;
	private string? _lastTrackId;
	private readonly Dictionary<string, YTrack> _yTrackCache = new();

	public bool IsSessionActive => _station != null;

	public YandexRadioService(YandexMusicApi api, AuthStorage storage)
	{
		_api = api;
		_storage = storage;
	}

	public Task<bool> StartMyWaveAsync() => StartStationAsync("user", "onyourwave");

	public async Task<bool> StartTrackWaveAsync(string seedTrackId)
	{
		bool started = await StartStationAsync("track", seedTrackId);
		if (!started)
			started = await StartMyWaveAsync();
		return started;
	}

	private async Task<bool> StartStationAsync(string type, string tag)
	{
		try
		{
			Reset();
			var response = await _api.Radio.GetStationAsync(_storage, type, tag);
			_station = response?.Result?.FirstOrDefault();
			return _station != null;
		}
		catch
		{
			return false;
		}
	}

	public async Task<(IReadOnlyList<string> trackIds, IReadOnlyList<Track> tracks)> FetchNextBatchAsync()
	{
		if (_station == null)
			return ([], []);

		try
		{
			var response = await _api.Radio.GetStationTracksAsync(_storage, _station, _lastTrackId ?? "");
			var sequence = response?.Result;
			if (sequence == null)
				return ([], []);

			_currentBatchId = sequence.BatchId;

			var yTracks = sequence.Sequence?
				.Select(s => s.Track)
				.Where(t => t != null)
				.ToList() ?? [];

			bool isFirstBatch = _lastTrackId == null;

			foreach (var yTrack in yTracks)
				_yTrackCache[yTrack.Id] = yTrack;

			_lastTrackId = yTracks.LastOrDefault()?.Id;

			if (isFirstBatch && yTracks.Count > 0)
				await SendFeedbackInternalAsync(YStationFeedbackType.RadioStarted, yTracks.First(), 0);

			var trackIds = (IReadOnlyList<string>)yTracks.Select(t => t.Id).ToList();
			var tracks = (IReadOnlyList<Track>)yTracks.Select(t => t.ToTrack()).ToList();

			return (trackIds, tracks);
		}
		catch
		{
			return ([], []);
		}
	}

	public Task SendTrackStartedAsync(string trackId)
	{
		if (_station == null || !_yTrackCache.TryGetValue(trackId, out var yTrack))
			return Task.CompletedTask;
		return SendFeedbackInternalAsync(YStationFeedbackType.TrackStarted, yTrack, 0);
	}

	public Task SendTrackFinishedAsync(string trackId, double totalPlayedSeconds = 0)
	{
		if (_station == null || !_yTrackCache.TryGetValue(trackId, out var yTrack))
			return Task.CompletedTask;
		return SendFeedbackInternalAsync(YStationFeedbackType.TrackFinished, yTrack, totalPlayedSeconds);
	}

	public Task SendTrackSkippedAsync(string trackId, double totalPlayedSeconds = 0)
	{
		if (_station == null || !_yTrackCache.TryGetValue(trackId, out var yTrack))
			return Task.CompletedTask;
		return SendFeedbackInternalAsync(YStationFeedbackType.Skip, yTrack, totalPlayedSeconds);
	}

	private async Task SendFeedbackInternalAsync(YStationFeedbackType type, YTrack track, double totalPlayedSeconds)
	{
		try
		{
			await _api.Radio.SendStationFeedBackAsync(
				_storage, _station!, type, track, _currentBatchId ?? "", totalPlayedSeconds);
		}
		catch
		{
			// Фидбек не критичен — не роняем плеер при ошибке
		}
	}

	public void Reset()
	{
		_station = null;
		_currentBatchId = null;
		_lastTrackId = null;
		_yTrackCache.Clear();
	}
}
