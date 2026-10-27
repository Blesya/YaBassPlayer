using System.Threading;
using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Владелец состояния плейлистов и текущего состава. Держит три слоя:
/// снимок прошлого запуска (показывается сразу), слитую модель источников
/// (обновляется в фоне) и состав текущего плейлиста (из памяти → из БД → из источника).
/// Сеть никогда не нужна для первого экрана: <see cref="GetStartupSnapshotAsync"/> и
/// <see cref="GetCachedTracks"/> в неё не ходят.
/// </summary>
public class TrackRepository : ITrackRepository
{
	private readonly IMusicSourceRegistry _musicSourceRegistry;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly IHistoryService _historyService;
	private readonly ITrackRepositoryCache _cache;
	private readonly ILocalLibraryService _localLibraryService;
	private readonly PlaylistLoadStrategyResolver _strategyResolver;
	private readonly IAppPlaylistProvider _appPlaylistProvider;
	private readonly IPlaylistStateStore _playlistStateStore;
	private readonly IPlaylistCompositionStore _compositionStore;
	private readonly IEventBus _eventBus;
	private readonly IPlaylistRefreshStatus _refreshStatus;

	private readonly object _stateSync = new();
	private readonly object _playlistsSync = new();
	private readonly SemaphoreSlim _modelGate = new(1, 1);

	/// <summary>Плейлисты каждого источника: свежие после обновления, иначе из снимка.</summary>
	private readonly Dictionary<SourceId, List<Playlist>> _playlistsBySource = new();
	private List<Playlist> _appPlaylists = [];
	private bool _modelInitialized;

	private List<TrackKey> _trackIds = new();
	private Playlist? _currentPlaylist;
	private int _currentOffset;
	private PlaylistState? _persistedState;
	private bool _persistedStateLoaded;
	private CancellationTokenSource? _compositionRefreshCts;

	public TrackRepository(
		IMusicSourceRegistry musicSourceRegistry,
		ITrackInfoProvider trackInfoProvider,
		IHistoryService historyService,
		ITrackRepositoryCache cache,
		ILocalLibraryService localLibraryService,
		PlaylistLoadStrategyResolver strategyResolver,
		IAppPlaylistProvider appPlaylistProvider,
		IPlaylistStateStore playlistStateStore,
		IPlaylistCompositionStore compositionStore,
		IEventBus eventBus,
		IPlaylistRefreshStatus refreshStatus)
	{
		_musicSourceRegistry = musicSourceRegistry;
		_trackInfoProvider = trackInfoProvider;
		_historyService = historyService;
		_cache = cache;
		_localLibraryService = localLibraryService;
		_strategyResolver = strategyResolver;
		_appPlaylistProvider = appPlaylistProvider;
		_playlistStateStore = playlistStateStore;
		_compositionStore = compositionStore;
		_eventBus = eventBus;
		_refreshStatus = refreshStatus;

		_cache.MyWaveReplaced += OnMyWaveReplaced;
		_cache.MyWaveAppended += OnMyWaveAppended;
	}

	private readonly object _saveSync = new();
	private PlaylistState? _pendingSave;
	private bool _saveInProgress;

	public PlaylistState? GetPersistedSnapshot()
	{
		lock (_stateSync)
		{
			if (_persistedStateLoaded)
				return _persistedState;

			_persistedStateLoaded = true;
			_persistedState = _playlistStateStore.Load();
			return _persistedState;
		}
	}

	/// <inheritdoc />
	public async Task<PlaylistStartupSnapshot> GetStartupSnapshotAsync(CancellationToken ct = default)
	{
		Logging.LogBeforeCall();

		PlaylistState? persisted = GetPersistedSnapshot();
		await EnsureModelAsync(ct);

		List<Playlist> playlists = BuildMergedPlaylists();
		Playlist? lastPlaylist = persisted?.LastPlaylist is { } last && IsSourceRegistered(last)
			? last
			: null;

		Log.Information("Снимок для первого экрана: {Count} плейлистов", playlists.Count);
		Logging.LogAfterCall();

		return new PlaylistStartupSnapshot(
			playlists,
			lastPlaylist,
			persisted?.LastTrack,
			persisted?.SavedAt ?? default);
	}

