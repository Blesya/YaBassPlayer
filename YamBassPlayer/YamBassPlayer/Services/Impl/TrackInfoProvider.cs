using Microsoft.Data.Sqlite;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

public class TrackInfoProvider : ITrackInfoProvider
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;


	public TrackInfoProvider(YandexMusicApi api, AuthStorage storage, SqliteConnection connection)
	{
	    _api = api;
	    _storage = storage;

	    using SqliteCommand cmd = connection.CreateCommand();
	    cmd.CommandText = @"
	        CREATE TABLE IF NOT EXISTS Tracks (
	            Id INTEGER PRIMARY KEY AUTOINCREMENT,
	            TrackId TEXT UNIQUE,
	            Artist TEXT,
	            Title TEXT,
	            Album TEXT,
	            UpdatedAt INTEGER
	        );";
	    cmd.ExecuteNonQuery();
	}

	public async Task<IEnumerable<Track>> GetTracksInfoByIds(IEnumerable<string> ids)
	{
	    var idsList = ids.ToList();
	    var tracksResult = new List<Track>();
	    var missingIds = new List<string>();

	    string dbPath = Path.Combine(AppContext.BaseDirectory, "tracks_cache.db");
	    await using var connection = new SqliteConnection($"Data Source={dbPath}");
	    await connection.OpenAsync();

	    foreach (string id in idsList)
	    {
	        await using SqliteCommand cmd = connection.CreateCommand();
	        cmd.CommandText = "SELECT TrackId, Artist, Title, Album FROM Tracks WHERE TrackId = @id";
	        cmd.Parameters.AddWithValue("@id", id);

	        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
	        if (await reader.ReadAsync())
	        {
	            var trackId = reader.GetString(0);
	            var artist = reader.GetString(1);
	            var title = reader.GetString(2);
	            var album = reader.GetString(3);

	            tracksResult.Add(new Track(title, artist, album, trackId));
	        }
	        else
	        {
	            missingIds.Add(id);
	        }
	    }

	    if (missingIds.Any())
	    {
	        YResponse<List<YTrack>>? yResponse = await _api.Track.GetAsync(_storage, missingIds);
	        List<YTrack>? yTracks = yResponse.Result;

	        foreach (YTrack yTrack in yTracks)
	        {
	            string artists = yTrack.Artists != null
	                ? string.Join(", ", yTrack.Artists.Select(a => a.Name))
	                : "Неизвестный исполнитель";

	            string album = yTrack.Albums != null && yTrack.Albums.Any()
	                ? yTrack.Albums.First().Title
	                : "";

	            var track = new Track(yTrack.Title, artists, album, yTrack.Id);
	                
	            await SaveAsync(track);
	                
	            tracksResult.Add(track);
	        }
	    }

	    return tracksResult;
	}

	public async Task<Track> GetTrackInfoById(string id)
	{
	    string dbPath = Path.Combine(AppContext.BaseDirectory, "tracks_cache.db");
	    await using var connection = new SqliteConnection($"Data Source={dbPath}");
	    await connection.OpenAsync();

	    await using SqliteCommand cmd = connection.CreateCommand();
	    cmd.CommandText = "SELECT TrackId, Artist, Title, Album, UpdatedAt FROM Tracks WHERE TrackId = @id";
	    cmd.Parameters.AddWithValue("@id", id);

	    await using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
	    if (await reader.ReadAsync())
	    {
	        var trackId = reader.GetString(0);
	        var artist = reader.GetString(1);
	        var title = reader.GetString(2);
	        var album = reader.GetString(3);

	        return new Track(title, artist, album, id);
	    }

	    Track? track = await TryGetFromApi(id);
	    if (track == null)
	        throw new ArgumentNullException(nameof(track), $"Не удалось получить информацию о треке: {id}");

	    return track;
	}

	private async Task<Track?> TryGetFromApi(string id)
	{
	    YResponse<List<YTrack>>? trackResponse = await _api.Track.GetAsync(_storage, id);
	    YTrack? track = trackResponse?.Result?.FirstOrDefault();

	    if (track == null)
	    {
	        return null;
	    }

	    Track trackVm = track.ToTrack();
	    await SaveAsync(trackVm);

	    return trackVm;
	}

	public async Task SaveAsync(Track track)
	{
	    string dbPath = Path.Combine(AppContext.BaseDirectory, "tracks_cache.db");
	    await using var connection = new SqliteConnection($"Data Source={dbPath}");
	    await connection.OpenAsync();

	    await using SqliteCommand cmd = connection.CreateCommand();
	    cmd.CommandText = @"
	        INSERT OR REPLACE INTO Tracks (TrackId, Artist, Title, Album, UpdatedAt)
	        VALUES (@TrackId, @artist, @title, @album, @updatedAt)";
	    cmd.Parameters.AddWithValue("@TrackId", track.Id ?? "");
	    cmd.Parameters.AddWithValue("@artist", track.Artist ?? "");
	    cmd.Parameters.AddWithValue("@title", track.Title ?? "");
	    cmd.Parameters.AddWithValue("@album", track.Album ?? "");
	    cmd.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

	    await cmd.ExecuteNonQueryAsync();
	}


	public async Task<bool> IsTrackCached(string trackId)
	{
	    string dbPath = Path.Combine(AppContext.BaseDirectory, "tracks_cache.db");
	    await using var connection = new SqliteConnection($"Data Source={dbPath}");
	    await connection.OpenAsync();

	    await using SqliteCommand cmd = connection.CreateCommand();
	    cmd.CommandText = "SELECT 1 FROM Tracks WHERE TrackId = @id LIMIT 1";
	    cmd.Parameters.AddWithValue("@id", trackId);

	    var result = await cmd.ExecuteScalarAsync();
	    return result != null;
	}

	public async Task<int> CountCachedTracks(IEnumerable<string> trackIds)
	{
	    int count = 0;
	    foreach (string trackId in trackIds)
	    {
	        if (!await IsTrackCached(trackId))
	            break;
	        count++;
	    }
	    return count;
	}
}