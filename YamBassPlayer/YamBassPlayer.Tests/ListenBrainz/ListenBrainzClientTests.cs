using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

using YamBassPlayer.ListenBrainz;

namespace YamBassPlayer.Tests.ListenBrainz;

[TestFixture]
public sealed class ListenBrainzClientTests
{
	private static ListenBrainzOptions Options(string? token = "token") => new()
	{
		Token = token,
		UserName = "Blesya",
		MinRequestInterval = TimeSpan.Zero
	};

	private static (ListenBrainzClient Client, StubHandler Handler) Create(
		Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> respond,
		string? token = "token")
	{
		var handler = new StubHandler(respond);
		var client = new ListenBrainzClient(Options(token), new HttpClient(handler));
		return (client, handler);
	}

	[Test]
	public async Task ValidateToken_ReturnsUserName()
	{
		var (client, _) = Create(_ => (HttpStatusCode.OK,
			"""{"code":200,"valid":true,"user_name":"Blesya"}"""));

		Assert.That(await client.ValidateTokenAsync(), Is.EqualTo("Blesya"));
	}

	[Test]
	public async Task ValidateToken_WithoutToken_ReturnsNullAndMakesNoRequest()
	{
		var (client, handler) = Create(_ => (HttpStatusCode.OK, "{}"), token: null);

		Assert.That(await client.ValidateTokenAsync(), Is.Null);
		Assert.That(handler.Calls, Is.Empty);
	}

	[Test]
	public async Task GetRecommendations_NoContent_ReturnsNull()
	{
		var (client, _) = Create(_ => (HttpStatusCode.NoContent, string.Empty));

		Assert.That(await client.GetRecommendationsAsync("Blesya", 25), Is.Null);
	}

	[Test]
	public async Task GetRecommendations_MapsItemsAndTotal()
	{
		var (client, handler) = Create(_ => (HttpStatusCode.OK,
			"""
			{"payload":{"last_updated":1787653608,"count":2,"offset":0,"total_mbid_count":37,
			"mbids":[{"recording_mbid":"aaaa","score":0.5,"latest_listened_at":"2025-10-10T21:01:44.000Z"},
			{"recording_mbid":"bbbb","score":0.4}]}}
			"""));

		RecommendationBatch? batch = await client.GetRecommendationsAsync("Blesya", 25);

		Assert.That(batch, Is.Not.Null);
		Assert.That(batch!.Total, Is.EqualTo(37));
		Assert.That(batch.Items, Has.Count.EqualTo(2));
		Assert.That(batch.Items[0].RecordingMbid, Is.EqualTo("aaaa"));
		Assert.That(batch.Items[0].Score, Is.EqualTo(0.5));
		Assert.That(batch.Items[0].LastListenedAt, Is.Not.Null);
		Assert.That(batch.Items[1].LastListenedAt, Is.Null);
		Assert.That(handler.LastUrl, Does.Contain("/1/cf/recommendation/user/Blesya/recording"));
		Assert.That(handler.LastUrl, Does.Contain("count=25"));
	}

	[Test]
	public async Task GetMetadata_ReturnsNamesAndIsrc()
	{
		var (client, handler) = Create(_ => (HttpStatusCode.OK,
			"""
			{"aaaa":{"artist":{"name":"Сруб"},"recording":{"name":"Обернись","isrcs":["RUA1"],"length":200000}}}
			"""));

		IReadOnlyList<RecordingMetadata> metadata = await client.GetMetadataAsync(["aaaa"]);

		Assert.That(metadata, Has.Count.EqualTo(1));
		Assert.That(metadata[0].ArtistName, Is.EqualTo("Сруб"));
		Assert.That(metadata[0].TrackName, Is.EqualTo("Обернись"));
		Assert.That(metadata[0].DurationMs, Is.EqualTo(200000));
		Assert.That(metadata[0].Isrc, Is.EqualTo("RUA1"));
		Assert.That(handler.LastUrl, Does.Contain("inc=artist"));
	}