	/// <summary>
	/// Заполняет модель из снимка и локальных плейлистов. Сеть не используется:
	/// ветки источников остаются такими, какими их видел пользователь при закрытии.
	/// </summary>
	private async Task EnsureModelAsync(CancellationToken ct)
	{
		lock (_playlistsSync)
		{
			if (_modelInitialized)
				return;
		}

		await _modelGate.WaitAsync(ct);
		try
		{
			lock (_playlistsSync)
			{
				if (_modelInitialized)
					return;
			}

			PlaylistState? persisted = GetPersistedSnapshot();

			List<Playlist> appPlaylists = (await _appPlaylistProvider.GetAppPlaylistsAsync(ct)).ToList();

			lock (_playlistsSync)
			{
				if (persisted is not null)
				{
					foreach (var group in persisted.Playlists
						.Where(IsSourceRegistered)
						.Where(p => p.SourceId is not null)
						.GroupBy(p => p.SourceId!.Value))
					{
						_playlistsBySource[group.Key] = group.ToList();
					}
				}

				_appPlaylists = appPlaylists;
				_modelInitialized = true;
			}
		}
		finally
		{
			_modelGate.Release();
		}
	}

	/// <summary>Плейлисты источника, которого больше нет в приложении, не показываем и не храним.</summary>
	private bool IsSourceRegistered(Playlist playlist)
		=> playlist.SourceId is not { } sourceId || _musicSourceRegistry.Get(sourceId) is not null;

	public async Task<IEnumerable<Playlist>> GetPlaylists(CancellationToken ct = default)
	{
		PlaylistRefreshResult result = await RefreshPlaylistsAsync(PlaylistRefreshScope.All, ct);
		return result.Playlists;
	}

	/// <inheritdoc />
	public async Task<PlaylistRefreshResult> RefreshPlaylistsAsync(
		PlaylistRefreshScope scope,
		CancellationToken ct = default)
	{
		Logging.LogBeforeCall();
		ct.ThrowIfCancellationRequested();

		await EnsureModelAsync(ct);

		if (scope is PlaylistRefreshScope.AppPlaylists or PlaylistRefreshScope.All)
		{
			List<Playlist> appPlaylists = (await _appPlaylistProvider.GetAppPlaylistsAsync(ct)).ToList();
			lock (_playlistsSync)
			{
				_appPlaylists = appPlaylists;
			}
		}

		var sourceResults = new List<SourceRefreshResult>();
		if (scope == PlaylistRefreshScope.All)
		{
			// Источники опрашиваются параллельно: медленный не задерживает остальные.
			IMusicSource[] sources = _musicSourceRegistry.Sources.ToArray();
			SourceRefreshResult[] results = await Task.WhenAll(
				sources.Select(source => RefreshSourceAsync(source, ct)));
			sourceResults.AddRange(results);
		}
		else
		{
			foreach (IMusicSource source in _musicSourceRegistry.Sources)
				sourceResults.Add(SourceRefreshResult.Skipped(source.Id));
		}

		List<Playlist> merged = BuildMergedPlaylists();
		PersistState(merged);
		await PruneCompositionsAsync(merged, ct);

		Log.Information(
			"Плейлисты обновлены: {Count} (источники: {Sources})",
			merged.Count,
			string.Join(", ", sourceResults.Select(r =>
				$"{r.Source.Value}:{(r.Succeeded ? r.PlaylistCount.ToString() : "ошибка")}")));
		Logging.LogAfterCall();

		return new PlaylistRefreshResult(merged, sourceResults);
	}

	/// <summary>
	/// Обновляет один источник. Успех заменяет его ветку, ошибка оставляет прежнюю:
	/// снимок не должен обеднеть из-за одного неудачного запроса.
	/// </summary>
	private async Task<SourceRefreshResult> RefreshSourceAsync(IMusicSource source, CancellationToken ct)
	{
		if (source.State != SourceState.Ready)
		{
			// Неготовый источник не дёргаем: его плейлисты остаются из снимка.
			return SourceRefreshResult.Skipped(source.Id);
		}

		try
		{
			List<Playlist> playlists = (await source.GetPlaylistsAsync(ct))
				.Where(playlist => playlist.Category == PlaylistCategory.Data)
				.Where(playlist => playlist.SourceId is not null)
				.ToList();

			lock (_playlistsSync)
			{
				_playlistsBySource[source.Id] = playlists;
			}

			_cache.ClearPlaylistTrackIds(source.Id);
			_refreshStatus.ReportSuccess(source.Id, DateTime.UtcNow);
			return SourceRefreshResult.Success(source.Id, playlists.Count);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Не удалось обновить плейлисты источника {Source}", source.Id);
			_refreshStatus.ReportFailure(source.Id, exception.Message, DateTime.UtcNow);
			return SourceRefreshResult.Failure(source.Id, exception.Message);
		}
	}

