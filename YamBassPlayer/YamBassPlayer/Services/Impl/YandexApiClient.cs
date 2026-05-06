using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Concrete implementation of IYandexApiClient that delegates to the real YandexMusicApi.
/// This wrapper enables future mocking and decouples business logic from the external library.
/// </summary>
public sealed class YandexApiClient : IYandexApiClient
{
    private readonly YandexMusicApi _api;

    public YandexApiClient(YandexMusicApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public Task<YResponse<List<YTrack>>?> GetTracksAsync(AuthStorage storage, IEnumerable<string> trackIds)
        => _api.Track.GetAsync(storage, trackIds);

    public Task<YResponse<List<YTrack>>?> GetTrackAsync(AuthStorage storage, string trackId)
        => _api.Track.GetAsync(storage, trackId);

    public Task<string> GetTrackDownloadUrlAsync(AuthStorage storage, string trackId)
        => _api.Track.GetFileLinkAsync(storage, trackId);

    public Task<YResponse<YTrackSupplement>?> GetTrackSupplementAsync(AuthStorage storage, string trackId)
        => _api.Track.GetSupplementAsync(storage, trackId);
}
