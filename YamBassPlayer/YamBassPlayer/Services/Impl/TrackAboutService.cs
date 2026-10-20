using System.Collections.Concurrent;
using Serilog;
using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Artist;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Track;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Собирает данные для окна «О треке» из свежих ответов API.
/// Кэш только в памяти и короткий: доступность и метки могут меняться.
/// </summary>
public sealed class TrackAboutService : ITrackAboutService
{
	private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);
	private static readonly TimeSpan DisclaimersTtl = TimeSpan.FromMinutes(10);

	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly ITrackCatalog _trackCatalog;
	private readonly ITrackSourceDetector _sourceDetector;

	private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
	private readonly SemaphoreSlim _disclaimersLock = new(1, 1);

	private IReadOnlyDictionary<string, YDisclaimer> _disclaimers = new Dictionary<string, YDisclaimer>();
	private DateTimeOffset _disclaimersLoadedAt = DateTimeOffset.MinValue;

	public TrackAboutService(
		YandexMusicApi api,
		AuthStorage storage,
		ITrackCatalog trackCatalog,
		ITrackSourceDetector sourceDetector)
	{
		_api = api;
		_storage = storage;
		_trackCatalog = trackCatalog;
		_sourceDetector = sourceDetector;
	}

	public async Task<TrackAboutInfo> GetAsync(string trackId, bool forceRefresh = false)
	{
		if (!forceRefresh
			&& _cache.TryGetValue(trackId, out CacheEntry? cached)
			&& cached.ExpiresAt > DateTimeOffset.UtcNow)
		{
			return cached.Info;
		}

		TrackAboutInfo info = await LoadAsync(trackId);
		_cache[trackId] = new CacheEntry(info, DateTimeOffset.UtcNow.Add(CacheTtl));
		return info;
	}

	private async Task<TrackAboutInfo> LoadAsync(string trackId)
	{
		Track? basic = null;
		try
		{
			basic = await _trackCatalog.GetAsync(trackId);
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Не удалось прочитать базовую информацию о треке {TrackId}", trackId);
		}

		if (_sourceDetector.IsLocal(trackId))
			return BuildLocal(trackId, basic);

		try
		{
			YResponse<List<YTrack>>? response = await _api.Track.GetAsync(_storage, trackId);
			YTrack? yTrack = response?.Result?.FirstOrDefault();

			if (yTrack is null)
				return BuildFallback(trackId, basic, "Не удалось получить данные из Яндекс.Музыки");

			return await BuildFromYandexAsync(trackId, basic, yTrack);
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Не удалось получить информацию о треке {TrackId}", trackId);
			return BuildFallback(trackId, basic, "Не удалось получить данные из Яндекс.Музыки");
		}
	}

	private async Task<TrackAboutInfo> BuildFromYandexAsync(string trackId, Track? basic, YTrack yTrack)
	{
		IReadOnlyDictionary<string, YDisclaimer> disclaimers = await GetDisclaimersAsync();
		IReadOnlyList<TrackAboutLabel> labels = TrackAboutLabelResolver.Resolve(yTrack.Disclaimers, disclaimers);
		IReadOnlyList<TrackAboutCredit> credits = await LoadCreditsAsync(trackId);
		string? artistDescription = await LoadArtistDescriptionAsync(yTrack);

		bool isAvailable = yTrack.Available;
		string? unavailableReason = null;
		string? substituteTitle = null;

		if (!isAvailable)
		{
			if (yTrack.Substituted is { } substituted)
			{
				unavailableReason = "Трек недоступен в вашем регионе";
				substituteTitle = $"{FormatArtists(substituted.Artists)} — {substituted.Title}";
			}
			else
			{
				unavailableReason = TrackAboutLabelResolver.FindBlockingReason(labels)
					?? (string.IsNullOrWhiteSpace(yTrack.Error) ? "Трек недоступен" : yTrack.Error.Trim());
			}
		}

		return new TrackAboutInfo
		{
			TrackId = trackId,
			Title = FirstNonEmpty(yTrack.Title, basic?.Title, trackId),
			Artist = FirstNonEmpty(FormatArtists(yTrack.Artists), basic?.Artist, "Неизвестный исполнитель"),
			Album = FirstNonEmpty(yTrack.Albums?.FirstOrDefault()?.Title, basic?.Album),
			IsAvailable = isAvailable,
			UnavailableReason = unavailableReason,
			SubstituteTitle = substituteTitle,
			Labels = labels,
			Credits = credits,
			ArtistDescription = artistDescription,
		};
	}

	private async Task<IReadOnlyList<TrackAboutCredit>> LoadCreditsAsync(string trackId)
	{
		try
		{
			YResponse<YTrackCredits>? response = await _api.Track.GetCreditsAsync(_storage, trackId);
			return response?.Result?.Credits?
				.Where(credit => !string.IsNullOrWhiteSpace(credit.Title) && !string.IsNullOrWhiteSpace(credit.Value))
				.Select(credit => new TrackAboutCredit(credit.Title.Trim(), credit.Value.Trim()))
				.ToList() ?? [];
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Не удалось получить кредиты трека {TrackId}", trackId);
			return [];
		}
	}

	private async Task<string?> LoadArtistDescriptionAsync(YTrack track)
	{
		YArtist? artist = track.Artists?.FirstOrDefault();
		if (artist is null || string.IsNullOrWhiteSpace(artist.Id))
			return null;

		try
		{
			YArtistAbout? about = await _api.Artist.GetAboutAsync(_storage, artist.Id);
			string? text = about?.Description;

			if (string.IsNullOrWhiteSpace(text))
				text = about?.Artist?.Description?.Text;

			return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Не удалось получить описание исполнителя {ArtistId}", artist.Id);
			return null;
		}
	}

	private async Task<IReadOnlyDictionary<string, YDisclaimer>> GetDisclaimersAsync()
	{
		if (DateTimeOffset.UtcNow - _disclaimersLoadedAt < DisclaimersTtl)
			return _disclaimers;

		await _disclaimersLock.WaitAsync();
		try
		{
			if (DateTimeOffset.UtcNow - _disclaimersLoadedAt < DisclaimersTtl)
				return _disclaimers;

			List<YDisclaimer>? disclaimers = await _api.Track.GetDisclaimersAsync(_storage);
			_disclaimers = (disclaimers ?? [])
				.Where(disclaimer => !string.IsNullOrWhiteSpace(disclaimer.Id))
				.GroupBy(disclaimer => disclaimer.Id)
				.ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
			_disclaimersLoadedAt = DateTimeOffset.UtcNow;

			return _disclaimers;
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Не удалось получить словарь дисклеймеров");
			return _disclaimers;
		}
		finally
		{
			_disclaimersLock.Release();
		}
	}

	private static TrackAboutInfo BuildLocal(string trackId, Track? basic)
	{
		return new TrackAboutInfo
		{
			TrackId = trackId,
			Title = FirstNonEmpty(basic?.Title, Path.GetFileName(trackId)),
			Artist = FirstNonEmpty(basic?.Artist, "Неизвестный исполнитель"),
			Album = basic?.Album,
			IsAvailable = true,
		};
	}

	private static TrackAboutInfo BuildFallback(string trackId, Track? basic, string note)
	{
		return new TrackAboutInfo
		{
			TrackId = trackId,
			Title = FirstNonEmpty(basic?.Title, trackId),
			Artist = FirstNonEmpty(basic?.Artist, "Неизвестный исполнитель"),
			Album = basic?.Album,
			IsAvailable = true,
			Note = note,
		};
	}

	private static string FormatArtists(IReadOnlyList<YArtist>? artists)
	{
		return artists is null || artists.Count == 0
			? string.Empty
			: string.Join(", ", artists
				.Select(artist => artist.Name)
				.Where(name => !string.IsNullOrWhiteSpace(name)));
	}

	private static string FirstNonEmpty(params string?[] values)
	{
		return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim()
			?? string.Empty;
	}

	private sealed record CacheEntry(TrackAboutInfo Info, DateTimeOffset ExpiresAt);
}
