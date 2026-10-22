using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Presenters.Impl;

/// <summary>
/// «Моя волна»: запускает радио источника, складывает батчи в кэш и очередь.
/// Источник выбирается по возможности <see cref="IRadioSource"/>, поэтому
/// презентер не знает, чьё это радио.
/// </summary>
public class MyWavePresenter : IMyWavePresenter
{
	private readonly IMusicSourceRegistry _sources;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ITrackRepositoryCache _trackRepositoryCache;
	private readonly ITracksPresenter _tracksPresenter;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly IPlayStatusPresenter _playStatusPresenter;
	private readonly IErrorHandler _errorHandler;

	private IRadioSource? _radio;
	private SourceId _radioSourceId;

	public MyWavePresenter(
		IMusicSourceRegistry sources,
		ITrackInfoProvider trackInfoProvider,
		ITrackRepositoryCache trackRepositoryCache,
		ITracksPresenter tracksPresenter,
		IPlaybackQueue playbackQueue,
		IPlayStatusPresenter playStatusPresenter,
		IErrorHandler errorHandler)
	{
		_sources = sources;
		_trackInfoProvider = trackInfoProvider;
		_trackRepositoryCache = trackRepositoryCache;
		_tracksPresenter = tracksPresenter;
		_playbackQueue = playbackQueue;
		_playStatusPresenter = playStatusPresenter;
		_errorHandler = errorHandler;
	}

	public Task<Playlist?> StartMyWaveAsync()
	{
		var found = FindRadio(_playbackQueue.CurrentTrack?.SourceId);
		if (found is null)
			return Task.FromResult<Playlist?>(null);

		ActivateRadio(found.Value);
		return StartWaveInternalAsync(
			() => found.Value.Radio.StartPersonalRadioAsync(),
			"Моя волна",
			"Персональная радиостанция");
	}

	public async Task<Playlist?> StartMyWaveFromTrackAsync(TrackKey key)
	{
		var found = FindRadio(key.SourceId);
		if (found is null)
			return null;

		ActivateRadio(found.Value);

		string description = "По треку";
		try
		{
			var track = await _trackInfoProvider.GetTrackInfoById(key);
			description = $"По треку: {track.Artist} — {track.Title}";
		}
		catch { }

		return await StartWaveInternalAsync(
			() => found.Value.Radio.StartTrackRadioAsync(key),
			"Моя волна",
			description);
	}

	private async Task<Playlist?> StartWaveInternalAsync(
		Func<Task<bool>> startStation,
		string playlistName,
		string description)
	{
		try
		{
			_playStatusPresenter.SetPlayStatus("Запускаем Мою волну...");

			bool started = await startStation();
			if (!started)
			{
				_playStatusPresenter.SetPlayStatus("Не удалось запустить Мою волну");
				return null;
			}

			RadioBatch batch = await _radio!.FetchNextBatchAsync();
			if (batch.Keys.Count == 0)
			{
				_playStatusPresenter.SetPlayStatus("Нет треков в волне");
				return null;
			}

			foreach (var track in batch.Tracks)
				await _trackInfoProvider.SaveAsync(track);

			_trackRepositoryCache.ReplaceMyWaveTracks(batch.Tracks);

			var playlist = new Playlist("my-wave", playlistName)
			{
				SourceId = _radioSourceId,
				Category = PlaylistCategory.Transient,
				Context = PlaylistContext.Radio,
				Description = description,
				TrackCount = batch.Keys.Count
			};

			await _tracksPresenter.LoadTracksFor(playlist);

			// Запускаем первый трек волны автоматически
			_playbackQueue.SetQueue(batch.Keys, 0);
			return playlist;
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
			return null;
		}
	}

	public async Task FetchMoreTracksAsync()
	{
		if (_radio is null)
			return;

		try
		{
			RadioBatch batch = await _radio.FetchNextBatchAsync();
			if (batch.Keys.Count == 0)
				return;

			foreach (var track in batch.Tracks)
				await _trackInfoProvider.SaveAsync(track);

			_trackRepositoryCache.AppendMyWaveTracks(batch.Tracks);
			_playbackQueue.AddToQueue(batch.Keys);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	public Task NotifyTrackStartedAsync(TrackKey key)
		=> _radio?.SendTrackStartedAsync(key) ?? Task.CompletedTask;

	public Task NotifyTrackFinishedAsync(TrackKey key, double totalPlayedSeconds = 0)
		=> _radio?.SendTrackFinishedAsync(key, totalPlayedSeconds) ?? Task.CompletedTask;

	public Task NotifyTrackSkippedAsync(TrackKey key, double totalPlayedSeconds = 0)
		=> _radio?.SendTrackSkippedAsync(key, totalPlayedSeconds) ?? Task.CompletedTask;

	private (IMusicSource Source, IRadioSource Radio)? FindRadio(SourceId? preferred)
	{
		if (preferred is { } sourceId
			&& _sources.Get(sourceId) is { } preferredSource
			&& preferredSource is IRadioSource preferredRadio)
		{
			return (preferredSource, preferredRadio);
		}

		foreach (IMusicSource source in _sources.Sources)
		{
			if (source is IRadioSource radio)
				return (source, radio);
		}

		return null;
	}

	private void ActivateRadio((IMusicSource Source, IRadioSource Radio) found)
	{
		_radio = found.Radio;
		_radioSourceId = found.Source.Id;
	}
}
