using Terminal.Gui;
using YamBassPlayer.Enums;

namespace YamBassPlayer.Views.Impl;

public sealed class PlayStatusView : View, IPlayStatusView
{
	private const string PlayButtonLabel = "Играть";
	private const string PauseButtonLabel = "Пауза";
	private const string StopButtonLabel = "Стоп";
	private const string RestartButtonLabel = "Заново";
	private const string PrevButtonLabel = "Назад";
	private const string NextButtonLabel = "Вперёд";

	private readonly Label _statusLabel;
	private readonly FrameView _panel;
	private readonly Button _playButton;
	private readonly Button _stopButton;
	private readonly Button _restartButton;
	private readonly SeekBarView _seekBar;
	private readonly VolumeBarView _volumeBar;
	private readonly Button _prevButton;
	private readonly Button _nextButton;
	private readonly View _actionsBar;
	private readonly Button _queueButton;
	private readonly Button _playbackModeButton;

	public event Action? OnPlayClicked;
	public event Action? OnStopClicked;
	public event Action? OnPrevClicked;
	public event Action? OnNextClicked;
	public event Action<int>? OnSeekRequested;
	public event Action<int>? OnVolumeChangeRequested;
	public event Action<string>? OnTrackActionClicked;
	public event Action? OnQueueClicked;
	public event Action? OnPlaybackModeToggled;
	public event Action? OnRestartClicked;

	public PlayStatusView()
	{
		Width = Dim.Fill();
		Height = 6;

		_panel = new FrameView("Управление воспроизведением")
		{
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};

		// Транспорт — короткие русские подписи вместо английских: панель компактнее,
		// а подписи не спорят с языком интерфейса.
		_playButton = new Button
		{
			X = 1,
			Y = 0,
			Text = PlayButtonLabel
		};
		_playButton.Clicked += () => OnPlayClicked?.Invoke();

		_stopButton = new Button
		{
			X = Pos.Right(_playButton) + 1,
			Y = 0,
			Text = StopButtonLabel
		};
		_stopButton.Clicked += () => OnStopClicked?.Invoke();

		_restartButton = new Button
		{
			X = Pos.Right(_stopButton) + 1,
			Y = 0,
			Text = RestartButtonLabel
		};
		_restartButton.Clicked += () => OnRestartClicked?.Invoke();

		_prevButton = new Button
		{
			X = Pos.Right(_restartButton) + 1,
			Y = 0,
			Text = PrevButtonLabel
		};
		_prevButton.Clicked += () => OnPrevClicked?.Invoke();

		_nextButton = new Button
		{
			X = Pos.Right(_prevButton) + 1,
			Y = 0,
			Text = NextButtonLabel
		};
		_nextButton.Clicked += () => OnNextClicked?.Invoke();

		_queueButton = new Button
		{
			X = Pos.Right(_nextButton) + 3,
			Y = 0,
			Text = "Очередь"
		};
		_queueButton.Clicked += () => OnQueueClicked?.Invoke();

		_playbackModeButton = new Button
		{
			X = Pos.Right(_queueButton) + 1,
			Y = 0,
			Text = "Поочерёдно"
		};
		_playbackModeButton.Clicked += () => OnPlaybackModeToggled?.Invoke();

		_actionsBar = new View
		{
			X = Pos.Right(_playbackModeButton) + 1,
			Y = 0,
			Width = Dim.Fill(),
			Height = 1
		};

		// Прошедшее и оставшееся время рисует сама полоса — по краям дорожки.
		_seekBar = new SeekBarView
		{
			X = 1,
			Y = 1,
			Width = Dim.Fill() - 2,
			Height = 1
		};
		_seekBar.SeekRequested += percent => OnSeekRequested?.Invoke(percent);

		_statusLabel = new Label
		{
			X = 1,
			Y = 2,
			Width = Dim.Fill() - 2,
			Height = 1,
			Text = "Готов к работе"
		};

		_volumeBar = new VolumeBarView
		{
			X = 1,
			Y = 3,
			Width = Dim.Fill() - 2,
			Height = 1
		};
		_volumeBar.VolumeChangeRequested += percent => OnVolumeChangeRequested?.Invoke(percent);

		_panel.Add(_playButton, _stopButton, _restartButton, _prevButton, _nextButton,
			_queueButton, _playbackModeButton, _actionsBar,
			_seekBar, _statusLabel, _volumeBar);
		Add(_panel);
	}

	public void SetPlayStatus(string status)
	{
		Application.MainLoop.Invoke(() =>
		{
			_statusLabel.Text = status;
		});
	}

	public void SetProgress(int percent)
	{
		Application.MainLoop.Invoke(() =>
		{
			_seekBar.Fraction = percent / 100f;
		});
	}

	public void SetPlaying(bool isPlaying)
	{
		Application.MainLoop.Invoke(() =>
		{
			string text = isPlaying ? PauseButtonLabel : PlayButtonLabel;
			if (_playButton.Text?.ToString() != text)
				_playButton.Text = text;
		});
	}

	public void SetTitle(string title)
	{
		Application.MainLoop.Invoke(() =>
		{
			_panel.Title = title;
		});
	}

	public void SetVolume(int percent)
	{
		Application.MainLoop.Invoke(() =>
		{
			_volumeBar.Percent = percent;
		});
	}

	public void SetTime(TimeSpan current, TimeSpan duration)
	{
		Application.MainLoop.Invoke(() =>
		{
			_seekBar.SetTime(current, duration);
		});
	}

	public void SetTrackActions(IReadOnlyList<TrackActionButton> actions)
	{
		Application.MainLoop.Invoke(() =>
		{
			_actionsBar.RemoveAll();

			Button? previous = null;
			foreach (TrackActionButton action in actions)
			{
				TrackActionButton captured = action;
				var button = new Button
				{
					X = previous is null ? 0 : Pos.Right(previous) + 1,
					Y = 0,
					Text = captured.Title,
					Enabled = captured.Enabled
				};
				button.Clicked += () => OnTrackActionClicked?.Invoke(captured.Id);

				_actionsBar.Add(button);
				previous = button;
			}

			_actionsBar.SetNeedsDisplay();
		});
	}

	public void SetPlaybackMode(PlaybackMode mode)
	{
		Application.MainLoop.Invoke(() =>
		{
			_playbackModeButton.Text = mode switch
			{
				PlaybackMode.Shuffle => "Случайно",
				PlaybackMode.Repeat => "Повтор",
				_ => "Поочерёдно"
			};
		});
	}
}
