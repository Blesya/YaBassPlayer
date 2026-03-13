using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Views;

namespace YamBassPlayer.Views.Impl;

public sealed class RecommendationGraphView : Window, IRecommendationGraphView
{
	private readonly Label _statusLabel;
	private readonly TextView _graphTextView;
	private readonly Label _legendLabel;

	public RecommendationGraphView() : base("Граф рекомендаций")
	{
		X = 0;
		Y = 0;
		Width = Dim.Fill();
		Height = Dim.Fill();

		_statusLabel = new Label
		{
			X = 1,
			Y = 0,
			Width = Dim.Fill() - 2,
			Height = 1,
			Text = "Загрузка графа...",
			AutoSize = false
		};

		_graphTextView = new TextView
		{
			X = 0,
			Y = 1,
			Width = Dim.Fill(),
			Height = Dim.Fill(4),
			ReadOnly = true,
			WordWrap = false
		};

		_legendLabel = new Label
		{
			X = 1,
			Y = Pos.AnchorEnd(3),
			Width = Dim.Fill() - 18,
			Height = 2,
			Text = "● — текущий трек   ○ — рядом   · — дальше   → вес перехода",
			AutoSize = false
		};

		var closeButton = new Button
		{
			X = Pos.AnchorEnd(15),
			Y = Pos.AnchorEnd(3),
			Text = "Закрыть [ESC]"
		};
		closeButton.Clicked += () => Close();

		Add(_statusLabel, _graphTextView, _legendLabel, closeButton);

		KeyPress += e =>
		{
			if (e.KeyEvent.Key == Key.Esc)
			{
				Close();
				e.Handled = true;
			}
		};
	}

	public void SetGraphData(GraphData data)
	{
		Application.MainLoop.Invoke(() =>
		{
			_statusLabel.Text = $"Центр: {data.TrackLabels.GetValueOrDefault(data.CenterTrackId, data.CenterTrackId)}   |   " +
			                    $"Узлов: {data.TrackLabels.Count}   Рёбер: {data.Edges.Count}";

			_graphTextView.Text = RenderGraph(data);
		});
	}

	public void Show()
	{
		Application.Run(this);
	}

	public void Close()
	{
		Application.RequestStop(this);
	}

	private static string RenderGraph(GraphData data)
	{
		var sb = new System.Text.StringBuilder();

		var centerLabel = TruncateLabel(data.TrackLabels.GetValueOrDefault(data.CenterTrackId, data.CenterTrackId), 50);

		var outgoing = data.Edges
			.Where(e => e.FromId == data.CenterTrackId)
			.OrderByDescending(e => e.Weight)
			.ToList();

		var incoming = data.Edges
			.Where(e => e.ToId == data.CenterTrackId)
			.OrderByDescending(e => e.Weight)
			.ToList();

		var depth2Edges = data.Edges
			.Where(e => e.FromId != data.CenterTrackId && e.ToId != data.CenterTrackId)
			.OrderByDescending(e => e.Weight)
			.ToList();

		sb.AppendLine();
		sb.AppendLine($"  ● {centerLabel}");
		sb.AppendLine();

		if (outgoing.Count > 0)
		{
			sb.AppendLine("  ┌─ Переходы ПОСЛЕ этого трека:");
			foreach (var edge in outgoing)
			{
				string label = TruncateLabel(data.TrackLabels.GetValueOrDefault(edge.ToId, edge.ToId), 48);
				sb.AppendLine($"  │   ○ → {label}  ×{edge.Weight}");
			}
			sb.AppendLine("  │");
		}

		if (incoming.Count > 0)
		{
			sb.AppendLine("  ├─ Переходы ДО этого трека:");
			foreach (var edge in incoming)
			{
				string label = TruncateLabel(data.TrackLabels.GetValueOrDefault(edge.FromId, edge.FromId), 48);
				sb.AppendLine($"  │   ○ ← {label}  ×{edge.Weight}");
			}
			sb.AppendLine("  │");
		}

		if (depth2Edges.Count > 0)
		{
			sb.AppendLine("  └─ Связанные переходы (глубина 2):");

			var groupedByFrom = depth2Edges
				.GroupBy(e => e.FromId)
				.OrderByDescending(g => g.Sum(e => e.Weight))
				.Take(10);

			foreach (var group in groupedByFrom)
			{
				string fromLabel = TruncateLabel(data.TrackLabels.GetValueOrDefault(group.Key, group.Key), 42);
				sb.AppendLine($"      · {fromLabel}");

				foreach (var edge in group.Take(3))
				{
					string toLabel = TruncateLabel(data.TrackLabels.GetValueOrDefault(edge.ToId, edge.ToId), 40);
					sb.AppendLine($"           · → {toLabel}  ×{edge.Weight}");
				}
			}
		}

		if (outgoing.Count == 0 && incoming.Count == 0 && depth2Edges.Count == 0)
		{
			sb.AppendLine();
			sb.AppendLine("  Граф пуст — недостаточно данных в истории прослушивания.");
			sb.AppendLine("  Слушайте больше музыки, чтобы граф заполнился.");
		}

		return sb.ToString();
	}

	private static string TruncateLabel(string label, int maxLen)
	{
		if (label.Length <= maxLen) return label;
		return label[..(maxLen - 1)] + "…";
	}
}
