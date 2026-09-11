using System.Reflection;
using System.Runtime.InteropServices;
using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Окно «О программе». Показывает версию, сведения о среде выполнения,
/// возможности, горячие клавиши и использованные технологии в прокручиваемом
/// текстовом поле. Закрывается по кнопке OK или Esc.
/// </summary>
public sealed class AboutDialog : Dialog
{
	private const string RepositoryUrl = "https://github.com/Blesya/YaBassPlayer";

	public AboutDialog() : base("О программе", 80, 24)
	{
		var titleLabel = new Label
		{
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = 1,
			TextAlignment = TextAlignment.Centered,
			AutoSize = false,
			Text = "YamBassPlayer"
		};

		var textView = new TextView
		{
			X = 1,
			Y = 2,
			Width = Dim.Fill() - 2,
			Height = Dim.Fill(4),
			ReadOnly = true,
			WordWrap = true,
			CanFocus = true
		};
		textView.Text = BuildText();

		var closeButton = new Button("OK", is_default: true);
		closeButton.Clicked += () => Application.RequestStop(this);

		AddButton(closeButton);
		Add(titleLabel, textView);

		KeyPress += e =>
		{
			if (e.KeyEvent.Key == Key.Esc)
			{
				Application.RequestStop(this);
				e.Handled = true;
			}
		};
	}

	/// <summary>Открывает модальное окно «О программе».</summary>
	public static void Show()
	{
		var dialog = new AboutDialog();
		Application.Run(dialog);
	}

	private static string BuildText()
	{
		string version = GetVersion();

		return string.Join("\n", new[]
		{
			$"Версия:               {version}",
			$"Среда выполнения:     {RuntimeInformation.FrameworkDescription}",
			$"Операционная система: {RuntimeInformation.OSDescription}",
			"",
			"Консольный музыкальный плеер с терминальным интерфейсом:",
			"Яндекс.Музыка, локальная медиатека, радио, визуализация спектра.",
			"",
			"Возможности:",
			" • Воспроизведение Яндекс.Музыки с предзагрузкой следующего трека",
			" • Локальная медиатека: папки, сканирование, чтение ID3-тегов",
			" • Радио «Моя волна» — персональная и по треку",
			" • Тексты песен и ASCII-обложки",
			" • 8 режимов визуализации спектра в реальном времени",
			" • 10-полосный эквалайзер с сохранением настроек",
			" • 7 тем оформления",
			" • Плейлисты, избранное, плейлист дня, умные топы по истории",
			" • Поиск по локальной медиатеке и по Яндекс.Музыке",
			" • Встроенная командная строка и история прослушивания",
			"",
			"Горячие клавиши:",
			" F5 — визуализация «Сейчас играет»",
			" F8 — крупное инфо о треке",
			" F9 — «Моя волна»",
			" ё / ~ — фокус на командную строку",
			"",
			"Технологии:",
			" Terminal.Gui 1.19.0 — терминальный интерфейс",
			" ManagedBass 4.0.2 — воспроизведение, эквалайзер, FFT",
			" KM.Yandex.Music.Api 2.0.6 — интеграция с Яндекс.Музыкой",
			" Microsoft.Data.Sqlite — локальное хранилище",
			" TagLibSharp 2.3.0 — метаданные аудиофайлов",
			" Microsoft.ML 5.0.0 — рекомендации треков",
			" Autofac 8.1.1 — внедрение зависимостей",
			"",
			$"Исходный код: {RepositoryUrl}",
			"",
			"© 2025 Blesya. Лицензия MIT."
		});
	}

	private static string GetVersion()
	{
		var assembly = typeof(AboutDialog).Assembly;

		string? informational = assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
			.InformationalVersion;

		string version = informational
			?? assembly.GetName().Version?.ToString()
			?? "1.0.0";

		// Отбрасываем суффикс сборки вида «1.0.0+<commit>», если он есть.
		int plusIndex = version.IndexOf('+');
		return plusIndex >= 0 ? version[..plusIndex] : version;
	}
}
