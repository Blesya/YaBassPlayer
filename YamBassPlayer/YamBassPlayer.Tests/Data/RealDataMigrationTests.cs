using System.Text.Json;
using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Services.Impl.Migrations;

namespace YamBassPlayer.Tests.Data;

/// <summary>
/// Проверка миграций на копии реальных данных версии 1.1.0.
/// Путь к каталогу с playlist_state.json, tracks_cache.db и tracks/ задаётся
/// переменной окружения YAMBASS_VERIFY_DATA.
/// </summary>
[TestFixture]
[Explicit("Требует копию данных 1.1.0; путь в YAMBASS_VERIFY_DATA")]
[NonParallelizable]
public sealed class RealDataMigrationTests
{
    private string _workDir = null!;

    [SetUp]
    public void SetUp()
    {
        _workDir = Path.Combine(Path.GetTempPath(), "ybp-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
    }

    [TearDown]
    public void TearDown()
    {
        // Пул соединений Microsoft.Data.Sqlite держит файл базы открытым.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_workDir))
            Directory.Delete(_workDir, true);
    }

    private static string RequireVerifyData()
    {
        string? source = Environment.GetEnvironmentVariable("YAMBASS_VERIFY_DATA");
        if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
        {
            Assert.Fail(
                "Нужна копия данных 1.1.0: скопируйте playlist_state.json, tracks_cache.db и tracks/ " +
                "из bin/Debug/net8.0 в отдельный каталог и укажите его в YAMBASS_VERIFY_DATA.");
        }

        return source;
    }

    private string CopyData()
    {
        string source = RequireVerifyData();

        foreach (string file in Directory.GetFiles(source))
        {
            string name = Path.GetFileName(file);
            if (name is "playlist_state.json" or "tracks_cache.db")
                File.Copy(file, Path.Combine(_workDir, name), overwrite: true);
        }

        string tracksSource = Path.Combine(source, "tracks");
        if (Directory.Exists(tracksSource))
            CopyDirectory(tracksSource, Path.Combine(_workDir, "tracks"));

        return _workDir;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, destination, StringComparison.Ordinal), overwrite: true);
    }

    private MigratedSnapshot ReadLegacySnapshot()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_workDir, "playlist_state.json")));
        Assert.That(PlaylistSnapshotMigration.TryMigrate(document, out MigratedSnapshot? snapshot), Is.True);
        return snapshot!;
    }

    [Test]
    public void SnapshotMigrationKeepsEveryDataPlaylist()
    {
        CopyData();
        MigratedSnapshot legacy = ReadLegacySnapshot();

        var pluginState = new PluginStateFile(Path.Combine(_workDir, "plugin_state"));
        var state = new PlaylistStateStore(Path.Combine(_workDir, "playlist_state.json"), pluginState).Load();

        Assert.Multiple(() =>
        {
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.Playlists, Has.Count.EqualTo(legacy.State.Playlists.Count));
            Assert.That(state.Playlists, Has.All.Matches<Playlist>(p => !string.IsNullOrWhiteSpace(p.Kind)));
            Assert.That(File.Exists(Path.Combine(_workDir, "playlist_state.v1.json")), Is.True);
        });
    }

    [Test]
    public void SnapshotMigrationMovesFavoritesToPluginState()
    {
        CopyData();
        MigratedSnapshot legacy = ReadLegacySnapshot();

        var pluginState = new PluginStateFile(Path.Combine(_workDir, "plugin_state"));
        new PlaylistStateStore(Path.Combine(_workDir, "playlist_state.json"), pluginState).Load();

        List<string>? favoriteIds = pluginState.Get<List<string>>("yandex-music", "favoriteTrackIds");

        Assert.Multiple(() =>
        {
            Assert.That(favoriteIds, Is.Not.Null);
            Assert.That(favoriteIds!, Is.EquivalentTo(legacy.FavoriteTrackIds));
        });
    }

    [Test]
    public void SnapshotMigrationKeepsLastPlaylist()
    {
        CopyData();

        var pluginState = new PluginStateFile(Path.Combine(_workDir, "plugin_state"));
        var state = new PlaylistStateStore(Path.Combine(_workDir, "playlist_state.json"), pluginState).Load();

        Assert.That(state!.LastPlaylist, Is.Not.Null);
        Assert.That(state.LastTrack, Is.Not.Null);
    }

    [Test]
    public void DatabaseMigrationReachesVersionSeven()
    {
        CopyData();
        string dbPath = Path.Combine(_workDir, "tracks_cache.db");
        var factory = new SqliteConnectionFactory(dbPath);
        var pluginState = new PluginStateFile(Path.Combine(_workDir, "plugin_state"));

        long tracksBefore;
        using (SqliteConnection connection = factory.Create())
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Tracks";
            tracksBefore = (long)cmd.ExecuteScalar()!;
        }

        new DatabaseInitializer(factory, pluginState).Initialize();

        using (SqliteConnection connection = factory.Create())
        {
            using var versionCmd = connection.CreateCommand();
            versionCmd.CommandText = "PRAGMA user_version";
            long version = (long)versionCmd.ExecuteScalar()!;

            using var tracksCmd = connection.CreateCommand();
            tracksCmd.CommandText = "SELECT COUNT(*) FROM Tracks";
            long tracksAfter = (long)tracksCmd.ExecuteScalar()!;

            using var nullCmd = connection.CreateCommand();
            nullCmd.CommandText = "SELECT COUNT(*) FROM Tracks WHERE SourceType IS NULL OR SourceType = ''";
            long nullSources = (long)nullCmd.ExecuteScalar()!;

            using var favoriteTableCmd = connection.CreateCommand();
            favoriteTableCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='favoriteLocalTracks'";
            long favoriteTable = (long)favoriteTableCmd.ExecuteScalar()!;

            Assert.Multiple(() =>
            {
                Assert.That(version, Is.EqualTo(8));
                Assert.That(tracksAfter, Is.EqualTo(tracksBefore));
                Assert.That(nullSources, Is.Zero);
                Assert.That(favoriteTable, Is.EqualTo(1L));
            });
        }
    }

    [Test]
    public void TrackCacheMigrationMovesFlatFiles()
    {
        CopyData();
        string tracksDir = Path.Combine(_workDir, "tracks");
        int flatBefore = Directory.Exists(tracksDir)
            ? Directory.GetFiles(tracksDir, "*.mp3", SearchOption.TopDirectoryOnly).Length
            : 0;

        var migrator = new TrackCacheMigrator(tracksDir, new SourceId("yandex"));
        migrator.Migrate();
        migrator.Migrate();

        int flatAfter = Directory.GetFiles(tracksDir, "*.mp3", SearchOption.TopDirectoryOnly).Length;
        int nestedAfter = Directory.GetFiles(Path.Combine(tracksDir, "yandex"), "*.mp3", SearchOption.TopDirectoryOnly).Length;

        Assert.Multiple(() =>
        {
            Assert.That(flatAfter, Is.Zero);
            Assert.That(nestedAfter, Is.EqualTo(flatBefore));
        });
    }
}
