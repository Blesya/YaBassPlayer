using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters
{
	public class TracksPresenter
	{
		private readonly TracksView _view;
		private readonly ITracksService _tracksService;

		private List<Track> _tracks = new();

		private const int TracksPerBatch = 30;

		public event Action<Track>? OnTrackChosen;
		public event Action<Track> OnTrackForPlaySelected;

		public TracksPresenter(TracksView view, ITracksService tracksService)
		{
			_view = view;
			_tracksService = tracksService;

			_view.OnTrackSelected += OnTrackSelected;
			_view.NeedMoreTracks += OnNeedMoreTracks;
			_view.OnCellActivated += ViewOnOnTrackSelected;
		}

		public async void LoadTracksFor(Playlist playlist)
		{
			await _tracksService.SetPlaylist(playlist);

			IEnumerable<Track> trackBatch = await _tracksService.GetNextTracks(TracksPerBatch);
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

		private void ViewOnOnTrackSelected(int trackNumber)
		{
			OnTrackForPlaySelected?.Invoke(_tracks[trackNumber]);
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
			IEnumerable<Track> result = await _tracksService.GetNextTracks(TracksPerBatch);
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
