using System.Threading;
using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

/// <summary>
/// Дерево плейлистов по принципу «сначала показать, потом обновить»: первый экран
/// строится из снимка (включая ветки ещё не готовых источников) и не ждёт сеть,
/// а актуализация идёт в фоне одним коалесцированным проходом.
/// </summary>
public class PlaylistsPresenter : IPlaylistsPresenter
{
	private readonly IPlaylistsView _view;
	private readonly ITrackRepository _trackRepository;
	private readonly IPlaylistTreeComposer _playlistTreeComposer;
	private readonly IErrorHandler _errorHandler;

	private readonly object _stateSync = new();
	private readonly object _refreshSync = new();
	private readonly SemaphoreSlim _applyGate = new(1, 1);

	private Playlist? _currentPlaylist;

	/// <summary>Плейлист из снимка, который ещё не найден в дереве (например, «Загруженные»).</summary>
	private Playlist? _restoredLastPlaylist;

	/// <summary>Пользователь сам выбрал плейлист — прошлый выбор больше не навязываем.</summary>
	private bool _userChosePlaylist;

	private PlaylistRefreshScope _pendingScope = PlaylistRefreshScope.None;
	private bool _pendingInvalidateStatic;
	private bool _refreshRunning;

	/// <summary>Сколько раз дерево уже отдавалось вьюхе (первый кадр — 0).</summary>
	private int _appliedCount;

	/// <summary>
	/// Задача текущего фонового прохода. Нужна тестам, чтобы дождаться обновления,
	/// и вызывающему коду, который хочет знать, когда дерево актуализировано.
	/// </summary>
	public Task? PendingRefresh { get; private set; }

	public event Action<Playlist>? PlaylistChosen;

	public PlaylistsPresenter(
		IPlaylistsView view,
		ITrackRepository trackRepository,
		IPlaylistTreeComposer playlistTreeComposer,
		IErrorHandler errorHandler)
	{
		_view = view;
		_trackRepository = trackRepository;
		_playlistTreeComposer = playlistTreeComposer;
		_errorHandler = errorHandler;

		_view.PlaylistSelected += OnPlaylistSelected;

		// DO NOT call LoadPlaylists() here — call InitializeAsync() explicitly
	}

