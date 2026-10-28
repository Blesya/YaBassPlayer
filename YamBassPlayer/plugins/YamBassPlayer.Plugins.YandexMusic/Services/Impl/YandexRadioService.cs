using Serilog;

using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Radio;
using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

public sealed class YandexRadioService : IYandexRadioService
{
	private const string MyWaveType = "user";
	private const string MyWaveTag = "onyourwave";

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

	public Task<bool> StartMyWaveAsync() => StartStationAsync(MyWaveType, MyWaveTag);

	/// <summary>
	/// Отправляет settings2 для персональной станции. Дескриптор станции
	/// собирается локально: эндпоинту нужны только type и tag, а дашборд
	/// рекомендаций может быть недоступен. Настройки применяются к следующему
	/// запросу треков этой станции.
	/// </summary>
	public async Task<bool> ApplyMyWaveSettingsAsync(MyWaveSettings settings)
	{
		try
		{
			await _api.Radio.SetStationSettings2Async(_storage, CreateStation(MyWaveType, MyWaveTag), new YStationSettings2 {
				Diversity = settings.Diversity,
				Language = settings.Language,
				MoodEnergy = settings.MoodEnergy
			});

			Log.Information("Настройки «Моей волны» применены: треки {Diversity}, настроение {MoodEnergy}, язык {Language}",
				settings.Diversity, settings.MoodEnergy, settings.Language);
			return true;
		}
		catch (Exception ex)
		{
			// Настройки — не повод не запускать волну: станция стартует с прежними.
			Log.Warning(ex, "Не удалось применить настройки «Моей волны»");
			return false;
		}
	}

	public async Task<bool> StartTrackWaveAsync(string seedTrackId)
	{
		bool started = await StartStationAsync("track", seedTrackId);
		if (!started)
			started = await StartMyWaveAsync();
		return started;
	}

	private async Task<bool> StartStationAsync(string type, string tag)
	{
		Reset();

		try
		{
			// Эндпоинт /rotor/station/{type}:{tag}/info у Яндекса отвечает 451, поэтому
			// станцию ищем в дашборде рекомендаций и в списке станций: только там есть
			// настоящий idForFrom, который Яндекс принимает в поле from у фидбека.
			_station = await FindStationInDashboardAsync(type, tag)
				?? await FindStationInListAsync(type, tag);

			if (_station == null)
			{
				Log.Warning("Станция {Type}:{Tag} не найдена в дашборде и списке станций, собираем дескриптор локально", type, tag);
				_station = CreateStation(type, tag);
			}

			Log.Information("Радиостанция {Type}:{Tag} готова", type, tag);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Не удалось запустить радиостанцию {Type}:{Tag}", type, tag);
			return false;
		}
	}

	private async Task<YStation?> FindStationInDashboardAsync(string type, string tag)
	{
		try
		{
			var response = await _api.Radio.GetStationsDashboardAsync(_storage);
			return response?.Result?.Stations?
				.FirstOrDefault(s => s.Station?.Id?.Type == type && s.Station.Id.Tag == tag);
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Не удалось получить дашборд станций, станция {Type}:{Tag} будет собрана локально", type, tag);
			return null;
		}
	}

	private async Task<YStation?> FindStationInListAsync(string type, string tag)
	{
		try
		{
			var response = await _api.Radio.GetStationsAsync(_storage);
			return response?.Result?
				.FirstOrDefault(s => s.Station?.Id?.Type == type && s.Station.Id.Tag == tag);
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Не удалось получить список станций, станция {Type}:{Tag} будет собрана локально", type, tag);
			return null;
		}
	}

	private static YStation CreateStation(string type, string tag)
	{
		return new YStation {
			Station = new YStationDescription {
				Id = new YStationId { Type = type, Tag = tag },
				// Канонический вид идентификатора станции — единственное, что можно
				// подставить в from, не зная настоящего idForFrom из ответа Яндекса.
				IdForFrom = $"{type}:{tag}",
				Name = "Моя волна"
			}
		};
	}

	public async Task<(IReadOnlyList<string> trackIds, IReadOnlyList<Track> tracks)> FetchNextBatchAsync()
	{
		if (_station == null)
		{
			Log.Warning("Нельзя получить треки волны: станция не запущена");
			return ([], []);
		}

		try
		{
			var response = await _api.Radio.GetStationTracksAsync(_storage, _station, _lastTrackId ?? "");
			var sequence = response?.Result;
			if (sequence == null)
			{
				Log.Warning("Станция {Station} вернула пустой ответ без последовательности треков", StationId);
				return ([], []);
			}

			_currentBatchId = sequence.BatchId;

			var yTracks = sequence.Sequence?
				.Select(s => s.Track)
				.Where(t => t != null)
				.ToList() ?? [];

			bool isFirstBatch = _lastTrackId == null;

			foreach (var yTrack in yTracks)
				_yTrackCache[yTrack.Id] = yTrack;

			_lastTrackId = yTracks.LastOrDefault()?.Id;

			// radioStarted относится к запуску станции, а не к треку, поэтому уходит без трека
			// и без batch-id. Не ждём ответ: фидбек не должен задерживать старт волны.
			if (isFirstBatch && yTracks.Count > 0)
				_ = SendFeedbackInternalAsync(YStationFeedbackType.RadioStarted, null, 0);

			var trackIds = (IReadOnlyList<string>)yTracks.Select(t => t.Id).ToList();
			var tracks = (IReadOnlyList<Track>)yTracks.Select(t => t.ToTrack()).ToList();

			Log.Information("Станция {Station}: получено треков {Count}", StationId, trackIds.Count);

			return (trackIds, tracks);
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Не удалось получить треки станции {Station}", StationId);
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

	private async Task SendFeedbackInternalAsync(YStationFeedbackType type, YTrack? track, double totalPlayedSeconds)
	{
		try
		{
			await _api.Radio.SendStationFeedBackAsync(
				_storage, _station!, type, track, _currentBatchId ?? "", totalPlayedSeconds);
		}
		catch (YErrorResponse error)
		{
			// Фидбек не критичен — не роняем плеер, но пишем причину из тела ответа:
			// у YErrorResponse нет Message, поэтому без Error.Name/Message лог бесполезен.
			Log.Debug("Фидбек {FeedbackType} станции {Station} отклонён: {ErrorName} — {ErrorMessage}",
				type, StationId, error.Error?.Name, error.Error?.Message);
		}
		catch (Exception ex)
		{
			// Фидбек не критичен — не роняем плеер при ошибке
			Log.Debug(ex, "Не удалось отправить фидбек {FeedbackType} станции {Station}", type, StationId);
		}
	}

	public void Reset()
	{
		_station = null;
		_currentBatchId = null;
		_lastTrackId = null;
		_yTrackCache.Clear();
	}

	private string StationId => _station?.Station?.IdForFrom ?? "unknown";
}