	private List<Playlist> BuildMergedPlaylists()
	{
		lock (_playlistsSync)
		{
			return _playlistsBySource
				.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
				.SelectMany(pair => pair.Value)
				.Concat(_appPlaylists)
				.ToList();
		}
	}

	/// <summary>Сохраняет актуальный состав плейлистов источников для следующего запуска.</summary>
	private void PersistState(IReadOnlyList<Playlist> playlists)
	{
		List<Playlist> current = playlists
			.Where(p => p.Category == PlaylistCategory.Data)
			.ToList();

		PlaylistState? previous = GetPersistedSnapshot();
		Playlist? lastPlaylist = previous?.LastPlaylist is { } last && IsSourceRegistered(last)
			? last
			: null;

		Playlist? currentPlaylist;
		lock (_stateSync)
		{
			currentPlaylist = _currentPlaylist;
		}

		var state = new PlaylistState
		{
			Playlists = current,
			LastPlaylist = lastPlaylist ?? currentPlaylist,
			LastTrack = previous?.LastTrack,
			SavedAt = DateTime.UtcNow
		};

		lock (_stateSync)
		{
			_persistedState = state;
			_persistedStateLoaded = true;
		}

		_playlistStateStore.Save(state);
	}

	/// <summary>Удаляет составы плейлистов, которых больше нет в успешно обновлённом дереве.</summary>
	private async Task PruneCompositionsAsync(IReadOnlyList<Playlist> playlists, CancellationToken ct)
	{
		try
		{
			HashSet<string> valid = playlists
				.Select(playlist => playlist.CacheKey)
				.ToHashSet(StringComparer.Ordinal);

			IReadOnlyCollection<string> stored = await _compositionStore.GetKeysAsync(ct);
			List<string> stale = stored
				.Where(key => !valid.Contains(key))
				.ToList();

			if (stale.Count > 0)
			{
				await _compositionStore.RemoveAsync(stale, ct);
				Log.Debug("Удалены составы исчезнувших плейлистов: {Count}", stale.Count);
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Не удалось почистить составы плейлистов");
		}
	}

	/// <summary>Запоминает выбранный плейлист, чтобы восстановить выделение при следующем запуске.</summary>
	private void PersistLastPlaylist(Playlist playlist)
	{
		// Временные плейлисты (поиск, очередь, «Моя волна») не переживают перезапуск.
		if (playlist.Category == PlaylistCategory.Transient)
			return;

		PlaylistState? state;
		lock (_stateSync)
		{
			state = _persistedState;
			if (state is null || state.Playlists.Count == 0)
				return;

			if (state.LastPlaylist is { } last && last.HasSameIdentity(playlist))
				return;

			_persistedState = new PlaylistState
			{
				Playlists = state.Playlists,
				LastPlaylist = playlist,
				LastTrack = state.LastTrack,
				SavedAt = DateTime.UtcNow
			};
			state = _persistedState;
		}

		_playlistStateStore.Save(state);
	}

	/// <inheritdoc />
	public void SetLastPlayedTrack(TrackKey key)
	{
		if (string.IsNullOrWhiteSpace(key.TrackId))
			return;

		PlaylistState? state;
		lock (_stateSync)
		{
			state = _persistedState;
			if (state is null || state.LastTrack == key)
				return;

			_persistedState = new PlaylistState
			{
				Playlists = state.Playlists,
				LastPlaylist = state.LastPlaylist,
				LastTrack = key,
				SavedAt = DateTime.UtcNow
			};
			state = _persistedState;
		}

		// Запись снимка — это сериализация всего дерева плейлистов и файл: на 200
		// плейлистах ~18 мс (замер в PerfBenchmarks). Держать это на UI-потоке в момент
		// старта трека нельзя, поэтому пишем в фоне одним фоновым писателем.
		SaveInBackground(state);
	}

	/// <summary>
	/// Фоновый писатель снимка: одновременно идёт не больше одной записи, а если
	/// состояние успело смениться, пишется самое свежее (промежуточные не нужны).
	/// </summary>
	private void SaveInBackground(PlaylistState state)
	{
		lock (_saveSync)
		{
			_pendingSave = state;
			if (_saveInProgress)
				return;

			_saveInProgress = true;
		}

		_ = Task.Run(SavePendingLoop);
	}

	private void SavePendingLoop()
	{
		while (true)
		{
			PlaylistState? state;
			lock (_saveSync)
			{
				state = _pendingSave;
				_pendingSave = null;
				if (state is null)
				{
					_saveInProgress = false;
					return;
				}
			}

			try
			{
				_playlistStateStore.Save(state);
			}
			catch
			{
				// Снимок не критичен для работы: ошибку записи проглатываем.
			}
		}
	}

	public async Task SetPlaylist(Playlist playlist, CancellationToken ct = default)
	{
		Logging.LogBeforeCall();

		ct.ThrowIfCancellationRequested();
		try
		{
			(List<TrackKey> keys, CompositionOrigin origin) = await LoadTrackKeysAsync(playlist, ct);

			lock (_stateSync)
			{
				_trackIds = keys;
				_currentOffset = 0;
				_currentPlaylist = playlist;
			}

			PersistLastPlaylist(playlist);
			Logging.LogAfterCall();

			// Состав пришёл из постоянного хранилища — тихо сверяем его с источником.
			// Из памяти он свежий: туда попадает только что полученный ответ источника.
			if (origin == CompositionOrigin.Store)
				StartCompositionRefresh(playlist);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			// Ошибку источника уже залогировал он сам; модальный диалог на загрузке
			// списка не нужен — пользователь увидит пустой список и маркер кеша.
			Log.Warning(exception, "Не удалось загрузить состав плейлиста «{Title}»", playlist.Title);
		}
	}

	private enum CompositionOrigin
	{
		/// <summary>Ответ источника, полученный в этом запуске.</summary>
		Memory,

		/// <summary>Постоянное хранилище — нужно сверить с источником в фоне.</summary>
		Store,

		/// <summary>Источник ответил только что.</summary>
		Source
	}

	private async Task<(List<TrackKey> Keys, CompositionOrigin Origin)> LoadTrackKeysAsync(
		Playlist playlist,
		CancellationToken ct)
	{
		if (playlist.SourceId is null)
		{
			List<TrackKey> strategyKeys = (await _strategyResolver.Resolve(playlist.Kind).LoadTrackIdsAsync(playlist))
				.ToList();
			return (strategyKeys, CompositionOrigin.Source);
		}

		SourceId sourceId = playlist.SourceId.Value;

		if (playlist.Category == PlaylistCategory.Transient)
		{
			if (playlist.Context == PlaylistContext.Radio)
				return (_cache.MyWaveTracks.Select(track => track.Key).ToList(), CompositionOrigin.Source);

			return (_cache.TryGetSearchTrackIds(sourceId, out IReadOnlyList<TrackKey> cached)
				? cached.ToList()
				: [], CompositionOrigin.Source);
		}

		// Состав обычного плейлиста источника спрашиваем у источника один раз: повторный
		// выбор берёт его из памяти. Сбрасывается при успешном обновлении источника.
		if (_cache.TryGetPlaylistTrackIds(sourceId, playlist.CacheKey, out IReadOnlyList<TrackKey> cachedKeys))
			return (cachedKeys.ToList(), CompositionOrigin.Memory);

		IReadOnlyList<TrackKey>? stored = await _compositionStore.TryLoadAsync(playlist.CacheKey, ct);
		if (stored is { Count: > 0 })
		{
			_cache.SetPlaylistTrackIds(sourceId, playlist.CacheKey, stored);
			Log.Debug("Состав плейлиста «{Title}» восстановлен из БД: {Count}", playlist.Title, stored.Count);
			return (stored.ToList(), CompositionOrigin.Store);
		}

		IMusicSource source = _musicSourceRegistry.GetRequired(sourceId);
		IReadOnlyList<string> ids = await source.GetPlaylistTrackIdsAsync(playlist, ct);
		List<TrackKey> trackKeys = ids.Select(id => new TrackKey(sourceId, id)).ToList();

		// Пустой ответ не кешируем: источник мог не ответить из-за ошибки (она уже
		// залогирована), и плейлист иначе остался бы пустым до обновления дерева.
		if (trackKeys.Count > 0)
		{
			_cache.SetPlaylistTrackIds(sourceId, playlist.CacheKey, trackKeys);
			await SaveCompositionAsync(playlist.CacheKey, trackKeys, ct);
		}

		return (trackKeys, CompositionOrigin.Source);
	}

	private async Task SaveCompositionAsync(
		string playlistKey,
		IReadOnlyList<TrackKey> keys,
		CancellationToken ct)
	{
		try
		{
			await _compositionStore.SaveAsync(playlistKey, keys, ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Не удалось сохранить состав плейлиста {Key}", playlistKey);
		}
	}

	/// <summary>
	/// Сверяет показанный из кеша состав с источником. Если он изменился — обновляет
	/// память, хранилище и сообщает презентеру списка треков.
	/// </summary>
	private void StartCompositionRefresh(Playlist playlist)
	{
		if (playlist.SourceId is not { } sourceId
			|| playlist.Category == PlaylistCategory.Transient)
		{
			return;
		}

		IMusicSource? source = _musicSourceRegistry.Get(sourceId);
		if (source is null || source.State != SourceState.Ready)
			return;

		CancellationTokenSource cts;
		lock (_stateSync)
		{
			_compositionRefreshCts?.Cancel();
			_compositionRefreshCts?.Dispose();
			cts = new CancellationTokenSource();
			_compositionRefreshCts = cts;
		}

		_ = Task.Run(() => RefreshCompositionAsync(source, playlist, cts.Token), cts.Token);
	}

	private async Task RefreshCompositionAsync(
		IMusicSource source,
		Playlist playlist,
		CancellationToken ct)
	{
		try
		{
			IReadOnlyList<string> ids = await source.GetPlaylistTrackIdsAsync(playlist, ct);
			ct.ThrowIfCancellationRequested();

			if (ids.Count == 0)
				return;

			SourceId sourceId = playlist.SourceId!.Value;
			List<TrackKey> keys = ids.Select(id => new TrackKey(sourceId, id)).ToList();

			lock (_stateSync)
			{
				if (_currentPlaylist is null || !_currentPlaylist.HasSameIdentity(playlist))
					return;

				if (_trackIds.Count == keys.Count && _trackIds.SequenceEqual(keys))
					return;
			}

			_cache.SetPlaylistTrackIds(sourceId, playlist.CacheKey, keys);
			await SaveCompositionAsync(playlist.CacheKey, keys, ct);

			lock (_stateSync)
			{
				if (_currentPlaylist is null || !_currentPlaylist.HasSameIdentity(playlist))
					return;

				_trackIds = keys;
				_currentOffset = 0;
			}

			Log.Information(
				"Состав плейлиста «{Title}» обновлён в фоне: {Count}",
				playlist.Title,
				keys.Count);
			_eventBus.Publish(new PlaylistCompositionUpdatedEvent(
				playlist.SourceId,
				playlist.CacheKey,
				keys.Count));
		}
		catch (OperationCanceledException)
		{
			// Пользователь уже выбрал другой плейлист — это нормальный путь.
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Не удалось обновить состав плейлиста «{Title}»", playlist.Title);
		}
	}

	public async Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			List<TrackKey> slice;
			lock (_stateSync)
			{
				int start = Math.Clamp(_currentOffset, 0, _trackIds.Count);
				int count = Math.Clamp(_trackIds.Count - start, 0, Math.Max(0, tracksPerBatch));
				slice = _trackIds.GetRange(start, count);
				_currentOffset += tracksPerBatch;
			}

			IEnumerable<Track> tracks = await _trackInfoProvider.GetTracksInfoByIds(slice);
			return tracks.ToList();
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Не удалось получить следующую порцию треков");
			return [];
		}
	}

