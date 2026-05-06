using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Services;

/// <summary>
/// Abstraction over YandexMusicApi to decouple services from the concrete API class.
/// Enables mocking in tests and reduces direct dependency on external library.
/// </summary>
public interface IYandexApiClient
{
    Task<YResponse<List<YTrack>>?> GetTracksAsync(AuthStorage storage, IEnumerable<string> trackIds);
    Task<YResponse<List<YTrack>>?> GetTrackAsync(AuthStorage storage, string trackId);
    Task<string> GetTrackDownloadUrlAsync(AuthStorage storage, string trackId);
    Task<YResponse<YTrackSupplement>?> GetTrackSupplementAsync(AuthStorage storage, string trackId);
}
