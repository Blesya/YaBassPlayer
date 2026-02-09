using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class DatabaseStatisticsView : Dialog, IDatabaseStatisticsView
{
	private readonly Label[] _valueLabels = new Label[9];

	private static readonly string[] MetricNames =
	[
		"Треков в кэше метаданных:",
		"Всего прослушиваний:",
		"Уникальных прослушанных:",
		"Локальных избранных:",
		"Файлов в кэше:",
		"Размер кэша:",
		"Размер БД:",
		"Первое прослушивание:",
		"Последнее прослушивание:"
	];

	public DatabaseStatisticsView() : base("Статистика", 55, 22)
	{
		int labelWidth = 30;
		int valueX = labelWidth + 1;

		for (int i = 0; i < MetricNames.Length; i++)
		{
			var nameLabel = new Label
			{
				X = 2,
				Y = 1 + i,
				Width = labelWidth,
				Text = MetricNames[i]
			};

			_valueLabels[i] = new Label
			{
				X = valueX,
				Y = 1 + i,
				Width = 20,
				Text = "—"
			};

			Add(nameLabel, _valueLabels[i]);
		}

		var closeButton = new Button("Закрыть");
		closeButton.Clicked += () => Application.RequestStop(this);
		AddButton(closeButton);
	}

	public void SetStatistics(DatabaseStatistics stats)
	{
		_valueLabels[0].Text = stats.TracksCount.ToString();
		_valueLabels[1].Text = stats.TotalListens.ToString();
		_valueLabels[2].Text = stats.UniqueListenedTracks.ToString();
		_valueLabels[3].Text = stats.LocalFavoritesCount.ToString();
		_valueLabels[4].Text = stats.CachedFilesCount.ToString();
		_valueLabels[5].Text = stats.CachedFilesSize.ToHumanReadableSize();
		_valueLabels[6].Text = stats.DatabaseFileSize.ToHumanReadableSize();
		_valueLabels[7].Text = stats.FirstListenDate?.ToString("g") ?? "—";
		_valueLabels[8].Text = stats.LastListenDate?.ToString("g") ?? "—";
	}

	public void Show()
	{
		Application.Run(this);
	}

	public void Close()
	{
		Application.RequestStop(this);
	}
}
