using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Превращает сырые токены <c>track.disclaimers</c> в понятные русские метки,
/// подставляя тексты из глобального словаря <c>/disclaimers</c>.
/// </summary>
public static class TrackAboutLabelResolver
{
	private static readonly Dictionary<string, string> SimpleLabels = new(StringComparer.OrdinalIgnoreCase)
	{
		["explicit"] = "EXPLICIT",
		["explicitIcon"] = "EXPLICIT",
		["age18"] = "18+",
		["age18Icon"] = "18+",
		["age16Icon"] = "16+",
		["age12Icon"] = "12+",
		["foreignAgent"] = "Исполнитель признан иноагентом",
		["drugs"] = "Приводит к возрастному ограничению",
		["obscenity"] = "Приводит к возрастному ограничению",
		["violence"] = "Приводит к возрастному ограничению",
	};

	private static readonly string[] BlockingReasonMarkers =
	[
		"недоступ",
		"удал",
		"роскомнадзор",
		"правоохран",
		"правообладател",
		"беларус",
		"заблокир",
	];

	public static IReadOnlyList<TrackAboutLabel> Resolve(
		IEnumerable<string>? rawTokens,
		IReadOnlyDictionary<string, YDisclaimer>? dictionary)
	{
		if (rawTokens is null)
			return [];

		var result = new List<TrackAboutLabel>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (string token in rawTokens)
		{
			if (string.IsNullOrWhiteSpace(token))
				continue;

			(string type, string? id) = SplitToken(token);
			TrackAboutLabel? label = ResolveOne(type, id, dictionary);

			if (label is null || !seen.Add(label.Text))
				continue;

			result.Add(label);
		}

		return result;
	}

	/// <summary>Первый текст, который действительно объясняет, почему трек недоступен.</summary>
	public static string? FindBlockingReason(IReadOnlyList<TrackAboutLabel> labels)
	{
		foreach (TrackAboutLabel label in labels)
		{
			if (BlockingReasonMarkers.Any(marker =>
					label.Text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
			{
				return label.Text;
			}
		}

		return null;
	}

	private static TrackAboutLabel? ResolveOne(
		string type,
		string? id,
		IReadOnlyDictionary<string, YDisclaimer>? dictionary)
	{
		// Иконка «!» — не текстовая метка, в окне она не нужна.
		if (type.Equals("exclamationIcon", StringComparison.OrdinalIgnoreCase))
			return null;

		if (id is not null && dictionary is not null && dictionary.TryGetValue(id, out YDisclaimer? disclaimer))
		{
			string? text = !string.IsNullOrWhiteSpace(disclaimer.Title)
				? disclaimer.Title
				: disclaimer.Description;

			if (!string.IsNullOrWhiteSpace(text))
				return new TrackAboutLabel(type, text.Trim());
		}

		return SimpleLabels.TryGetValue(type, out string? simple)
			? new TrackAboutLabel(type, simple)
			: null;
	}

	private static (string Type, string? Id) SplitToken(string token)
	{
		int separator = token.IndexOf(':');
		return separator < 0
			? (token, null)
			: (token[..separator], token[(separator + 1)..]);
	}
}
