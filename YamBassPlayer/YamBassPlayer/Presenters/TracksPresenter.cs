using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters
{
    public class TracksPresenter
    {
        private readonly TracksView _view;
        private readonly TrackFileProvider _trackFileProvider;
        private readonly ITrackRepository _trackRepository;
        private readonly PlaybackQueue _playbackQueue;

        private List<Track> _tracks = new();

        private const int TracksPerBatch = 50;

        public event Action<Track>? OnTrackChosen;

        public PlaybackQueue PlaybackQueue => _playbackQueue;

        public TracksPresenter(TracksView view, TrackFileProvider trackFileProvider, ITrackRepository trackRepository, PlaybackQueue playbackQueue)
        {
            _view = view;
            _trackFileProvider = trackFileProvider;
            _trackRepository = trackRepository;
            _playbackQueue = playbackQueue;

            _view.OnTrackSelected += OnTrackSelected;
            _view.NeedMoreTracks += OnNeedMoreTracks;
            _view.OnCellActivated += ViewOnTrackSelected;
        }

        public async Task LoadTracksFor(Playlist playlist)
        {
            await _trackRepository.SetPlaylist(playlist);

            IEnumerable<Track> trackBatch = await _trackRepository.GetCachedTracksOrMinimum(TracksPerBatch);
            _tracks = trackBatch.ToList();

            if (_tracks.Count == 0)
            {
                _view.Clear();
            }
            else
            {
                _view.SetTracks(_tracks, _trackFileProvider.IsTrackDownloaded);
            }
        }

        private void ViewOnTrackSelected(int trackNumber)
        {
            var allTrackIds = _trackRepository.GetAllTrackIds();
            if (trackNumber < 0 || trackNumber >= allTrackIds.Count)
                return;

            _playbackQueue.SetQueue(allTrackIds, trackNumber);
        }

        private void OnTrackSelected(int index)
        {
            if (index < 0 || index >= _tracks.Count)
            {
                return;
            }

            OnTrackChosen?.Invoke(_tracks[index]);
        }

        private async void OnNeedMoreTracks()
        {
            IEnumerable<Track> result = await _trackRepository.GetNextTracks(TracksPerBatch);
            var more = result.ToList();
            if (more.Count == 0)
            {
                return;
            }

            _tracks.AddRange(more);
            _view.AddTracks(more, _trackFileProvider.IsTrackDownloaded);
        }
    }

}
