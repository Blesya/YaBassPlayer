using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class TracksPresenter : ITracksPresenter
{
	private readonly ITracksView _view;
	private readonly ITrackFileProvider _trackFileProvider;
	private readonly ITrackRepository _trackRepository;
	private readonly IPlaybackQueue _playbackQueue;

	private List<Track> _tracks = new();

	private const int TracksPerBatch = 50;

	public event Action<Track>? OnTrackChosen;

	public IPlaybackQueue PlaybackQueue => _playbackQueue;

	public TracksPresenter(ITracksView view, ITrackFileProvider trackFileProvider, ITrackRepository trackRepository, IPlaybackQueue playbackQueue)
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
	        _view.ClearTracks();
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