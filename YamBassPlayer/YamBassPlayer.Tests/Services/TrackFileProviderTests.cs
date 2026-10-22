using Moq;
using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class TrackFileProviderTests
{
	private string _root = null!;
	private TrackFileProvider _provider = null!;

	[SetUp]
	public void SetUp()
	{
		_root = Path.Combine(Path.GetTempPath(), "ybp-files-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);
		_provider = new TrackFileProvider(
			_root,
			new Mock<ITrackDownloadTracker>().Object,
			new Mock<IMusicSourceRegistry>().Object);
	}

	[TearDown]
	public void TearDown()
	{
		if (Directory.Exists(_root))
			Directory.Delete(_root, true);
	}

	[Test]
	public void GetTrackPath_PlacesFileUnderSourceFolder()
	{
		string path = _provider.GetTrackPath(new TrackKey(new SourceId("yandex"), "123"));

		Assert.That(path, Is.EqualTo(Path.Combine(_root, "yandex", "123.mp3")));
	}

	[Test]
	public void GetTrackPath_HashesRootedId()
	{
		string path = _provider.GetTrackPath(new TrackKey(new SourceId("local"), @"C:\music\a.mp3"));

		Assert.Multiple(() =>
		{
			Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Path.Combine(_root, "local")));
			Assert.That(Path.GetFileNameWithoutExtension(path), Is.Not.EqualTo("a"));
		});
	}

	[Test]
	public void IsTrackDownloaded_FallsBackToFlatFile()
	{
		File.WriteAllText(Path.Combine(_root, "123.mp3"), "audio");
		var track = new Track("t", "a", "al", "123") { SourceId = new SourceId("yandex") };

		Assert.That(_provider.IsTrackDownloaded(track), Is.True);
	}

	[Test]
	public void IsTrackDownloaded_UsesLocalFilePath()
	{
		string file = Path.Combine(_root, "song.mp3");
		File.WriteAllText(file, "audio");
		var track = new Track("t", "a", "al", file)
		{
			SourceId = LocalMusicSource.Id,
			LocalFilePath = file,
		};

		Assert.That(_provider.IsTrackDownloaded(track), Is.True);
	}
}