	public IReadOnlyList<TrackKey> GetAllTrackIds()
	{
		lock (_stateSync)
		{
			return _trackIds.AsReadOnly();
		}
	}

	/// <inheritdoc />
	public async Task<IEnumerable<Track>> GetCachedTracks(int maxCount, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		List<TrackKey> keys;
		lock (_stateSync)
		{
			keys = _trackIds.Take(Math.Max(0, maxCount)).ToList();
			// Первая порция показана — следующая пагинация начинается после неё.
			_currentOffset = keys.Count;
		}

		if (keys.Count == 0)
			return [];

		return await _trackInfoProvider.GetCachedTracksByIds(keys);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<Track>> LoadTracksInfo(int maxCount, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		List<TrackKey> keys;
		lock (_stateSync)
		{
			keys = _trackIds.Take(Math.Max(0, maxCount)).ToList();
		}

		if (keys.Count == 0)
			return [];

		return (await _trackInfoProvider.GetTracksInfoByIds(keys)).ToList();
	}

	private void OnMyWaveReplaced()
	{
		lock (_stateSync)
		{
			if (_currentPlaylist?.Context != PlaylistContext.Radio)
				return;

			_trackIds.Clear();
			_trackIds.AddRange(_cache.MyWaveTracks.Select(t => t.Key));
		}
	}

	private void OnMyWaveAppended()
	{
		lock (_stateSync)
		{
			if (_currentPlaylist?.Context != PlaylistContext.Radio)
				return;

			_trackIds.AddRange(_cache.MyWaveTracks.Select(t => t.Key));
		}
	}
}
