using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Presenters.Impl;

public class MyWavePresenter : IMyWavePresenter
{
	private readonly IYandexRadioService _radioService;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ITrackRepository _trackRepository;
	private readonly ITracksPresenter _tracksPresenter;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly IPlayStatusPresenter _playStatusPresenter;

	public MyWavePresenter(
		IYandexRadioService radioService,
		ITrackInfoProvider trackInfoProvider,
		ITrackRepository trackRepository,
		ITracksPresenter tracksPresenter,
		IPlaybackQueue playbackQueue,
		IPlayStatusPresenter playStatusPresenter)
	{
		_radioService = radioService;
		_trackInfoProvider = trackInfoProvider;
		_trackRepository = trackRepository;
		_tracksPresenter = tracksPresenter;
		_playbackQueue = playbackQueue;
		_playStatusPresenter = playStatusPresenter;
	}

	public Task<Playlist?> StartMyWaveAsync()
		=> StartWaveInternalAsync(
			() => _radioService.StartMyWaveAsync(),
			"Моя волна",
			"Персональная радиостанция Яндекс.Музыки");

	public async Task<Playlist?> StartMyWaveFromTrackAsync(string trackId)
	{
		string description = "По треку";
		try
		{
			var track = await _trackInfoProvider.GetTrackInfoById(trackId);
			description = $"По треку: {track.Artist} — {track.Title}";
		}
		catch { }

		return await StartWaveInternalAsync(
			() => _radioService.StartTrackWaveAsync(trackId),
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

			var (trackIds, tracks) = await _radioService.FetchNextBatchAsync();
			if (trackIds.Count == 0)
			{
				_playStatusPresenter.SetPlayStatus("Нет треков в волне");
				return null;
			}

			foreach (var track in tracks)
				await _trackInfoProvider.SaveAsync(track);

			_trackRepository.UpdateMyWaveCache(tracks);

			var playlist = new Playlist(playlistName, PlaylistType.MyWave)
			{
				Description = description,
				TrackCount = trackIds.Count
			};

			await _tracksPresenter.LoadTracksFor(playlist);

			// Запускаем первый трек волны автоматически
			_playbackQueue.SetQueue(trackIds, 0);
			return playlist;
		}
		catch (Exception ex)
		{
			ex.Handle();
			return null;
		}
	}

	public async Task FetchMoreTracksAsync()
	{
		try
		{
			var (trackIds, tracks) = await _radioService.FetchNextBatchAsync();
			if (trackIds.Count == 0)
				return;

			foreach (var track in tracks)
				await _trackInfoProvider.SaveAsync(track);

			_trackRepository.AppendMyWaveCache(tracks);
			_playbackQueue.AddToQueue(trackIds);
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}

	public Task NotifyTrackStartedAsync(string trackId)
		=> _radioService.SendTrackStartedAsync(trackId);

	public Task NotifyTrackFinishedAsync(string trackId, double totalPlayedSeconds = 0)
		=> _radioService.SendTrackFinishedAsync(trackId, totalPlayedSeconds);

	public Task NotifyTrackSkippedAsync(string trackId, double totalPlayedSeconds = 0)
		=> _radioService.SendTrackSkippedAsync(trackId, totalPlayedSeconds);
}
