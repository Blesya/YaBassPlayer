using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Presenters.Impl;

public class OnSameWavePresenter : IOnSameWavePresenter
{
    private readonly IRecommendationService _recommendationService;
    private readonly IPlaybackQueue _playbackQueue;
    private readonly ITrackRepository _trackRepository;
    private readonly ITracksPresenter _tracksPresenter;
    private readonly IPlayStatusPresenter _playStatusPresenter;
    private readonly ITrackInfoProvider _trackInfoProvider;

    public OnSameWavePresenter(
    IRecommendationService recommendationService,
    IPlaybackQueue playbackQueue,
    ITrackRepository trackRepository,
    ITracksPresenter tracksPresenter,
    IPlayStatusPresenter playStatusPresenter,
    ITrackInfoProvider trackInfoProvider)
    {
        _recommendationService = recommendationService;
        _playbackQueue = playbackQueue;
        _trackRepository = trackRepository;
        _tracksPresenter = tracksPresenter;
        _playStatusPresenter = playStatusPresenter;
        _trackInfoProvider = trackInfoProvider;
    }

    public async Task<Playlist?> ShowOnSameWaveAsync()
    {
        try
        {
            var currentTrackId = _playbackQueue.CurrentTrackId;
            if (currentTrackId == null)
            {
                _playStatusPresenter.SetPlayStatus("Сначала начните воспроизведение трека");
                return null;
            }

            _playStatusPresenter.SetPlayStatus("Подбираем рекомендации...");

            var result = await _recommendationService.GetRecommendationsAsync(currentTrackId);

            if (result.InsufficientData)
            {
                _playStatusPresenter.SetPlayStatus("Недостаточно данных для рекомендаций");
                return null;
            }

            var reasonByTrackId = result.Tracks.ToDictionary(t => t.TrackId, t => t.Reason);
            var trackIds = result.Tracks.Select(t => t.TrackId).ToList();

            var resolvedTracks = await _trackInfoProvider.GetTracksInfoByIds(trackIds);
            var enrichedTracks = resolvedTracks.Select(t => new Track(t.Title, t.Artist, t.Album, t.Id)
            {
                Subtitle = reasonByTrackId.TryGetValue(t.Id, out var reason) ? reason.ToDisplayString() : null
            }).ToList();

            _trackRepository.UpdateOnSameWaveCache(enrichedTracks);

            var baseTrack = await _trackInfoProvider.GetTrackInfoById(currentTrackId);
            var description = $"На основе: {baseTrack.Artist} - {baseTrack.Title}";

            var playlist = new Playlist("На одной волне", PlaylistType.OnSameWave)
            {
                Description = description,
                TrackCount = enrichedTracks.Count
            };

            await _tracksPresenter.LoadTracksFor(playlist);
            return playlist;
        }
        catch (Exception exception)
        {
            exception.Handle();
            return null;
        }
    }
}
