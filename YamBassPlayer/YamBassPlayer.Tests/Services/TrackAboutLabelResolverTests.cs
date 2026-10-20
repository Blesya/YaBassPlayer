using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class TrackAboutLabelResolverTests
{
	private static readonly Dictionary<string, YDisclaimer> Dictionary = new()
	{
		["blocked"] = new YDisclaimer
		{
			Id = "blocked",
			Type = "descriptionText",
			Title = "Трек недоступен по требованию Роскомнадзора",
		},
		["agent"] = new YDisclaimer
		{
			Id = "agent",
			Type = "foreignAgent",
			Title = "ИСПОЛНИТЕЛЬ ПРИЗНАН ИНОАГЕНТОМ",
		},
		["age"] = new YDisclaimer
		{
			Id = "age",
			Type = "descriptionText",
			Title = "Возрастное ограничение 18+",
		},
	};

	[Test]
	public void Resolve_MapsSimpleAndDictionaryLabels()
	{
		var labels = TrackAboutLabelResolver.Resolve(
			["explicit", "exclamationIcon:any", "descriptionText:blocked", "foreignAgent:agent"],
			Dictionary);

		Assert.That(labels.Select(l => l.Text), Is.EqualTo(new[]
		{
			"EXPLICIT",
			"Трек недоступен по требованию Роскомнадзора",
			"ИСПОЛНИТЕЛЬ ПРИЗНАН ИНОАГЕНТОМ",
		}));
	}

	[Test]
	public void Resolve_DeduplicatesSameText()
	{
		var labels = TrackAboutLabelResolver.Resolve(
			["foreignAgent", "foreignAgent:agent", "foreignAgent:agent"],
			Dictionary);

		Assert.That(labels, Has.Count.EqualTo(1));
	}

	[Test]
	public void Resolve_ReturnsEmpty_WhenTokensMissing()
	{
		Assert.That(TrackAboutLabelResolver.Resolve(null, Dictionary), Is.Empty);
	}

	[Test]
	public void Resolve_KeepsUnknownTokensOut()
	{
		var labels = TrackAboutLabelResolver.Resolve(["unknownThing", "unknownThing:id"], Dictionary);

		Assert.That(labels, Is.Empty);
	}

	[Test]
	public void FindBlockingReason_PrefersActualBlockReason()
	{
		var labels = TrackAboutLabelResolver.Resolve(
			["descriptionText:age", "descriptionText:blocked"],
			Dictionary);

		string? reason = TrackAboutLabelResolver.FindBlockingReason(labels);

		Assert.That(reason, Is.EqualTo("Трек недоступен по требованию Роскомнадзора"));
	}

	[Test]
	public void FindBlockingReason_IgnoresAgeRestriction()
	{
		var labels = TrackAboutLabelResolver.Resolve(["descriptionText:age"], Dictionary);

		Assert.That(TrackAboutLabelResolver.FindBlockingReason(labels), Is.Null);
	}
}
