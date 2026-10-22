using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Tests.Data;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class TrackEnricherTests
{
	private SharedMemoryDbConnectionFactory _factory = null!;
	private string _stateDirectory = null!;

	[SetUp]
	public void SetUp()
	{
		_factory = new SharedMemoryDbConnectionFactory();
		_stateDirectory = Path.Combine(Path.GetTempPath(), $"track_enricher_state_{Guid.NewGuid():N}");

		new DatabaseInitializer(_factory, new PluginStateFile(_stateDirectory)).Initialize();
	}

	[TearDown]
	public void TearDown()
	{
		_factory?.Dispose();

		if (Directory.Exists(_stateDirectory))
			Directory.Delete(_stateDirectory, recursive: true);
	}

	[Test]
	public async Task TrackEnricher_DoesNotMixSameTrackIdFromTwoSources()
	{
		// Одинаковые TrackId, ArtistId и AlbumId из двух источников: различить их можно
		// только составным ключом (SourceType, Id).
		await WriteTrackAsync(YandexMusicSourceId.Value, "42", "Яндекс Артист", "Яндекс Альбом");
		await WriteTrackAsync(LocalMusicSource.Id, "42", "Локальный Артист", "Локальный Альбом");

		TrackRow[] rows =
		[
			CreateRow(YandexMusicSourceId.Value),
			CreateRow(LocalMusicSource.Id),
		];

		var enriched = await new TrackEnricher(_factory).EnrichAsync(rows);

		Assert.Multiple(() =>
		{
			Assert.That(enriched, Has.Count.EqualTo(2));

			Assert.That(enriched[0].SourceId, Is.EqualTo(YandexMusicSourceId.Value));
			Assert.That(enriched[0].Artists, Is.Not.Null);
			Assert.That(enriched[0].Artists!.Select(a => a.Name), Is.EqualTo(new[] { "Яндекс Артист" }));
			Assert.That(enriched[0].AlbumInfo?.Title, Is.EqualTo("Яндекс Альбом"));

			Assert.That(enriched[1].SourceId, Is.EqualTo(LocalMusicSource.Id));
			Assert.That(enriched[1].Artists, Is.Not.Null);
			Assert.That(enriched[1].Artists!.Select(a => a.Name), Is.EqualTo(new[] { "Локальный Артист" }));
			Assert.That(enriched[1].AlbumInfo?.Title, Is.EqualTo("Локальный Альбом"));
		});
	}

	private static TrackRow CreateRow(SourceId sourceId) => new(
		TrackId: "42",
		Artist: "Артист",
		Title: "Трек",
		Album: "Альбом",
		DurationMs: null,
		Year: null,
		CoverUrl: null,
		RemoteCoverUrl: null,
		LocalCoverPath: null,
		GenresJson: null,
		AlbumId: "album-1",
		SourceId: sourceId,
		LocalFilePath: null);

	private async Task WriteTrackAsync(SourceId sourceId, string trackId, string artistName, string albumTitle)
	{
		using var connection = _factory.Create();
		using var transaction = connection.BeginTransaction();
		using var commands = new TrackWriteCommands(connection, transaction);

		await commands.WriteAsync(new Track("Трек", artistName, albumTitle, trackId)
		{
			SourceId = sourceId,
			Artists = [new Artist("artist-1", artistName)],
			AlbumInfo = new Album("album-1", albumTitle),
		});

		transaction.Commit();
	}
}