	[Test]
	public async Task GetRadio_ParsesJspfPlaylist()
	{
		var (client, handler) = Create(_ => (HttpStatusCode.OK,
			"""
			{"payload":{"feedback":["tag: using only seed tag 'russian post-punk'."],
			"jspf":{"playlist":{"title":"LB Radio for tag russian post-punk","track":[
			{"title":"Обернись","creator":"Сруб","album":"У зимы","duration":245000,
			"identifier":["https://musicbrainz.org/recording/cccc"]}]}}}}
			"""));

		RadioPlaylist? playlist = await client.GetRadioAsync("tag:(russian post-punk)");

		Assert.That(playlist, Is.Not.Null);
		Assert.That(playlist!.Tracks, Has.Count.EqualTo(1));
		Assert.That(playlist.Tracks[0].Creator, Is.EqualTo("Сруб"));
		Assert.That(playlist.Tracks[0].RecordingMbid, Is.EqualTo("cccc"));
		Assert.That(playlist.Tracks[0].DurationMs, Is.EqualTo(245000));
		Assert.That(playlist.Feedback, Has.Count.EqualTo(1));
		Assert.That(handler.LastUrl, Does.Contain("mode=easy"));
	}

	[Test]
	public async Task SubmitListen_SendsSnakeCasePayload()
	{
		var (client, handler) = Create(_ => (HttpStatusCode.OK, """{"status":"ok"}"""));

		await client.SubmitListenAsync(new ListenSubmission(
			DateTimeOffset.FromUnixTimeSeconds(1795000000),
			ArtistName: "Кино",
			TrackName: "Группа крови",
			ReleaseName: "Группа крови",
			DurationMs: 284000,
			PlayedSeconds: 120,
			MusicService: "music.yandex.ru"));

		using JsonDocument json = JsonDocument.Parse(handler.LastBody!);
		JsonElement root = json.RootElement;

		Assert.That(root.GetProperty("listen_type").GetString(), Is.EqualTo("single"));

		JsonElement listen = root.GetProperty("payload")[0];
		Assert.That(listen.GetProperty("listened_at").GetInt64(), Is.EqualTo(1795000000));

		JsonElement metadata = listen.GetProperty("track_metadata");
		Assert.That(metadata.GetProperty("artist_name").GetString(), Is.EqualTo("Кино"));
		Assert.That(metadata.GetProperty("track_name").GetString(), Is.EqualTo("Группа крови"));

		JsonElement additional = metadata.GetProperty("additional_info");
		Assert.That(additional.GetProperty("duration_ms").GetInt32(), Is.EqualTo(284000));
		Assert.That(additional.GetProperty("duration_played_ms").GetInt32(), Is.EqualTo(120000));
		Assert.That(additional.GetProperty("music_service").GetString(), Is.EqualTo("music.yandex.ru"));
		Assert.That(additional.GetProperty("submission_client").GetString(), Is.EqualTo("YamBassPlayer"));

		Assert.That(handler.LastAuthorization, Is.EqualTo("Token token"));
	}

	[Test]
	public async Task SubmitPlayingNow_SendsListenWithoutTimestamp()
	{
		var (client, handler) = Create(_ => (HttpStatusCode.OK, """{"status":"ok"}"""));

		await client.SubmitPlayingNowAsync(new ListenSubmission(
			DateTimeOffset.UtcNow,
			ArtistName: "Кино",
			TrackName: "Группа крови"));

		using JsonDocument json = JsonDocument.Parse(handler.LastBody!);
		JsonElement root = json.RootElement;

		Assert.That(root.GetProperty("listen_type").GetString(), Is.EqualTo("playing_now"));

		JsonElement listen = root.GetProperty("payload")[0];
		Assert.That(listen.TryGetProperty("listened_at", out _), Is.False,
			"playing_now не должен содержать listened_at");
		Assert.That(
			listen.GetProperty("track_metadata").GetProperty("track_name").GetString(),
			Is.EqualTo("Группа крови"));
	}

	[Test]
	public async Task SubmitListen_ZeroPlayedSeconds_OmitsDurationPlayed()
	{
		// Регресс: сервер отвечает 400 «should be a positive integer» на 0.
		var (client, handler) = Create(_ => (HttpStatusCode.OK, """{"status":"ok"}"""));

		await client.SubmitListenAsync(new ListenSubmission(
			DateTimeOffset.UtcNow,
			ArtistName: "Кино",
			TrackName: "Группа крови",
			PlayedSeconds: 0));

		using JsonDocument json = JsonDocument.Parse(handler.LastBody!);
		JsonElement additional = json.RootElement.GetProperty("payload")[0]
			.GetProperty("track_metadata")
			.GetProperty("additional_info");

		Assert.That(additional.TryGetProperty("duration_played_ms", out _), Is.False);
	}

