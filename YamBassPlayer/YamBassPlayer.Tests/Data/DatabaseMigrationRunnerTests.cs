using Microsoft.Data.Sqlite;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Services.Impl.Migrations;

namespace YamBassPlayer.Tests.Data;

[TestFixture]
public sealed class DatabaseMigrationRunnerTests
{
	private SharedMemoryDbConnectionFactory _factory = null!;
	private string _stateDirectory = null!;
	private IPluginStateFile _pluginState = null!;

	[SetUp]
	public void SetUp()
	{
		_factory = new SharedMemoryDbConnectionFactory();
		_stateDirectory = Path.Combine(Path.GetTempPath(), $"migration_state_{Guid.NewGuid():N}");
		_pluginState = new PluginStateFile(_stateDirectory);
	}

	[TearDown]
	public void TearDown()
	{
		_factory.Dispose();

		if (Directory.Exists(_stateDirectory))
			Directory.Delete(_stateDirectory, recursive: true);
	}

	[Test]
	public void Initialize_SetsCurrentVersion()
	{
		CreateInitializer().Initialize();

		using var connection = _factory.Create();
		Assert.Multiple(() =>
		{
			Assert.That(DatabaseMigrationRunner.GetUserVersion(connection), Is.EqualTo(9));
			Assert.That(SqliteSchemaHelper.HasTable(connection, "Tracks"), Is.True);
			Assert.That(SqliteSchemaHelper.HasTable(connection, "Artists"), Is.True);
			Assert.That(
				SqliteSchemaHelper.HasUniqueIndex(connection, "Tracks", "SourceType", "TrackId"),
				Is.True);
			Assert.That(
				SqliteSchemaHelper.HasUniqueIndex(connection, "Artists", "SourceType", "Id"),
				Is.True);
			Assert.That(
				SqliteSchemaHelper.HasUniqueIndex(connection, "Albums", "SourceType", "Id"),
				Is.True);
			Assert.That(
				SqliteSchemaHelper.HasUniqueIndex(connection, "TrackArtists", "SourceType", "TrackId", "ArtistId"),
				Is.True);
			Assert.That(SqliteSchemaHelper.HasColumn(connection, "listensHistory", "sourceId"), Is.True);
			Assert.That(SqliteSchemaHelper.HasColumn(connection, "listensHistory", "context"), Is.True);
			Assert.That(SqliteSchemaHelper.HasColumn(connection, "listensHistory", "source"), Is.False);
			Assert.That(SqliteSchemaHelper.HasTable(connection, "favoriteLocalTracks"), Is.True);
			Assert.That(SqliteSchemaHelper.HasTable(connection, "playlistCompositions"), Is.True);
			Assert.That(SqliteSchemaHelper.HasTable(connection, "playlistCompositionTracks"), Is.True);
		});
	}

	[Test]
	public void Initialize_IsIdempotent()
	{
		var initializer = CreateInitializer();

		Assert.DoesNotThrow(initializer.Initialize);
		Assert.DoesNotThrow(initializer.Initialize);

		using var connection = _factory.Create();
		Assert.Multiple(() =>
		{
			Assert.That(DatabaseMigrationRunner.GetUserVersion(connection), Is.EqualTo(9));
			Assert.That(
				SqliteSchemaHelper.HasUniqueIndex(connection, "Tracks", "SourceType", "TrackId"),
				Is.True);
		});
	}

