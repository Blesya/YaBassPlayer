using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class PlaylistsView : View, IPlaylistsView
{
    private readonly ListView _list;

    public event Action<int>? PlaylistSelected;

    public PlaylistsView()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        _list = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false
        };

        _list.SelectedItemChanged += args =>
        {
            PlaylistSelected?.Invoke(args.Item);
        };

        _list.OpenSelectedItem += args =>
        {
            PlaylistSelected?.Invoke(args.Item);
        };

        Add(_list);
    }

    public void SetPlaylists(IEnumerable<Playlist> playlists)
    {
        Application.MainLoop.Invoke(() =>
        {
            _list.SetSource(playlists.ToList());
        });
    }

    public void HighlightPlaylist(int index)
    {
        Application.MainLoop.Invoke(() =>
        {
            _list.SelectedItem = index;
        });
    }
}