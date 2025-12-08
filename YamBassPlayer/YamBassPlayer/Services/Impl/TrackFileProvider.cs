using YamBassPlayer.Extensions;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer.Services.Impl;

public class TrackFileProvider : ITrackFileProvider
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly string _tracksFolder;

	public TrackFileProvider(YandexMusicApi api, AuthStorage storage, string tracksFolder)
	{
	    _api = api;
	    _storage = storage;
	    _tracksFolder = tracksFolder;

	    if (!Directory.Exists(_tracksFolder))
	    {
	        Directory.CreateDirectory(_tracksFolder);
	    }
	}

	public string GetTrackPath(string trackId)
	{
	    return Path.Combine(_tracksFolder, $"{trackId}.mp3");
	}

	public bool IsTrackDownloaded(string trackId)
	{
	    return File.Exists(GetTrackPath(trackId));
	}

	public async Task<string> DownloadTrackAsync(string trackId)
	{
	    try
	    {
	        string filePath = GetTrackPath(trackId);

	        if (File.Exists(filePath))
	        {
	            return filePath;
	        }

	        var trackResponse = await _api.Track.GetAsync(_storage, trackId);
	        var track = trackResponse?.Result?.FirstOrDefault();

	        if (track == null)
	        {
	            throw new Exception("Не удалось получить информацию о треке");
	        }

	        await _api.Track.ExtractToFileAsync(_storage, track, filePath);

	        return filePath;
	    }
	    catch (Exception e)
	    {
	        e.Handle();
	        return string.Empty;
	    }
	}
}