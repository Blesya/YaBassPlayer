using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Views.Impl;

public sealed class LargeTrackInfoView : Window, ILargeTrackInfoView
{
	private readonly Label _artistTitleLabel;
	private readonly Label _albumLabel;
	private readonly CoverAsciiView _asciiView;
	private readonly PlaylistRowsView _playlistPanel;

	private (int Width, int Height) _appliedCoverSize;
	private bool _applyingLayout;

	public event Action? CoverSizeChanged;

	public Action? OnClose { get; set; }

	public Action<TrackKey>? OnTrackActivated
	{
		get => _playlistPanel.OnTrackActivated;
		set => _playlistPanel.OnTrackActivated = value;
	}

	public LargeTrackInfoView() : base("Крупное инфо")
	{
		// Полноэкранное окно поверх главного: без Modal таймер спектра главного
		// окна продолжает рисовать полосу поверх него.
		Modal = true;

		X = 0;
		Y = 0;
		Width = Dim.Fill();
		Height = Dim.Fill();

		_asciiView = new CoverAsciiView
		{
			X = 1,
			Y = 1
		};

		_artistTitleLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 2,
			Width = Dim.Fill(2),
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = "— — —"
		};

		_albumLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 4,
			Width = Dim.Fill(2),
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = string.Empty
		};

		_playlistPanel = new PlaylistRowsView
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 6,
			Width = Dim.Fill(2),
			Height = Dim.Fill(3)
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

		_appliedCoverSize = ComputeCoverSize();
		_asciiView.Width = _appliedCoverSize.Width;
		_asciiView.Height = _appliedCoverSize.Height;

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

	public (int Width, int Height) CoverSize => ComputeCoverSize();

	/// <summary>
	/// Размер обложки по текущему размеру окна. До первой раскладки берётся
	/// размер драйвера: окно полноэкранное, поэтому значения совпадают.
	/// </summary>
	private (int Width, int Height) ComputeCoverSize()
	{
		int frameWidth = Bounds.Width > 0 ? Bounds.Width : Application.Driver?.Cols ?? 0;
		int frameHeight = Bounds.Height > 0 ? Bounds.Height : Application.Driver?.Rows ?? 0;

		if (frameWidth <= 0 || frameHeight <= 0)
			return (20, 11);

		return CoverLayout.Compute(
			Math.Max(1, frameWidth - 4),
			Math.Max(1, frameHeight - 5));
	}

	public override void LayoutSubviews()
	{
		base.LayoutSubviews();

		if (_applyingLayout)
			return;

		_applyingLayout = true;
		try
		{
			(int Width, int Height) size = ComputeCoverSize();
			if (size == _appliedCoverSize)
				return;

			_appliedCoverSize = size;
			_asciiView.Width = size.Width;
			_asciiView.Height = size.Height;
			CoverSizeChanged?.Invoke();
		}
		finally
		{
			_applyingLayout = false;
		}
	}

	public void SetCover(CoverArt? art)
	{
		Application.MainLoop.Invoke(() => _asciiView.SetPixels(art));
	}

	public void SetPlaylist(IReadOnlyList<Track> tracks)
	{
		Application.MainLoop.Invoke(() => _playlistPanel.SetTracks(tracks));
	}

	public void SetCurrentTrack(TrackKey? key)
	{
		Application.MainLoop.Invoke(() => _playlistPanel.SetCurrentTrack(key));
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