	[Test]
	public void MigratesFromVersionFiveToCurrent()
	{
		using (var connection = _factory.Create())
		{
			Execute(connection, null, ReadFixture("tracks_schema_v5.sql"));
			DatabaseMigrationRunner.SetUserVersion(connection, null, 5);

			// Строки без источника: путь — локальный трек, всё остальное — Яндекс.
			// Пустая строка в SourceType не попадает под baseline-бэкфилл (он трогает только NULL/yandex),
			// поэтому её классифицирует именно миграция.
			Execute(
				connection,
				null,
				"""
				INSERT INTO Tracks (TrackId, Title, SourceType) VALUES
					('987654', 'Яндекс', NULL),
					('C:\music\track.mp3', 'Локальный', NULL),
					('D:\music\other.mp3', 'Локальный без источника', ''),
					('123456', 'Пустой источник', '');
				""");
		}

		CreateInitializer().Initialize();

		using var check = _factory.Create();

		Assert.Multiple(() =>
		{
			Assert.That(DatabaseMigrationRunner.GetUserVersion(check), Is.EqualTo(9));
			Assert.That(
				SqliteSchemaHelper.HasUniqueIndex(check, "Tracks", "SourceType", "TrackId"),
				Is.True);
			Assert.That(
				Scalar(check, "SELECT COUNT(*) FROM Tracks WHERE SourceType IS NULL OR SourceType = ''"),
				Is.EqualTo(0L));
			Assert.That(Scalar(check, "SELECT SourceType FROM Tracks WHERE TrackId = '987654'"), Is.EqualTo("yandex"));
			Assert.That(Scalar(check, "SELECT SourceType FROM Tracks WHERE TrackId = '123456'"), Is.EqualTo("yandex"));
			Assert.That(
				Scalar(check, """SELECT SourceType FROM Tracks WHERE TrackId = 'C:\music\track.mp3'"""),
				Is.EqualTo("local"));
			Assert.That(
				Scalar(check, """SELECT SourceType FROM Tracks WHERE TrackId = 'D:\music\other.mp3'"""),
				Is.EqualTo("local"));
			// Пересборка не теряет данные.
			Assert.That(Scalar(check, "SELECT Title FROM Tracks WHERE TrackId = '987654'"), Is.EqualTo("Яндекс"));
		});

		// Inline-уникальности TrackId больше нет: одинаковый идентификатор из двух источников разрешён.
		Assert.DoesNotThrow(() => Execute(
			check,
			null,
			"INSERT INTO Tracks (TrackId, Title, SourceType) VALUES ('987654', 'Локальная копия', 'local');"));

		// Составная уникальность при этом работает.
		Assert.Throws<SqliteException>(() => Execute(
			check,
			null,
			"INSERT INTO Tracks (TrackId, Title, SourceType) VALUES ('987654', 'Дубль', 'yandex');"));
	}

	[Test]
	public void MigratesFromVersionFiveToCurrent_IsIdempotent()
	{
		using (var connection = _factory.Create())
		{
			Execute(connection, null, ReadFixture("tracks_schema_v5.sql"));
			DatabaseMigrationRunner.SetUserVersion(connection, null, 5);
			Execute(
				connection,
				null,
				"INSERT INTO Tracks (TrackId, Title, SourceType) VALUES ('987654', 'Яндекс', NULL);");
		}

		CreateInitializer().Initialize();

		// Повторный прогон шага 5→6: HasUniqueIndex видит готовый индекс и не пересобирает таблицу.
		using (var connection = _factory.Create())
			DatabaseMigrationRunner.SetUserVersion(connection, null, 5);

		Assert.DoesNotThrow(CreateInitializer().Initialize);

		using var check = _factory.Create();
		Assert.Multiple(() =>
		{
			Assert.That(DatabaseMigrationRunner.GetUserVersion(check), Is.EqualTo(9));
			Assert.That(Scalar(check, "SELECT COUNT(*) FROM Tracks"), Is.EqualTo(1L));
			Assert.That(Scalar(check, "SELECT SourceType FROM Tracks WHERE TrackId = '987654'"), Is.EqualTo("yandex"));
		});
	}

