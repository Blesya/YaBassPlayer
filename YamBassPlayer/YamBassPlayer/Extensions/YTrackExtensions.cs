using YamBassPlayer.Models;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Extensions;

public static class YTrackExtensions
{
	public static Track ToTrack(this YTrack track)
	{
	    string artists = track.Artists != null
	        ? string.Join(", ", track.Artists.Select(a => a.Name))
	        : "Неизвестный исполнитель";

	    string album = track.Albums != null && track.Albums.Any()
	        ? track.Albums.First().Title
	        : "";

	    var trackVm = new Track(track.Title, artists, album, track.Id);
	    return trackVm;
	}
}