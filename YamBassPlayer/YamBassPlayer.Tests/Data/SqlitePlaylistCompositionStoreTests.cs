using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Data;

[TestFixture]
public sealed class SqlitePlaylistCompositionStoreTests
{
	private static readonly SourceId Yandex = YandexMusicSourceId.Value;

	private SharedMemoryDbConnectionFactory _factory = null!;
	private DbWriteLock _writeLock = null!;
	private SqlitePlaylistCompositionStore _store = null!;

	[SetUp]
	public void SetUp()
	{
		_factory = new SharedMemoryDbConnectionFactory();
		_writeLock = new DbWriteLock();

		using SqliteConnection connection = _factory.Create();
		using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			CREATE TABLE playlistCompositions (
				playlistKey TEXT PRIMARY KEY,
				savedAt     TEXT NOT NULL
			);

			CREATE TABLE playlistCompositionTracks (
				playlistKey TEXT NOT NULL,
				position    INTEGER NOT NULL,
				sourceId    TEXT NOT NULL,
				trackId     TEXT NOT NULL,
				PRIMARY KEY (playlistKey, position)
			);
			""";
		command.ExecuteNonQuery();

		_store = new SqlitePlaylistCompositionStore(_factory, _writeLock);
	}

	[TearDown]
	public void TearDown()
	{
		_writeLock.Dispose();
		_factory.Dispose();
	}

	private static TrackKey K(string id) => new(Yandex, id);

	[Test]
	public async Task TryLoad_WhenNothingSaved_ReturnsNull()
	{
		IReadOnlyList<TrackKey>? keys = await _store.TryLoadAsync("42:7");

		Assert.That(keys, Is.Null);
	}

	[Test]
	public async Task Save_ThenLoad_ReturnsSameOrder()
	{
		await _store.SaveAsync("42:7", [K("10"), K("20"), K("30")]);

		IReadOnlyList<TrackKey>? keys = await _store.TryLoadAsync("42:7");

		Assert.That(keys, Is.EqualTo(new[] { K("10"), K("20"), K("30") }));
	}

	[Test]
	public async Task Save_ReplacesPreviousContent()
	{
		await _store.SaveAsync("42:7", [K("10"), K("20")]);
		await _store.SaveAsync("42:7", [K("30")]);

		IReadOnlyList<TrackKey>? keys = await _store.TryLoadAsync("42:7");

		Assert.That(keys, Is.EqualTo(new[] { K("30") }));
	}

	[Test]
	public async Task Save_KeepsPlaylistsIndependent()
	{
		await _store.SaveAsync("42:7", [K("10")]);
		await _store.SaveAsync("42:8", [K("20")]);

		IReadOnlyList<TrackKey>? own = await _store.TryLoadAsync("42:7");
		IReadOnlyList<TrackKey>? liked = await _store.TryLoadAsync("42:8");

		Assert.Multiple(() =>
		{
			Assert.That(own, Is.EqualTo(new[] { K("10") }));
			Assert.That(liked, Is.EqualTo(new[] { K("20") }));
		});
	}

	[Test]
	public async Task GetKeys_ReturnsEverySavedPlaylist()
	{
		await _store.SaveAsync("42:7", [K("10")]);
		await _store.SaveAsync("42:8", [K("20")]);

		IReadOnlyCollection<string> keys = await _store.GetKeysAsync();

		Assert.That(keys, Is.EquivalentTo(new[] { "42:7", "42:8" }));
	}

	[Test]
	public async Task Remove_DropsComposition()
	{
		await _store.SaveAsync("42:7", [K("10")]);
		await _store.SaveAsync("42:8", [K("20")]);

		await _store.RemoveAsync(["42:7"]);

		IReadOnlyList<TrackKey>? removed = await _store.TryLoadAsync("42:7");
		IReadOnlyList<TrackKey>? kept = await _store.TryLoadAsync("42:8");

		Assert.Multiple(() =>
		{
			Assert.That(removed, Is.Null);
			Assert.That(kept, Is.EqualTo(new[] { K("20") }));
		});
	}

	[Test]
	public async Task SavedComposition_SurvivesNewStoreInstance()
	{
		await _store.SaveAsync("42:7", [K("10")]);

		var another = new SqlitePlaylistCompositionStore(_factory, _writeLock);

		Assert.That(await another.TryLoadAsync("42:7"), Is.EqualTo(new[] { K("10") }));
	}

	[Test]
	public async Task Save_EmptyList_DoesNothing()
	{
		await _store.SaveAsync("42:7", []);

		Assert.That(await _store.TryLoadAsync("42:7"), Is.Null);
	}
}
