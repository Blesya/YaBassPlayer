using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Data;

[TestFixture]
public sealed class TrackWriteCommandsTests
{
	private SharedMemoryDbConnectionFactory _factory = null!;
	private string _stateDirectory = null!;

	[SetUp]
	public void SetUp()
	{
		_factory = new SharedMemoryDbConnectionFactory();
		_stateDirectory = Path.Combine(Path.GetTempPath(), $"track_write_state_{Guid.NewGuid():N}");

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
	public async Task TrackWriteCommands_WritesArtistAndAlbumWithSource()
	{
		await WriteTrackAsync(YandexMusicSourceId.Value, "42", "Artist", "Album");

		using var check = _factory.Create();
		Assert.Multiple(() =>
		{
			Assert.That(
				Scalar(check, "SELECT SourceType FROM Artists WHERE Id = 'artist-1'"),
				Is.EqualTo("yandex"));
			Assert.That(
				Scalar(check, "SELECT SourceType FROM Albums WHERE Id = 'album-1'"),
				Is.EqualTo("yandex"));
			Assert.That(
				Scalar(
					check,
					"SELECT COUNT(*) FROM TrackArtists WHERE SourceType = 'yandex' AND TrackId = '42' AND ArtistId = 'artist-1'"),
				Is.EqualTo(1L));
		});
	}

	[Test]
	public async Task TrackWriteCommands_KeepsSameIdsFromTwoSources()
	{
		await WriteTrackAsync(YandexMusicSourceId.Value, "42", "Яндекс Артист", "Яндекс Альбом");
		await WriteTrackAsync(LocalMusicSource.Id, "42", "Локальный Артист", "Локальный Альбом");

		using var check = _factory.Create();
		Assert.Multiple(() =>
		{
			// Одинаковые TrackId/ArtistId/AlbumId из двух источников не слипаются.
			Assert.That(Scalar(check, "SELECT COUNT(*) FROM Tracks"), Is.EqualTo(2L));
			Assert.That(Scalar(check, "SELECT COUNT(*) FROM Artists"), Is.EqualTo(2L));
			Assert.That(Scalar(check, "SELECT COUNT(*) FROM Albums"), Is.EqualTo(2L));
			Assert.That(Scalar(check, "SELECT COUNT(*) FROM TrackArtists"), Is.EqualTo(2L));

			Assert.That(
				Scalar(check, "SELECT Name FROM Artists WHERE SourceType = 'yandex' AND Id = 'artist-1'"),
				Is.EqualTo("Яндекс Артист"));
			Assert.That(
				Scalar(check, "SELECT Name FROM Artists WHERE SourceType = 'local' AND Id = 'artist-1'"),
				Is.EqualTo("Локальный Артист"));
			Assert.That(
				Scalar(check, "SELECT Title FROM Albums WHERE SourceType = 'yandex' AND Id = 'album-1'"),
				Is.EqualTo("Яндекс Альбом"));
			Assert.That(
				Scalar(check, "SELECT Title FROM Albums WHERE SourceType = 'local' AND Id = 'album-1'"),
				Is.EqualTo("Локальный Альбом"));
		});
	}

	private async Task WriteTrackAsync(SourceId sourceId, string trackId, string artistName, string albumTitle)
	{
		using var connection = _factory.Create();
		using var transaction = connection.BeginTransaction();
		using var commands = new TrackWriteCommands(connection, transaction);

		await commands.WriteAsync(new Track($"Трек {trackId}", artistName, albumTitle, trackId)
		{
			SourceId = sourceId,
			Artists = [new Artist("artist-1", artistName)],
			AlbumInfo = new Album("album-1", albumTitle),
		});

		transaction.Commit();
	}

	private static object? Scalar(SqliteConnection connection, string sql)
	{
		using var command = connection.CreateCommand();
		command.CommandText = sql;
		return command.ExecuteScalar();
	}
}
