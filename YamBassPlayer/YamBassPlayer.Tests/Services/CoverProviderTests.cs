using Moq;
using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class CoverProviderTests
{
	private string _root = null!;
	private CoverProvider _provider = null!;

	[SetUp]
	public void SetUp()
	{
		_root = Path.Combine(Path.GetTempPath(), "ybp-covers-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);
		var connectionFactory = new Mock<IDbConnectionFactory>();
		connectionFactory.Setup(f => f.Create()).Throws(new InvalidOperationException("no database"));
		_provider = new CoverProvider(
			_root,
			connectionFactory.Object,
			new Mock<IMusicSourceRegistry>().Object);
	}

	[TearDown]
	public void TearDown()
	{
		if (Directory.Exists(_root))
			Directory.Delete(_root, true);
	}

	[Test]
	public void GetCoverPath_UsesSourceFolder()
	{
		string path = _provider.GetCoverPath(new TrackKey(new SourceId("yandex"), "123"));

		Assert.That(path, Is.EqualTo(Path.Combine(_root, "yandex", "123.jpg")));
	}

	[Test]
	public void GetCoverPath_KeepsLocalCoverFlat()
	{
		string path = _provider.GetCoverPath(new TrackKey(LocalMusicSource.Id, @"C:\music\a.mp3"));

		Assert.Multiple(() =>
		{
			Assert.That(Path.GetDirectoryName(path), Is.EqualTo(_root));
			Assert.That(Path.GetFileNameWithoutExtension(path), Is.Not.EqualTo("a"));
		});
	}

	[Test]
	public void GetCoverPath_FindsLegacyFlatCover()
	{
		File.WriteAllText(Path.Combine(_root, "123.jpg"), "cover");

		Assert.That(_provider.IsCoverDownloaded(new TrackKey(new SourceId("yandex"), "123")), Is.True);
	}

	[Test]
	public async Task DownloadCoverAsync_LocalTrackUsesLocalFilePath()
	{
		string albumFolder = Path.Combine(_root, "album");
		Directory.CreateDirectory(albumFolder);
		string audio = Path.Combine(albumFolder, "song.mp3");
		File.WriteAllText(audio, "audio");
		string cover = Path.Combine(albumFolder, "cover.jpg");
		File.WriteAllText(cover, "cover");

		var track = new Track("t", "a", "al", audio)
		{
			SourceId = LocalMusicSource.Id,
			LocalFilePath = audio,
		};

		string result = await _provider.DownloadCoverAsync(track);

		Assert.That(result, Is.EqualTo(cover));
		Assert.That(Directory.Exists(Path.Combine(_root, "local")), Is.False);
	}
}
