using YamBassPlayer.Configuration;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class EqualizerPresenter : IEqualizerPresenter
{
	private readonly IBassEqualizer _bassEqualizer;
	private readonly IAppConfiguration _configuration;
	private readonly IViewFactory _viewFactory;
	private readonly float[] _savedValues = new float[10];
	private readonly float[] _tempValues = new float[10];

	public EqualizerPresenter(IBassEqualizer bassEqualizer, IAppConfiguration configuration, IViewFactory viewFactory)
	{
		_bassEqualizer = bassEqualizer;
		_configuration = configuration;
		_viewFactory = viewFactory;
		LoadFromConfig();
	}

	private void LoadFromConfig()
	{
		var bands = _configuration.GetEqualizerBands();
		Array.Copy(bands, _savedValues, 10);
		ApplyEqualizerValues(_savedValues);
	}

	private void SaveToConfig()
	{
		_configuration.SaveEqualizerBands(_savedValues);
	}

	public void ShowEqualizerDialog()
	{
		var view = _viewFactory.Create<IEqualizerView>();

		Array.Copy(_savedValues, _tempValues, 10);

		for (int i = 0; i < 10; i++)
		{
			view.SetBandValue(i, _savedValues[i]);
		}

		view.OnBandChanged += (bandIndex, value) =>
		{
			_tempValues[bandIndex] = value;
			ApplyEqualizerValues(_tempValues);
		};

		view.OnOkClicked += () =>
		{
			Array.Copy(_tempValues, _savedValues, 10);
			SaveToConfig();
		};

		view.OnCancelClicked += () =>
		{
			ApplyEqualizerValues(_savedValues);
		};

		view.Show();
	}

	private void ApplyEqualizerValues(float[] values)
	{
		for (int i = 0; i < values.Length; i++)
		{
			_bassEqualizer.SetBand(i, values[i]);
		}
	}
}