	public async Task InitializeAsync(CancellationToken ct = default)
	{
		Logging.LogBeforeCall();

		ct.ThrowIfCancellationRequested();
		try
		{
			// 1. Первый кадр целиком из снимка: сохранённые ветки источников и локальные
			//    плейлисты приложения. Сеть на этом пути не используется.
			PlaylistStartupSnapshot snapshot = await _trackRepository.GetStartupSnapshotAsync(ct);

			lock (_stateSync)
			{
				_restoredLastPlaylist ??= snapshot.LastPlaylist;
			}

			if (snapshot.Playlists.Count > 0)
				Log.Information("Плейлисты восстановлены из снимка: {Count}", snapshot.Playlists.Count);

			await ApplyPlaylistsAsync(
				snapshot.Playlists,
				snapshot.LastPlaylist,
				ct,
				selectIfNotFound: true,
				invalidateStaticCache: true,
				onlyIfFirst: true);

			// 2. Актуализация — в фоне, экран её не ждёт.
			_ = RequestRefreshAsync(PlaylistRefreshScope.All, invalidateStaticCache: false);

			Logging.LogAfterCall();
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	public void LoadPlaylistTree()
		=> _ = RequestRefreshAsync(PlaylistRefreshScope.All, invalidateStaticCache: true);

	public void RefreshAppPlaylists()
		=> _ = RequestRefreshAsync(PlaylistRefreshScope.AppPlaylists, invalidateStaticCache: false);

	/// <summary>
	/// Ставит обновление в очередь. Пока проход идёт, повторные запросы не плодят
	/// параллельные опросы источников: они схлопываются в один следующий проход.
	/// </summary>
	private Task RequestRefreshAsync(PlaylistRefreshScope scope, bool invalidateStaticCache)
	{
		lock (_refreshSync)
		{
			_pendingScope = Combine(_pendingScope, scope);
			// Сброс статических веток нужен только по явному запросу (F5, скан, плагин).
			if (invalidateStaticCache)
				_pendingInvalidateStatic = true;

			if (_refreshRunning)
				return PendingRefresh ?? Task.CompletedTask;

			_refreshRunning = true;
			PendingRefresh = Task.Run(RefreshLoopAsync);
			return PendingRefresh;
		}
	}

	private static PlaylistRefreshScope Combine(PlaylistRefreshScope left, PlaylistRefreshScope right)
		=> left == PlaylistRefreshScope.All || right == PlaylistRefreshScope.All
			? PlaylistRefreshScope.All
			: left == PlaylistRefreshScope.AppPlaylists || right == PlaylistRefreshScope.AppPlaylists
				? PlaylistRefreshScope.AppPlaylists
				: PlaylistRefreshScope.None;

	private async Task RefreshLoopAsync()
	{
		while (true)
		{
			PlaylistRefreshScope scope;
			bool invalidateStatic;

			lock (_refreshSync)
			{
				scope = _pendingScope;
				invalidateStatic = _pendingInvalidateStatic;
				_pendingScope = PlaylistRefreshScope.None;
				_pendingInvalidateStatic = false;

				if (scope == PlaylistRefreshScope.None)
				{
					_refreshRunning = false;
					return;
				}
			}

			try
			{
				PlaylistRefreshResult result = await _trackRepository.RefreshPlaylistsAsync(
					scope,
					CancellationToken.None);

				if (result.Playlists.Count == 0)
					continue;

				Playlist? preferred;
				bool selectIfNotFound;
				lock (_stateSync)
				{
					preferred = _userChosePlaylist
						? _currentPlaylist
						: _restoredLastPlaylist ?? _currentPlaylist;
					selectIfNotFound = _currentPlaylist is null && _restoredLastPlaylist is null;
				}

				await ApplyPlaylistsAsync(
					result.Playlists,
					preferred,
					CancellationToken.None,
					selectIfNotFound,
					invalidateStatic);
			}
			catch (Exception ex)
			{
				// Фоновое обновление не должно показывать модальный диалог поверх игры.
				Log.Warning(ex, "Не удалось обновить дерево плейлистов в фоне");
			}
		}
	}

	/// <summary>
	/// Строит дерево из плейлистов, отдаёт его вьюхе и выбирает плейлист:
	/// <paramref name="preferred"/> (с восстановлением прошлого выбора), иначе первый доступный.
	/// Повторно <see cref="PlaylistChosen"/> не поднимается, если плейлист тот же.
	/// </summary>
	private async Task ApplyPlaylistsAsync(
		IReadOnlyList<Playlist> playlists,
		Playlist? preferred,
		CancellationToken ct,
		bool selectIfNotFound = true,
		bool invalidateStaticCache = false,
		bool onlyIfFirst = false)
	{
		await _applyGate.WaitAsync(ct);
		try
		{
			// Фоновое обновление могло успеть применить свежее дерево раньше, чем
			// закончилась инициализация: снимок в этом случае уже неактуален.
			if (onlyIfFirst && _appliedCount > 0)
				return;

			if (invalidateStaticCache)
				_playlistTreeComposer.InvalidateCache();

			List<PlaylistTreeItem> roots = (await _playlistTreeComposer.ComposeAsync(playlists, ct)).ToList();
			_view.SetPlaylistTree(roots);
			_appliedCount++;

			Playlist? selected = preferred is not null ? FindPlaylist(roots, preferred) : null;
			if (selected is not null)
			{
				lock (_stateSync)
				{
					_restoredLastPlaylist = null;
				}
			}

			if (selected is null && selectIfNotFound)
				selected = FindFirstSelectablePlaylist(roots);

			if (selected is null)
				return;

			bool samePlaylist;
			lock (_stateSync)
			{
				samePlaylist = _currentPlaylist is not null && _currentPlaylist.HasSameIdentity(selected);
				_currentPlaylist = selected;
			}

			_view.MarkAsPlaying(selected);

			if (!samePlaylist)
				PlaylistChosen?.Invoke(selected);
		}
		finally
		{
			_applyGate.Release();
		}
	}

	private static Playlist? FindPlaylist(IEnumerable<PlaylistTreeItem> items, Playlist target)
	{
		foreach (var item in items)
		{
			if (item.Playlist is not null && item.Playlist.HasSameIdentity(target))
			{
				return item.Playlist;
			}

			var nested = FindPlaylist(item.Children.OfType<PlaylistTreeItem>(), target);
			if (nested is not null)
			{
				return nested;
			}
		}

		return null;
	}

	private static Playlist? FindFirstSelectablePlaylist(IEnumerable<PlaylistTreeItem> items)
	{
		foreach (var item in items)
		{
			var nestedPlaylist = FindFirstSelectablePlaylist(item.Children.OfType<PlaylistTreeItem>());
			if (nestedPlaylist is not null)
			{
				return nestedPlaylist;
			}

			if (item.Playlist is not null)
			{
				return item.Playlist;
			}
		}

		return null;
	}

	public void NotifyTransientPlaylistActive(Playlist playlist)
	{
		_view.AddOrUpdateTransientPlaylist(playlist);
		_view.MarkAsPlaying(playlist);
	}

	private void OnPlaylistSelected(Playlist playlist)
	{
		bool samePlaylist;
		lock (_stateSync)
		{
			samePlaylist = _currentPlaylist is not null && _currentPlaylist.HasSameIdentity(playlist);
			_currentPlaylist = playlist;
			_restoredLastPlaylist = null;
			_userChosePlaylist = true;
		}

		_view.MarkAsPlaying(playlist);

		// Повторный выбор того же плейлиста (в том числе восстановленный деревом
		// после фонового обновления) не должен перезагружать список треков.
		if (samePlaylist)
			return;

		Logging.LogUserAction($"выбран плейлист «{playlist.Title}» ({playlist.Kind})");
		PlaylistChosen?.Invoke(playlist);
	}
}
