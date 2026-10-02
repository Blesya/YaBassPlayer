using System.Threading;
using Serilog;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class PlaylistsPresenter : IPlaylistsPresenter
{
	private readonly IPlaylistsView _view;
	private readonly ITrackRepository _trackRepository;
	private readonly IPlaylistTreeComposer _playlistTreeComposer;
	private readonly IErrorHandler _errorHandler;

	private Playlist? _currentPlaylist;

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
			// 1. Мгновенно показываем состояние прошлого запуска (без сети).
			var persisted = _trackRepository.GetPersistedSnapshot();
			if (persisted is { Playlists.Count: > 0 })
			{
				Log.Information("Плейлисты восстановлены из снимка: {Count}", persisted.Playlists.Count);
				await ApplyPlaylistsAsync(persisted.Playlists, persisted.LastPlaylist, ct);

				// 2. В фоне актуализируем состав и перерисовываем дерево.
				_ = RefreshAsync(ct);
				Logging.LogAfterCall();
				return;
			}

			// Снимка нет — грузим как раньше, одним запросом.
			var playlists = (await _trackRepository.GetPlaylists(ct)).ToList();
			Log.Information("Плейлисты загружены из источников: {Count}", playlists.Count);
			await ApplyPlaylistsAsync(playlists, null, ct);
			Logging.LogAfterCall();
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	public void LoadPlaylistTree()
	{
		_ = RefreshAsync(CancellationToken.None);
	}

	/// <summary>Перезагружает дерево из источников, сохраняя текущий выбор.</summary>
	private async Task RefreshAsync(CancellationToken ct)
	{
		Logging.LogBeforeCall();

		try
		{
			var playlists = (await _trackRepository.GetPlaylists(ct)).ToList();
			if (playlists.Count == 0)
				return;

			Log.Information("Дерево плейлистов обновлено из источников: {Count}", playlists.Count);

			// Если выбран временный плейлист (поиск/очередь) — не навязываем первый из дерева.
			await ApplyPlaylistsAsync(playlists, _currentPlaylist, ct, selectIfNotFound: _currentPlaylist is null);
			Logging.LogAfterCall();
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	/// <summary>
	/// Строит дерево из плейлистов, отдаёт его вьюхе и выбирает плейлист:
	/// <paramref name="preferred"/> (с восстановлением прошлого выбора), иначе первый доступный.
	/// </summary>
	private async Task ApplyPlaylistsAsync(
		IReadOnlyList<Playlist> playlists,
		Playlist? preferred,
		CancellationToken ct,
		bool selectIfNotFound = true)
	{
		_playlistTreeComposer.InvalidateCache();
		var roots = (await _playlistTreeComposer.ComposeAsync(playlists, ct)).ToList();
		_view.SetPlaylistTree(roots);

		var selected = preferred is not null ? FindPlaylist(roots, preferred) : null;
		if (selected is null && selectIfNotFound)
			selected = FindFirstSelectablePlaylist(roots);

		if (selected is null)
			return;

		_currentPlaylist = selected;
		_view.MarkAsPlaying(selected);
		PlaylistChosen?.Invoke(selected);
	}

	private static Playlist? FindPlaylist(IEnumerable<PlaylistTreeItem> items, Playlist target)
	{
		foreach (var item in items)
		{
			if (item.Playlist is not null
				&& item.Playlist.Type == target.Type
				&& item.Playlist.PlaylistName == target.PlaylistName)
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
		Logging.LogUserAction($"выбран плейлист «{playlist.PlaylistName}» ({playlist.Type})");
		_currentPlaylist = playlist;
		_view.MarkAsPlaying(playlist);
		PlaylistChosen?.Invoke(playlist);
	}
}
