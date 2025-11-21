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

		private List<Track> _tracks = new();

		private const int TracksPerBatch = 50;

		private int _currentPlayedIndex = 0;

		public event Action<Track>? OnTrackChosen;
		public event Action<Track> OnTrackForPlaySelected;

		public TracksPresenter(TracksView view, TrackFileProvider trackFileProvider, ITrackRepository trackRepository)
		{
			_view = view;
			_trackFileProvider = trackFileProvider;
			_trackRepository = trackRepository;

			_view.OnTrackSelected += OnTrackSelected;
			_view.NeedMoreTracks += OnNeedMoreTracks;
			_view.OnCellActivated += ViewOnTrackSelected;

			AudioPlayer.OnTrackEnded += OnTrackEnded;
		}

		private async void OnTrackEnded(object? sender, EventArgs e)
		{
			await Task.Run(() =>
			{
				int trackNumber = ++_currentPlayedIndex;
				if (trackNumber == _tracks.Count - 1)
				{
					trackNumber = 0;
					_currentPlayedIndex = 0;
				}

				ViewOnTrackSelected(trackNumber);
			});
		}

		public async Task LoadTracksFor(Playlist playlist)
		{
			await _trackRepository.SetPlaylist(playlist);

			IEnumerable<Track> trackBatch = await _trackRepository.GetNextTracks(TracksPerBatch);
			_tracks = trackBatch.ToList();
				
			if (_tracks.Count == 0)
			{
				_view.Clear();
			}
			else
			{
				_view.SetTracks(_tracks);
			}
		}

		private void ViewOnTrackSelected(int trackNumber)
		{
			_currentPlayedIndex = trackNumber;
			Track track = _tracks[trackNumber];

			OnTrackForPlaySelected?.Invoke(track);
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
			_view.AddTracks(more);
		}
	}

}
