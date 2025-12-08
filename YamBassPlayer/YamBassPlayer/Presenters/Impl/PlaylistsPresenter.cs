using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class PlaylistsPresenter : IPlaylistsPresenter
{
    private readonly IPlaylistsView _view;
    private readonly ITrackRepository _trackRepository;

    private List<Playlist> _playlists = new();
    public event Action<Playlist>? PlaylistChosen;

    public PlaylistsPresenter(IPlaylistsView view, ITrackRepository trackRepository)
    {
        _view = view;
        _trackRepository = trackRepository;

        _view.PlaylistSelected += OnPlaylistSelected;

        LoadPlaylists();
    }

    private async void LoadPlaylists()
    {
        var playlists = await _trackRepository.GetPlaylists();
        _playlists = playlists.ToList();
        _view.SetPlaylists(_playlists);

        if (_playlists.Any())
        {
            PlaylistChosen?.Invoke(_playlists.First());
        }
    }

    private void OnPlaylistSelected(int index)
    {
        if (index < 0 || index >= _playlists.Count)
            return;

        PlaylistChosen?.Invoke(_playlists[index]);
    }
}