	[Test]
	public async Task SubmitPlayingNow_OmitsDurationPlayed()
	{
		var (client, handler) = Create(_ => (HttpStatusCode.OK, """{"status":"ok"}"""));

		await client.SubmitPlayingNowAsync(new ListenSubmission(
			DateTimeOffset.UtcNow, ArtistName: "Кино", TrackName: "Группа крови"));

		using JsonDocument json = JsonDocument.Parse(handler.LastBody!);
		JsonElement additional = json.RootElement.GetProperty("payload")[0]
			.GetProperty("track_metadata")
			.GetProperty("additional_info");

		Assert.That(additional.TryGetProperty("duration_played_ms", out _), Is.False);
	}

	[Test]
	public async Task ClearPlayingNow_PostsDeleteWithClientName()
	{
		var (client, handler) = Create(_ => (HttpStatusCode.OK, """{"status":"ok"}"""));

		await client.ClearPlayingNowAsync();

		using JsonDocument json = JsonDocument.Parse(handler.LastBody!);
		Assert.That(json.RootElement.GetProperty("client").GetString(), Is.EqualTo("YamBassPlayer"));
		Assert.That(handler.LastUrl, Does.Contain("/1/playing-now/delete"));
	}

	[Test]
	public void UpdateCredentials_SwitchesUserAndToken()
	{
		var (client, _) = Create(_ => (HttpStatusCode.OK, "{}"), token: null);

		Assert.That(client.HasToken, Is.False);
		Assert.That(client.UserName, Is.EqualTo("Blesya"));

		client.UpdateCredentials("OtherUser", "new-token");

		Assert.That(client.HasToken, Is.True);
		Assert.That(client.UserName, Is.EqualTo("OtherUser"));
	}

	[Test]
	public void Client_HasNoConstructorThatCreatesItsOwnTransport()
	{
		// Правило проекта: тесты не обращаются к внешним API. Клиент обязан
		// получать HttpClient снаружи (в тестах — заглушка), иначе однажды
		// тест молча уйдёт в настоящую сеть.
		ConstructorInfo[] constructors = typeof(ListenBrainzClient).GetConstructors();

		Assert.That(constructors, Has.Length.EqualTo(1));
		Assert.That(
			constructors[0].GetParameters().Select(parameter => parameter.ParameterType),
			Is.EquivalentTo(new[] { typeof(ListenBrainzOptions), typeof(HttpClient) }));
	}

	[Test]
	public void ServerError_IsMappedToException()
	{
		var (client, _) = Create(_ => (HttpStatusCode.Unauthorized,
			"""{"code":401,"error":"The listens were rejected because your MetaBrainz account does not have a verified email address."}"""));

		var exception = Assert.ThrowsAsync<ListenBrainzException>(async () =>
			await client.SubmitListenAsync(new ListenSubmission(DateTimeOffset.UtcNow, "Кино", "Группа крови")));

		Assert.That(exception!.IsUnauthorized, Is.True);
		Assert.That(exception.ServerMessage, Does.Contain("verified email"));
	}

	private sealed class StubHandler(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> respond)
		: HttpMessageHandler
	{
		public List<string> Calls { get; } = [];

		public string? LastUrl { get; private set; }

		public string? LastBody { get; private set; }

		public string? LastAuthorization { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			LastUrl = request.RequestUri?.ToString();
			Calls.Add(LastUrl ?? string.Empty);

			if (request.Headers.TryGetValues("Authorization", out IEnumerable<string>? values))
				LastAuthorization = values.FirstOrDefault();

			LastBody = request.Content is null
				? null
				: await request.Content.ReadAsStringAsync(cancellationToken);

			var (status, body) = respond(request);
			var response = new HttpResponseMessage(status);
			if (!string.IsNullOrEmpty(body))
				response.Content = new StringContent(body, Encoding.UTF8, "application/json");

			return response;
		}
	}
}
