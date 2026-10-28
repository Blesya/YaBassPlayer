using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Окно настроек «Моей волны». Вьюха только показывает варианты и поднимает
/// события: применение настроек и запуск волны — в презентере.
/// </summary>
public sealed class MyWaveSettingsView : Dialog, IMyWaveSettingsView
{
	private const int LeftColumn = 2;
	private const int RightColumn = 38;
	private const int GroupWidth = 32;

	private readonly RadioGroup _diversityGroup;
	private readonly RadioGroup _moodEnergyGroup;
	private readonly RadioGroup _languageGroup;

	public event Action? OnStartClicked;
	public event Action? OnCancelClicked;

	public MyWaveSettingsView() : base("Моя волна")
	{
		Width = 72;
		Height = 20;

		var hintLabel = new Label
		{
			X = LeftColumn,
			Y = 1,
			Text = "Настройки запуска"
		};

		var diversityLabel = new Label
		{
			X = LeftColumn,
			Y = 3,
			Text = "Треки"
		};
		_diversityGroup = CreateGroup(MyWaveSettingsOptions.Diversity, LeftColumn, 4);

		var languageLabel = new Label
		{
			X = LeftColumn,
			Y = 9,
			Text = "Язык"
		};
		_languageGroup = CreateGroup(MyWaveSettingsOptions.Language, LeftColumn, 10);

		var moodEnergyLabel = new Label
		{
			X = RightColumn,
			Y = 3,
			Text = "Настроение"
		};
		_moodEnergyGroup = CreateGroup(MyWaveSettingsOptions.MoodEnergy, RightColumn, 4);

		var startButton = new Button("Запустить")
		{
			X = Pos.Center() - 14,
			Y = Pos.AnchorEnd(2),
			IsDefault = true
		};
		startButton.Clicked += () => OnStartClicked?.Invoke();

		var cancelButton = new Button("Отменить")
		{
			X = Pos.Center() + 3,
			Y = Pos.AnchorEnd(2)
		};
		cancelButton.Clicked += () => OnCancelClicked?.Invoke();

		Add(hintLabel,
			diversityLabel,
			_diversityGroup,
			languageLabel,
			_languageGroup,
			moodEnergyLabel,
			_moodEnergyGroup,
			startButton,
			cancelButton);
	}

	public void SetSettings(MyWaveSettings settings)
	{
		_diversityGroup.SelectedItem = MyWaveSettingsOptions.IndexOf(
			MyWaveSettingsOptions.Diversity, settings.Diversity, MyWaveSettingsOptions.DefaultDiversity);

		_moodEnergyGroup.SelectedItem = MyWaveSettingsOptions.IndexOf(
			MyWaveSettingsOptions.MoodEnergy, settings.MoodEnergy, MyWaveSettingsOptions.DefaultMoodEnergy);

		_languageGroup.SelectedItem = MyWaveSettingsOptions.IndexOf(
			MyWaveSettingsOptions.Language, settings.Language, MyWaveSettingsOptions.DefaultLanguage);
	}

	public MyWaveSettings SelectedSettings => new(
		MyWaveSettingsOptions.ValueAt(
			MyWaveSettingsOptions.Diversity, _diversityGroup.SelectedItem, MyWaveSettingsOptions.DefaultDiversity),
		MyWaveSettingsOptions.ValueAt(
			MyWaveSettingsOptions.MoodEnergy, _moodEnergyGroup.SelectedItem, MyWaveSettingsOptions.DefaultMoodEnergy),
		MyWaveSettingsOptions.ValueAt(
			MyWaveSettingsOptions.Language, _languageGroup.SelectedItem, MyWaveSettingsOptions.DefaultLanguage));

	public void Show() => Application.Run(this);

	public void Close() => Application.RequestStop(this);

	private static RadioGroup CreateGroup(IReadOnlyList<MyWaveSettingOption> options, int x, int y) =>
		new(options.Select(option => (NStack.ustring)option.Label).ToArray())
		{
			X = x,
			Y = y,
			Width = GroupWidth,
			Height = options.Count,
			DisplayMode = DisplayModeLayout.Vertical
		};
}
