using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class TrackCacheMigratorTests
{
	private string _root = null!;

	[SetUp]
	public void SetUp()
	{
		_root = Path.Combine(Path.GetTempPath(), "ybp-cache-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);
	}

	[TearDown]
	public void TearDown()
	{
		if (Directory.Exists(_root))
			Directory.Delete(_root, true);
	}

	[Test]
	public void MovesFlatFilesIntoSourceFolder()
	{
		File.WriteAllText(Path.Combine(_root, "1.mp3"), "audio");

		new TrackCacheMigrator(_root, new SourceId("yandex")).Migrate();

		Assert.Multiple(() =>
		{
			Assert.That(File.Exists(Path.Combine(_root, "yandex", "1.mp3")), Is.True);
			Assert.That(File.Exists(Path.Combine(_root, "1.mp3")), Is.False);
		});
	}

	[Test]
	public void IsIdempotent()
	{
		File.WriteAllText(Path.Combine(_root, "1.mp3"), "audio");
		var migrator = new TrackCacheMigrator(_root, new SourceId("yandex"));

		migrator.Migrate();
		migrator.Migrate();

		Assert.That(Directory.GetFiles(Path.Combine(_root, "yandex")).Length, Is.EqualTo(1));
	}

	[Test]
	public void KeepsExistingTarget()
	{
		string targetFolder = Path.Combine(_root, "yandex");
		Directory.CreateDirectory(targetFolder);
		File.WriteAllText(Path.Combine(targetFolder, "1.mp3"), "new");
		File.WriteAllText(Path.Combine(_root, "1.mp3"), "old");

		new TrackCacheMigrator(_root, new SourceId("yandex")).Migrate();

		Assert.Multiple(() =>
		{
			Assert.That(File.ReadAllText(Path.Combine(targetFolder, "1.mp3")), Is.EqualTo("new"));
			Assert.That(File.Exists(Path.Combine(_root, "1.mp3")), Is.True);
		});
	}
}
