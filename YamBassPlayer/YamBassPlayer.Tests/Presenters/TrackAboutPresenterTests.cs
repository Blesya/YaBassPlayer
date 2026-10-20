using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class TrackAboutPresenterTests
{
	private Mock<ITrackAboutService> _trackAboutService = null!;
	private Mock<IViewFactory> _viewFactory = null!;
	private Mock<ITrackAboutView> _view = null!;
	private Mock<IPlaybackQueue> _playbackQueue = null!;
	private Mock<IErrorHandler> _errorHandler = null!;
	private TrackAboutPresenter _presenter = null!;

	[SetUp]
	public void SetUp()
	{
		_trackAboutService = new Mock<ITrackAboutService>();
		_viewFactory = new Mock<IViewFactory>();
		_view = new Mock<ITrackAboutView>();
		_playbackQueue = new Mock<IPlaybackQueue>();
		_errorHandler = new Mock<IErrorHandler>();

		_viewFactory.Setup(factory => factory.Create<ITrackAboutView>()).Returns(_view.Object);

		_presenter = new TrackAboutPresenter(
			_trackAboutService.Object,
			_viewFactory.Object,
			new FakeUiDispatcher(),
			_playbackQueue.Object,
			_errorHandler.Object);
	}

	[Test]
	public async Task ShowTrackAboutAsync_LoadsDataAndShowsWindow()
	{
		var info = new TrackAboutInfo
		{
			TrackId = "t1",
			Title = "Song",
			Artist = "Artist",
		};
		_trackAboutService.Setup(service => service.GetAsync("t1", false)).ReturnsAsync(info);

		await _presenter.ShowTrackAboutAsync("t1");

		Assert.Multiple(() =>
		{
			_trackAboutService.Verify(service => service.GetAsync("t1", false), Times.Once);
			_view.Verify(view => view.SetInfo(info), Times.Once);
			_view.Verify(view => view.Show(), Times.Once);
		});
	}

	[Test]
	public void ShowCurrentTrackAbout_WithoutCurrentTrack_DoesNothing()
	{
		_playbackQueue.SetupGet(queue => queue.CurrentTrackId).Returns((string?)null);

		_presenter.ShowCurrentTrackAbout();

		_trackAboutService.Verify(service => service.GetAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
	}

	[Test]
	public async Task ShowTrackAboutAsync_WhenServiceFails_ReportsError()
	{
		var error = new InvalidOperationException("boom");
		_trackAboutService
			.Setup(service => service.GetAsync("t1", false))
			.ThrowsAsync(error);

		await _presenter.ShowTrackAboutAsync("t1");

		_errorHandler.Verify(handler => handler.Handle(error), Times.Once);
		_view.Verify(view => view.Show(), Times.Never);
	}
}
