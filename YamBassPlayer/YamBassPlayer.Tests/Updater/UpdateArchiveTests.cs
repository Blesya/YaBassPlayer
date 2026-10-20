using System.IO.Compression;
using YamBassPlayer.Updater;

namespace YamBassPlayer.Tests.Updater;

[TestFixture]
public sealed class UpdateArchiveTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ybp-archive-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [TestCase("YamBassPlayer.exe", true)]
    [TestCase("YAMBASSPLAYER.EXE", true)]
    [TestCase("YamBassPlayer.Updater.exe", true)]
    [TestCase("bass.dll", true)]
    [TestCase("e_sqlite3.dll", true)]
    [TestCase("appsettings.example.json", true)]
    [TestCase("appsettings.json", false)]
    [TestCase("tracks_cache.db", false)]
    [TestCase("playlist_state.json", false)]
    public void IsPayloadFile_MatchesWhitelist(string fileName, bool expected)
    {
        Assert.That(UpdateArchive.IsPayloadFile(fileName), Is.EqualTo(expected));
    }

    [Test]
    public void Validate_ArchiveWithApplicationAndNatives_Passes()
    {
        string zip = CreateZip("YamBassPlayer.exe", "bass.dll", "bass_fx.dll");

        Assert.DoesNotThrow(() => UpdateArchive.Validate(zip));
    }

    [Test]
    public void Validate_ArchiveWithoutApplication_Throws()
    {
        string zip = CreateZip("bass.dll");

        Assert.Throws<InvalidDataException>(() => UpdateArchive.Validate(zip));
    }

    [Test]
    public void Validate_NestedEntry_Throws()
    {
        string zip = CreateZip("YamBassPlayer.exe", "sub/bass.dll");

        Assert.Throws<InvalidDataException>(() => UpdateArchive.Validate(zip));
    }

    [Test]
    public void Validate_PathTraversal_Throws()
    {
        string zip = CreateZip("YamBassPlayer.exe", "../evil.dll");

        Assert.Throws<InvalidDataException>(() => UpdateArchive.Validate(zip));
    }

    [Test]
    public void ExtractPayload_TakesOnlyWhitelistedFiles()
    {
        string zip = CreateZip("YamBassPlayer.exe", "bass.dll", "appsettings.example.json", "appsettings.json", "notes.txt");
        string staging = Path.Combine(_directory, "staging");

        IReadOnlyList<string> extracted = UpdateArchive.ExtractPayload(zip, staging);

        string[] names = extracted.Select(Path.GetFileName).ToArray()!;
        Assert.That(names, Is.EquivalentTo(new[] { "YamBassPlayer.exe", "appsettings.example.json", "bass.dll" }));
        Assert.That(File.Exists(Path.Combine(staging, "appsettings.json")), Is.False);
        Assert.That(File.Exists(Path.Combine(staging, "notes.txt")), Is.False);
    }

    private string CreateZip(params string[] entryNames)
    {
        string zipPath = Path.Combine(_directory, $"{Guid.NewGuid():N}.zip");

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        foreach (string entryName in entryNames)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using Stream stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write(entryName);
        }

        return zipPath;
    }
}
