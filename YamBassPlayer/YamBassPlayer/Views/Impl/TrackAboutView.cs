using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Окно «О треке»: доступность, метки, кредиты и описание исполнителя.
/// </summary>
public sealed class TrackAboutView : Dialog, ITrackAboutView
{
	private readonly TextView _textView;

	public TrackAboutView() : base("О треке", 80, 24)
	{
		_textView = new TextView
		{
			X = 1,
			Y = 1,
			Width = Dim.Fill() - 2,
			Height = Dim.Fill(4),
			ReadOnly = true,
			WordWrap = true,
			CanFocus = true
		};

		var closeButton = new Button("Закрыть [ESC]", is_default: true);
		closeButton.Clicked += () => Application.RequestStop(this);

		AddButton(closeButton);
		Add(_textView);

		KeyPress += e =>
		{
			if (e.KeyEvent.Key == Key.Esc)
			{
				Application.RequestStop(this);
				e.Handled = true;
			}
		};
	}

	public Toplevel ModalWindow => this;

	public void SetInfo(TrackAboutInfo info)
	{
		_textView.Text = TrackAboutInfoFormatter.Format(info);
	}

	public void Show()
	{
		Application.Run(this);
	}
}
