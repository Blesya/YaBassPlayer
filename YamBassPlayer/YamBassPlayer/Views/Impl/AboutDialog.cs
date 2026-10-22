using System.Runtime.InteropServices;
using Terminal.Gui;
using YamBassPlayer.Constants;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Окно «О программе». Показывает версию, сведения о среде выполнения,
/// возможности, горячие клавиши и использованные технологии в прокручиваемом
/// текстовом поле. Закрывается по кнопке OK или Esc.
/// </summary>
public sealed class AboutDialog : Dialog
{
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
		return string.Join("\n", new[]
		{
			$"Версия:               {AppInfo.Version}",
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
			" • Окно «О треке»: доступность, метки, кредиты, описание исполнителя",
			" • 8 режимов визуализации спектра в реальном времени",
			" • 10-полосный эквалайзер с сохранением настроек",
			" • 7 тем оформления",
			" • Плагины: локальная музыка и Яндекс.Музыка",
			" • Плейлисты, избранное, плейлист дня, умные топы по истории",
			" • Поиск по локальной медиатеке и по Яндекс.Музыке",
			" • Встроенная командная строка и история прослушивания",
			"",
			"Горячие клавиши:",
			" F5 — визуализация «Сейчас играет»",
			" F7 — «О треке»: доступность, метки, кредиты, описание исполнителя",
			" F8 — крупное инфо о треке",
			" F9 — «Моя волна»",
			" ё / ~ — фокус на командную строку",
			"",
			"Технологии:",
			" Terminal.Gui 1.19.0 — терминальный интерфейс",
			" ManagedBass 4.0.2 — воспроизведение, эквалайзер, FFT",
			" Плагин Яндекс.Музыки (YamBassPlayer.Api, GPL-3.0) — источник и вход",
			" Microsoft.Data.Sqlite — локальное хранилище",
			" TagLibSharp 2.3.0 — метаданные аудиофайлов",
			" Microsoft.ML 5.0.0 — рекомендации треков",
			" Autofac 8.1.1 — внедрение зависимостей",
			"",
			$"Исходный код: {AppInfo.RepositoryUrl}",
			"",
			"© 2025 Blesya. Лицензия MIT."
		});
	}
}
