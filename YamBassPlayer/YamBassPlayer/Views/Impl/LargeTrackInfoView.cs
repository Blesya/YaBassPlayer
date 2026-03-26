using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class LargeTrackInfoView : Window, ILargeTrackInfoView
{
	private readonly Label _artistTitleLabel;
	private readonly Label _albumLabel;
	private readonly CoverAsciiView _asciiView;
	private readonly PlaylistRowsView _playlistPanel;

	public Action? OnClose { get; set; }

	public Action<string>? OnTrackActivated
	{
		get => _playlistPanel.OnTrackActivated;
		set => _playlistPanel.OnTrackActivated = value;
	}

	private const int CoverWidth = 100;
	private const int CoverHeight = 55;

	public LargeTrackInfoView() : base("Крупное инфо")
	{
		X = 0;
		Y = 0;
		Width = Dim.Fill();
		Height = Dim.Fill();

		_asciiView = new CoverAsciiView
		{
			X = 1,
			Y = 1,
			Width = CoverWidth,
			Height = CoverHeight
		};

		_artistTitleLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 2,
			Width = Dim.Fill() - 2,
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = "— — —"
		};

		_albumLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 4,
			Width = Dim.Fill() - 2,
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = string.Empty
		};

		_playlistPanel = new PlaylistRowsView
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 6,
			Width = Dim.Fill() - 2,
			Height = Dim.Fill() - 3
		};

		var closeButton = new Button
		{
			X = Pos.Center() - 8,
			Y = Pos.AnchorEnd(2),
			Text = "Закрыть [ESC]",
			CanFocus = false
		};
		closeButton.Clicked += Close;

		Add(_asciiView, _artistTitleLabel, _albumLabel, _playlistPanel, closeButton);

		Loaded += () => _playlistPanel.SetFocus();

		KeyPress += e =>
		{
			if (e.KeyEvent.Key == Key.Esc)
			{
				Close();
				e.Handled = true;
			}
		};
	}

	public void SetTrack(Track track)
	{
		Application.MainLoop.Invoke(() =>
		{
			_artistTitleLabel.Text = $"{track.Artist}   —   {track.Title}";
			_albumLabel.Text = string.IsNullOrWhiteSpace(track.Album) ? string.Empty : $"[ {track.Album} ]";
		});
	}

	public void SetListenCount(int count) { }

	public void SetCover(string? coverPath)
	{
		Application.MainLoop.Invoke(() =>
		{
			if (string.IsNullOrWhiteSpace(coverPath) || !File.Exists(coverPath))
			{
				_asciiView.SetPixels(null);
				return;
			}

			try
			{
				_asciiView.SetPixels(CoverAsciiView.RenderAscii(
					coverPath,
					CoverWidth,
					CoverHeight));
			}
			catch
			{
				_asciiView.SetPixels(null);
			}
		});
	}

	public void SetPlaylist(IReadOnlyList<Track> tracks)
	{
		Application.MainLoop.Invoke(() => _playlistPanel.SetTracks(tracks));
	}

	public void SetCurrentTrackId(string? trackId)
	{
		Application.MainLoop.Invoke(() => _playlistPanel.SetCurrentTrackId(trackId));
	}

	public void Show()
	{
		Application.Run(this);
	}

	public void Close()
	{
		OnClose?.Invoke();
		Application.RequestStop(this);
	}
}