	[Test]
	public void MigratesFromVersionSixToSeven()
	{
		PrepareVersionSix();

		CreateInitializer().Initialize();

		using var check = _factory.Create();
		Assert.Multiple(() =>
		{
			Assert.That(DatabaseMigrationRunner.GetUserVersion(check), Is.EqualTo(9));

			// listensHistory: source → context + sourceId.
			Assert.That(SqliteSchemaHelper.HasColumn(check, "listensHistory", "source"), Is.False);
			Assert.That(SqliteSchemaHelper.HasColumn(check, "listensHistory", "context"), Is.True);
			Assert.That(SqliteSchemaHelper.HasColumn(check, "listensHistory", "sourceId"), Is.True);

			// Справочники: источник в ключах.
			Assert.That(
				SqliteSchemaHelper.HasUniqueIndex(check, "Artists", "SourceType", "Id"),
				Is.True);
			Assert.That(
				SqliteSchemaHelper.HasUniqueIndex(check, "Albums", "SourceType", "Id"),
				Is.True);
			Assert.That(
				SqliteSchemaHelper.HasUniqueIndex(check, "TrackArtists", "SourceType", "TrackId", "ArtistId"),
				Is.True);

			// Локальное избранное переехало в состояние плагина.
			Assert.That(SqliteSchemaHelper.HasTable(check, "favoriteLocalTracks"), Is.True);

			// Backfill источника прослушивания: известный трек — по Tracks, неизвестный — по пути.
			Assert.That(
				Scalar(check, "SELECT sourceId FROM listensHistory WHERE trackId = '987654'"),
				Is.EqualTo("yandex"));
			Assert.That(
				Scalar(check, """SELECT sourceId FROM listensHistory WHERE trackId = 'C:\music\a.mp3'"""),
				Is.EqualTo("local"));
			Assert.That(
				Scalar(check, "SELECT sourceId FROM listensHistory WHERE trackId = 'unknown_track'"),
				Is.EqualTo("yandex"));

			// Сироты удалены, источник справочника взят из связанного трека.
			Assert.That(Scalar(check, "SELECT COUNT(*) FROM Artists WHERE Id = 'a-orphan'"), Is.EqualTo(0L));
			Assert.That(Scalar(check, "SELECT COUNT(*) FROM Albums WHERE Id = '333'"), Is.EqualTo(0L));
			Assert.That(
				Scalar(check, "SELECT COUNT(*) FROM TrackArtists WHERE TrackId = 'missing_track'"),
				Is.EqualTo(0L));
			Assert.That(Scalar(check, "SELECT SourceType FROM Artists WHERE Id = 'a-local'"), Is.EqualTo("local"));
			Assert.That(Scalar(check, "SELECT SourceType FROM Artists WHERE Id = 'a-yandex'"), Is.EqualTo("yandex"));
			Assert.That(Scalar(check, "SELECT SourceType FROM Albums WHERE Id = '222'"), Is.EqualTo("local"));

			// Дубль TrackId между источниками не размножает ссылку и не выбирает строку наугад.
			Assert.That(
				Scalar(check, "SELECT COUNT(*) FROM TrackArtists WHERE TrackId = '777'"),
				Is.EqualTo(1L));
			Assert.That(
				Scalar(check, "SELECT SourceType FROM TrackArtists WHERE TrackId = '777'"),
				Is.EqualTo("yandex"));
		});
	}

	[Test]
	public void MigratesFromVersionEightToNine_CreatesPlaylistCompositionTables()
	{
		CreateInitializer().Initialize();

		using (var connection = _factory.Create())
		{
			Execute(connection, null, "DROP TABLE playlistCompositionTracks;");
			Execute(connection, null, "DROP TABLE playlistCompositions;");
			DatabaseMigrationRunner.SetUserVersion(connection, null, 8);
		}

		CreateInitializer().Initialize();

		using var check = _factory.Create();
		Assert.Multiple(() =>
		{
			Assert.That(DatabaseMigrationRunner.GetUserVersion(check), Is.EqualTo(9));
			Assert.That(SqliteSchemaHelper.HasTable(check, "playlistCompositions"), Is.True);
			Assert.That(SqliteSchemaHelper.HasTable(check, "playlistCompositionTracks"), Is.True);
		});
	}

	[Test]
	public void Migration_006_To_007_MovesLocalFavoritesBeforeDrop()
	{
		PrepareVersionSix();

		CreateInitializer().Initialize();

		// Порядок таблицы — ORDER BY addedAt DESC.
		Assert.That(
			_pluginState.Get<List<string>>("local", "favoriteTrackIds"),
			Is.EqualTo(new[]
			{
				@"C:\music\newest.mp3",
				@"C:\music\middle.mp3",
				@"C:\music\old.mp3",
			}));

		using var check = _factory.Create();
		Assert.That(SqliteSchemaHelper.HasTable(check, "favoriteLocalTracks"), Is.True);
	}

	[Test]
	public void Migration_006_To_007_IsIdempotent()
	{
		PrepareVersionSix();

		CreateInitializer().Initialize();

		// Повторный прогон шага 6→7 на уже перенесённых данных.
		using (var connection = _factory.Create())
			DatabaseMigrationRunner.SetUserVersion(connection, null, 6);

		Assert.DoesNotThrow(CreateInitializer().Initialize);
		Assert.DoesNotThrow(CreateInitializer().Initialize);

		Assert.That(
			_pluginState.Get<List<string>>("local", "favoriteTrackIds"),
			Is.EqualTo(new[]
			{
				@"C:\music\newest.mp3",
				@"C:\music\middle.mp3",
				@"C:\music\old.mp3",
			}));

		using var check = _factory.Create();
		Assert.Multiple(() =>
		{
			Assert.That(DatabaseMigrationRunner.GetUserVersion(check), Is.EqualTo(9));
			Assert.That(SqliteSchemaHelper.HasTable(check, "favoriteLocalTracks"), Is.True);
		});
	}

