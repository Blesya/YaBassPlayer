using Moq;

using YamBassPlayer.ListenBrainz;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class ListenBrainzServiceTests
{
	private static readonly TrackKey YandexKey = new(new SourceId("yandex"), "123");
	private static readonly TrackKey LocalKey = new(new SourceId("local"), "local/1");

	private Mock<IListenBrainzClient> _client = null!;
	private Mock<ITrackInfoProvider> _trackInfo = null!;
	private EventBus _eventBus = null!;
	private ListenBrainzService _service = null!;

	[SetUp]
	public void SetUp()
	{
		_client = new Mock<IListenBrainzClient>();
		_trackInfo = new Mock<ITrackInfoProvider>();
		_eventBus = new EventBus();
		_service = new ListenBrainzService(
			_client.Object,
			_trackInfo.Object,
			new ListenBrainzTrackResolver(_trackInfo.Object),
			_eventBus);

		_client.SetupGet(client => client.HasToken).Returns(true);
		_client.SetupGet(client => client.UserName).Returns("Blesya");
	}

	[Test]
	public async Task GetRecommendations_WithoutConfig_ReturnsEmpty()
	{
		_client.SetupGet(client => client.HasToken).Returns(false);

		IReadOnlyList<RecommendationTrack> result = await _service.GetRecommendationsAsync();

		Assert.That(result, Is.Empty);
		_client.Verify(
			client => client.GetRecommendationsAsync(
				It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Test]
	public async Task GetRecommendations_WhenServerHasNoData_ReturnsEmpty()
	{
		_client
			.Setup(client => client.GetRecommendationsAsync("Blesya", 25, 0, It.IsAny<CancellationToken>()))
			.ReturnsAsync((RecommendationBatch?)null);

		Assert.That(await _service.GetRecommendationsAsync(), Is.Empty);
	}

	[Test]
	public async Task GetRecommendations_MapsTracksAndResolvesThem()
	{
		_client
			.Setup(client => client.GetRecommendationsAsync("Blesya", 25, 0, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new RecommendationBatch([new RecommendationItem("aaaa", 0.5, null)], 1, null));

		_client
			.Setup(client => client.GetMetadataAsync(
				It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new RecordingMetadata("aaaa", "Сруб", "Обернись", 200000, "RUA1")]);

		_trackInfo
			.Setup(provider => provider.SearchTracks("Обернись", It.IsAny<int>()))
			.ReturnsAsync([new Track("Обернись", "Сруб", "У зимы", "local/1") { SourceId = new SourceId("local") }]);

		IReadOnlyList<RecommendationTrack> result = await _service.GetRecommendationsAsync();

		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Artist, Is.EqualTo("Сруб"));
		Assert.That(result[0].Title, Is.EqualTo("Обернись"));
		Assert.That(result[0].Score, Is.EqualTo(0.5));
		Assert.That(result[0].IsResolved, Is.True);
		Assert.That(result[0].Key, Is.EqualTo(LocalKey));
	}

	[Test]
	public async Task GetRecommendations_LeavesUnknownTrackUnresolved()
	{
		_client
			.Setup(client => client.GetRecommendationsAsync("Blesya", 25, 0, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new RecommendationBatch([new RecommendationItem("aaaa", 0.5, null)], 1, null));

		_client
			.Setup(client => client.GetMetadataAsync(
				It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([new RecordingMetadata("aaaa", "Кто-то", "Что-то", null, null)]);

		_trackInfo
			.Setup(provider => provider.SearchTracks(It.IsAny<string>(), It.IsAny<int>()))
			.ReturnsAsync([]);

		IReadOnlyList<RecommendationTrack> result = await _service.GetRecommendationsAsync();

		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].IsResolved, Is.False);
		Assert.That(result[0].Key, Is.Null);
	}

	[Test]
	public async Task GetRadioByTag_BuildsTagPrompt()
	{
		_client
			.Setup(client => client.GetRadioAsync("tag:(russian post-punk)", RadioMode.Easy, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new RadioPlaylist(
				"LB Radio",
				[new RadioTrack("Обернись", "Сруб", "У зимы", 245000, "cccc")],
				[]));

		_trackInfo
			.Setup(provider => provider.SearchTracks("Обернись", It.IsAny<int>()))
			.ReturnsAsync([new Track("Обернись", "Сруб", "У зимы", "local/1") { SourceId = new SourceId("local") }]);

		IReadOnlyList<RecommendationTrack> result = await _service.GetRadioByTagAsync("russian post-punk");

		Assert.That(result, Has.Count.EqualTo(1));
		Assert.That(result[0].Key, Is.EqualTo(LocalKey));
	}

	[Test]
	public async Task GetRadioByTag_EmptyTag_DoesNotCallServer()
	{
		Assert.That(await _service.GetRadioByTagAsync("   "), Is.Empty);
		_client.Verify(
			client => client.GetRadioAsync(
				It.IsAny<string>(), It.IsAny<RadioMode>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Test]
	public async Task SubmitListen_SendsSourceDomainAndPlayedTime()
	{
		_trackInfo
			.Setup(provider => provider.GetTrackInfoById(YandexKey))
			.ReturnsAsync(new Track("Группа крови", "Кино", "Группа крови", "123")
			{
				SourceId = new SourceId("yandex"),
				DurationMs = 284000
			});

		ListenSubmission? captured = null;
		_client
			.Setup(client => client.SubmitListenAsync(It.IsAny<ListenSubmission>(), It.IsAny<CancellationToken>()))
			.Callback<ListenSubmission, CancellationToken>((submission, _) => captured = submission)
			.Returns(Task.CompletedTask);

		await _service.SubmitListenAsync(YandexKey, playedSeconds: 42);

		Assert.That(captured, Is.Not.Null);
		Assert.That(captured!.ArtistName, Is.EqualTo("Кино"));
		Assert.That(captured.TrackName, Is.EqualTo("Группа крови"));
		Assert.That(captured.DurationMs, Is.EqualTo(284000));
		Assert.That(captured.PlayedSeconds, Is.EqualTo(42));
		Assert.That(captured.MusicService, Is.EqualTo("music.yandex.ru"));

		// listened_at — начало трека, а не момент события.
		Assert.That(captured.StartedAt, Is.LessThanOrEqualTo(DateTimeOffset.UtcNow));
	}

	[Test]
	public async Task SubmitListen_WithoutConfig_DoesNothing()
	{
		_client.SetupGet(client => client.HasToken).Returns(false);

		await _service.SubmitListenAsync(YandexKey, playedSeconds: 42);

		_client.Verify(
			client => client.SubmitListenAsync(It.IsAny<ListenSubmission>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Test]
	public async Task Start_SubmitsListen_WhenListenRecorded()
	{
		_trackInfo
			.Setup(provider => provider.GetTrackInfoById(LocalKey))
			.ReturnsAsync(new Track("Обернись", "Сруб", "У зимы", "local/1") { SourceId = new SourceId("local") });

		var submitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_client
			.Setup(client => client.SubmitListenAsync(It.IsAny<ListenSubmission>(), It.IsAny<CancellationToken>()))
			.Callback(() => submitted.TrySetResult())
			.Returns(Task.CompletedTask);

		_service.Start();
		_eventBus.Publish(new ListenRecordedEvent(LocalKey, 30));

		await submitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
	}

	[Test]
	public async Task Start_SendsPlayingNow_WhenTrackStarted()
	{
		_trackInfo
			.Setup(provider => provider.GetTrackInfoById(YandexKey))
			.ReturnsAsync(new Track("Группа крови", "Кино", "Группа крови", "123")
			{
				SourceId = new SourceId("yandex")
			});

		var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_client
			.Setup(client => client.SubmitPlayingNowAsync(It.IsAny<ListenSubmission>(), It.IsAny<CancellationToken>()))
			.Callback(() => sent.TrySetResult())
			.Returns(Task.CompletedTask);

		_service.Start();
		_eventBus.Publish(new TrackStartedEvent(YandexKey));

		await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
	}

	[Test]
	public async Task Start_ClearsPlayingNow_WhenPlaybackStopped()
	{
		var cleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_client
			.Setup(client => client.ClearPlayingNowAsync(It.IsAny<CancellationToken>()))
			.Callback(() => cleared.TrySetResult())
			.Returns(Task.CompletedTask);

		_service.Start();
		_eventBus.Publish(new PlaybackStateChangedEvent(PlaybackState.Stopped));

		await cleared.Task.WaitAsync(TimeSpan.FromSeconds(5));
	}

	[Test]
	public void Start_KeepsPlayingNow_WhenPlaybackPaused()
	{
		_service.Start();
		_eventBus.Publish(new PlaybackStateChangedEvent(PlaybackState.Paused));

		_client.Verify(
			client => client.ClearPlayingNowAsync(It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Test]
	public void UpdateCredentials_ForwardsToClient()
	{
		_service.UpdateCredentials("Blesya", "new-token");

		_client.Verify(client => client.UpdateCredentials("Blesya", "new-token"), Times.Once);
	}

	[Test]
	public void Start_IsIdempotent()
	{
		_service.Start();
		_service.Start();

		Assert.DoesNotThrow(() => _eventBus.Publish(new ListenRecordedEvent(LocalKey, 30)));
	}
}
