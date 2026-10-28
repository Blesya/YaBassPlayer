namespace YamBassPlayer.Models;

/// <summary>
/// Настройки запуска «Моей волны» — тело запроса
/// <c>POST rotor/station/user:onyourwave/settings2</c>.
/// Значения строковые, как их ждёт API: diversity, moodEnergy, language.
/// </summary>
public sealed record MyWaveSettings(string Diversity, string MoodEnergy, string Language)
{
	public static MyWaveSettings Default { get; } = new(
		MyWaveSettingsOptions.DefaultDiversity,
		MyWaveSettingsOptions.DefaultMoodEnergy,
		MyWaveSettingsOptions.DefaultLanguage);
}

/// <summary>Вариант настройки: значение для API и подпись для окна.</summary>
public sealed record MyWaveSettingOption(string Value, string Label);

/// <summary>
/// Допустимые значения настроек волны. Источник — <c>restrictions2</c> станции
/// <c>user:onyourwave</c> из <c>GET rotor/stations/dashboard</c>; список зафиксирован,
/// чтобы окно открывалось без сети и с теми же подписями, что в Яндекс.Музыке.
/// </summary>
public static class MyWaveSettingsOptions
{
	public const string DefaultDiversity = "default";
	public const string DefaultMoodEnergy = "all";
	public const string DefaultLanguage = "any";

	public static IReadOnlyList<MyWaveSettingOption> Diversity { get; } =
	[
		new("favorite", "Любимое"),
		new("discover", "Незнакомое"),
		new("popular", "Популярное"),
		new("default", "Любое"),
	];

	public static IReadOnlyList<MyWaveSettingOption> MoodEnergy { get; } =
	[
		new("active", "Бодрое"),
		new("fun", "Весёлое"),
		new("calm", "Спокойное"),
		new("sad", "Грустное"),
		new("all", "Любое"),
	];

	public static IReadOnlyList<MyWaveSettingOption> Language { get; } =
	[
		new("russian", "Русский"),
		new("not-russian", "Иностранный"),
		new("any", "Любой"),
		new("without-words", "Без слов"),
	];

	/// <summary>Индекс сохранённого значения; неизвестное значение даёт индекс fallback.</summary>
	public static int IndexOf(IReadOnlyList<MyWaveSettingOption> options, string? value, string fallback)
	{
		string normalized = Normalize(options, value, fallback);

		for (int i = 0; i < options.Count; i++)
		{
			if (options[i].Value == normalized)
				return i;
		}

		return 0;
	}

	/// <summary>Значение по индексу выбранного пункта; выход за границы даёт fallback.</summary>
	public static string ValueAt(IReadOnlyList<MyWaveSettingOption> options, int index, string fallback)
		=> index >= 0 && index < options.Count ? options[index].Value : fallback;

	/// <summary>
	/// Оставляет только известное API значение: в конфигурации плагина может
	/// остаться устаревшая или чужая строка.
	/// </summary>
	public static string Normalize(IReadOnlyList<MyWaveSettingOption> options, string? value, string fallback)
		=> options.Any(option => option.Value == value) ? value! : fallback;
}
