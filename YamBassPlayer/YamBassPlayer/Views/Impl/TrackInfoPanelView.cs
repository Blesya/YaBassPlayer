using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class TrackInfoPanelView : FrameView, ITrackInfoPanelView
{
    private const int CoverWidth = 34;
    private const int CoverHeight = 17;

    private readonly CoverAsciiView _asciiView;
    private readonly Label _artistLabel;
    private readonly Label _titleLabel;
    private readonly Label _albumLabel;
    private readonly Label _placeholderLabel;
    private readonly TextView _lyricsTextView;
    private readonly Label _lyricsHeaderLabel;
    private readonly Label _lyricsLoadingLabel;

    public TrackInfoPanelView() : base("Инфо")
    {
        _asciiView = new CoverAsciiView
        {
            X = 1,
            Y = 1,
            Width = CoverWidth,
            Height = CoverHeight
        };

        _placeholderLabel = new Label
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = Dim.Fill(),
            Height = 1,
            TextAlignment = TextAlignment.Centered,
            AutoSize = false,
            Text = "Выберите трек"
        };

        _artistLabel = new Label
        {
            X = 1,
            Y = CoverHeight + 2,
            Width = Dim.Fill() - 1,
            Height = 1,
            AutoSize = false,
            TextAlignment = TextAlignment.Left,
            Text = string.Empty,
            Visible = false
        };

        _titleLabel = new Label
        {
            X = 1,
            Y = CoverHeight + 3,
            Width = Dim.Fill() - 1,
            Height = 1,
            AutoSize = false,
            TextAlignment = TextAlignment.Left,
            Text = string.Empty,
            Visible = false
        };

        _albumLabel = new Label
        {
            X = 1,
            Y = CoverHeight + 4,
            Width = Dim.Fill() - 1,
            Height = 1,
            AutoSize = false,
            TextAlignment = TextAlignment.Left,
            Text = string.Empty,
            Visible = false
        };

        _lyricsHeaderLabel = new Label
        {
            X = 1,
            Y = CoverHeight + 7,
            Width = Dim.Fill() - 1,
            Height = 1,
            AutoSize = false,
            Text = "── Текст песни ──────────────────────────────",
            Visible = false
        };

        _lyricsLoadingLabel = new Label
        {
            X = 1,
            Y = CoverHeight + 8,
            Width = Dim.Fill() - 1,
            Height = 1,
            AutoSize = false,
            Text = "Загрузка...",
            Visible = false
        };

        _lyricsTextView = new TextView
        {
            X = 1,
            Y = CoverHeight + 8,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 1,
            ReadOnly = true,
            WordWrap = true,
            CanFocus = true,
            Visible = false
        };

        Add(_placeholderLabel, _asciiView, _artistLabel, _titleLabel, _albumLabel,
            _lyricsHeaderLabel, _lyricsLoadingLabel, _lyricsTextView);
    }

    public void SetTrack(Track track)
    {
        Application.MainLoop.Invoke(() =>
        {
            _placeholderLabel.Visible = false;
            _asciiView.SetPixels(null);

            _artistLabel.Text = track.Artist;
            _titleLabel.Text = track.Title;
            _albumLabel.Text = string.IsNullOrWhiteSpace(track.Album) ? string.Empty : $"[ {track.Album} ]";

            _artistLabel.Visible = true;
            _titleLabel.Visible = true;
            _albumLabel.Visible = !string.IsNullOrWhiteSpace(track.Album);

            _lyricsTextView.Text = string.Empty;
            _lyricsTextView.Visible = false;
            _lyricsLoadingLabel.Visible = true;
            _lyricsHeaderLabel.Visible = true;

            SetNeedsDisplay();
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
                _asciiView.SetPixels(CoverAsciiView.RenderAscii(coverPath, CoverWidth, CoverHeight));
            }
            catch
            {
                _asciiView.SetPixels(null);
            }
        });
    }

    public void SetLyrics(string? lyrics)
    {
        Application.MainLoop.Invoke(() =>
        {
            _lyricsLoadingLabel.Visible = false;

            if (string.IsNullOrWhiteSpace(lyrics))
            {
                _lyricsHeaderLabel.Visible = false;
                _lyricsTextView.Visible = false;
            }
            else
            {
                _lyricsTextView.Text = lyrics;
                _lyricsTextView.Visible = true;
            }

            SetNeedsDisplay();
        });
    }

    public void ClearTrack()
    {
        Application.MainLoop.Invoke(() =>
        {
            _asciiView.SetPixels(null);
            _artistLabel.Text = string.Empty;
            _titleLabel.Text = string.Empty;
            _albumLabel.Text = string.Empty;

            _artistLabel.Visible = false;
            _titleLabel.Visible = false;
            _albumLabel.Visible = false;
            _lyricsHeaderLabel.Visible = false;
            _lyricsLoadingLabel.Visible = false;
            _lyricsTextView.Visible = false;
            _placeholderLabel.Visible = true;

            SetNeedsDisplay();
        });
    }

}