	[Test]
	public void Migration_MapsMyWaveContextToRadio()
	{
		PrepareVersionSix();

		CreateInitializer().Initialize();

		using var check = _factory.Create();
		Assert.Multiple(() =>
		{
			Assert.That(
				Scalar(check, """SELECT context FROM listensHistory WHERE trackId = 'C:\music\a.mp3'"""),
				Is.EqualTo("Radio"));
			Assert.That(
				Scalar(check, "SELECT context FROM listensHistory WHERE trackId = 'unknown_track'"),
				Is.EqualTo("Regular"));
			Assert.That(
				Scalar(check, "SELECT context FROM listensHistory WHERE trackId = '987654'"),
				Is.EqualTo("Regular"));
		});
	}

	[Test]
	public void Migrate_AppliesChain_FromCurrentToTarget()
	{
		using var connection = _factory.Create();
		DatabaseMigrationRunner.SetUserVersion(connection, null, 5);

		var runner = new DatabaseMigrationRunner([
			new FakeMigration(5, 6, (c, tx) => Execute(c, tx, "CREATE TABLE Step6 (Id INTEGER);")),
			new FakeMigration(6, 7, (c, tx) => Execute(c, tx, "CREATE TABLE Step7 (Id INTEGER);")),
		]);

		int version = runner.Migrate(connection, 5, 7);

		Assert.That(version, Is.EqualTo(7));
		Assert.That(DatabaseMigrationRunner.GetUserVersion(connection), Is.EqualTo(7));
		Assert.That(SqliteSchemaHelper.HasTable(connection, "Step6"), Is.True);
		Assert.That(SqliteSchemaHelper.HasTable(connection, "Step7"), Is.True);
	}

	[Test]
	public void Migrate_NoOp_WhenAlreadyAtTarget()
	{
		using var connection = _factory.Create();
		DatabaseMigrationRunner.SetUserVersion(connection, null, 5);

		var runner = new DatabaseMigrationRunner([]);

		int version = runner.Migrate(connection, 5, 5);

		Assert.That(version, Is.EqualTo(5));
	}

	[Test]
	public void Migrate_Throws_WhenDatabaseIsNewerThanApplication()
	{
		using var connection = _factory.Create();
		DatabaseMigrationRunner.SetUserVersion(connection, null, 7);

		var runner = new DatabaseMigrationRunner([]);

		Assert.Throws<InvalidOperationException>(() => runner.Migrate(connection, 7, 5));
	}

	[Test]
	public void Migrate_Throws_WhenChainHasGap()
	{
		Assert.Throws<InvalidOperationException>(() => new DatabaseMigrationRunner([
			new FakeMigration(5, 6, (_, _) => { }),
			new FakeMigration(7, 8, (_, _) => { }),
		]));
	}

	[Test]
	public void Migrate_RollsBack_WhenMigrationThrows()
	{
		using var connection = _factory.Create();
		DatabaseMigrationRunner.SetUserVersion(connection, null, 5);

		var runner = new DatabaseMigrationRunner([
			new FakeMigration(5, 6, (c, tx) =>
			{
				Execute(c, tx, "CREATE TABLE HalfApplied (Id INTEGER);");
				throw new InvalidOperationException("boom");
			}),
		]);

		Assert.Throws<InvalidOperationException>(() => runner.Migrate(connection, 5, 6));

		Assert.That(DatabaseMigrationRunner.GetUserVersion(connection), Is.EqualTo(5));
		Assert.That(SqliteSchemaHelper.HasTable(connection, "HalfApplied"), Is.False);
	}

	private DatabaseInitializer CreateInitializer() => new(_factory, _pluginState);

	/// <summary>Готовит БД версии 6 из raw-SQL фикстуры.</summary>
	private void PrepareVersionSix()
	{
		using var connection = _factory.Create();
		Execute(connection, null, ReadFixture("tracks_schema_v6.sql"));
		DatabaseMigrationRunner.SetUserVersion(connection, null, 6);
	}

	private static string ReadFixture(string fileName)
		=> File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Fixtures", fileName));

	private static object? Scalar(SqliteConnection connection, string sql)
	{
		using var command = connection.CreateCommand();
		command.CommandText = sql;
		return command.ExecuteScalar();
	}

	private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql)
	{
		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = sql;
		command.ExecuteNonQuery();
	}

	private sealed class FakeMigration(
		int fromVersion,
		int toVersion,
		Action<SqliteConnection, SqliteTransaction> up) : IDatabaseMigration
	{
		public int FromVersion { get; } = fromVersion;
		public int ToVersion { get; } = toVersion;
		public string Description => $"fake {FromVersion}->{ToVersion}";
		public void Up(SqliteConnection connection, SqliteTransaction transaction) => up(connection, transaction);
	}
}
