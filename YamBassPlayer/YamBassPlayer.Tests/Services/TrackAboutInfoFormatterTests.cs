using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class TrackAboutInfoFormatterTests
{
	[Test]
	public void Format_AvailableTrack_ShowsCreditsAndLabels()
	{
		var info = new TrackAboutInfo
		{
			TrackId = "t1",
			Title = "Song",
			Artist = "Artist",
			Album = "Album",
			IsAvailable = true,
			Labels = [new TrackAboutLabel("explicit", "EXPLICIT")],
			Credits =
			[
				new TrackAboutCredit("Исполнитель", "Artist"),
				new TrackAboutCredit("Автор музыки", "Composer"),
			],
			ArtistDescription = "Биография исполнителя.",
		};

		string text = TrackAboutInfoFormatter.Format(info);

		Assert.Multiple(() =>
		{
			Assert.That(text, Does.Contain("Исполнитель: Artist"));
			Assert.That(text, Does.Not.Contain("Доступность:"));
			Assert.That(text, Does.Contain("EXPLICIT"));
			Assert.That(text, Does.Contain("Автор музыки"));
			Assert.That(text, Does.Contain("Об исполнителе:"));
			Assert.That(text, Does.Not.Contain("Причина:"));
		});
	}

	[Test]
	public void Format_UnavailableTrack_ShowsReasonAndSubstitute()
	{
		var info = new TrackAboutInfo
		{
			TrackId = "t1",
			Title = "Song",
			Artist = "Artist",
			IsAvailable = false,
			UnavailableReason = "Трек недоступен по требованию Роскомнадзора",
			SubstituteTitle = "Other Artist — Other Song",
		};

		string text = TrackAboutInfoFormatter.Format(info);

		Assert.Multiple(() =>
		{
			Assert.That(text, Does.Contain("Доступность: Недоступен"));
			Assert.That(text, Does.Contain("Причина: Трек недоступен по требованию Роскомнадзора"));
			Assert.That(text, Does.Contain("Играет замена: Other Artist — Other Song"));
		});
	}

	[Test]
	public void Format_WithoutOptionalSections_DoesNotAddHeaders()
	{
		var info = new TrackAboutInfo
		{
			TrackId = "t1",
			Title = "Song",
			Artist = "Artist",
		};

		string text = TrackAboutInfoFormatter.Format(info);

		Assert.Multiple(() =>
		{
			Assert.That(text, Does.Not.Contain("Доступность:"));
			Assert.That(text, Does.Not.Contain("Метки:"));
			Assert.That(text, Does.Not.Contain("Кредиты:"));
			Assert.That(text, Does.Not.Contain("Об исполнителе:"));
		});
	}

	[Test]
	public void Format_ShowsNote()
	{
		var info = new TrackAboutInfo
		{
			TrackId = "t1",
			Title = "Song",
			Artist = "Artist",
			Note = "Не удалось получить данные из Яндекс.Музыки",
		};

		string text = TrackAboutInfoFormatter.Format(info);

		Assert.That(text, Does.Contain("Примечание: Не удалось получить данные из Яндекс.Музыки"));
	}
}
