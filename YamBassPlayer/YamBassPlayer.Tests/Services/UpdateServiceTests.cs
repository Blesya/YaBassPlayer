using System.Net;
using System.Security.Cryptography;
using System.Text;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class UpdateServiceTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "ybp-update-service", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public async Task DownloadAsync_SavesFileAndVerifiesChecksum()
    {
        byte[] payload = Encoding.UTF8.GetBytes("distributive");
        string checksum = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var service = CreateService(payload, checksum);

        string path = await service.DownloadAsync(CreateInfo(), progress: null, CancellationToken.None);

        Assert.That(File.Exists(path), Is.True);
        Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(payload));
    }

    [Test]
    public async Task DownloadAsync_ChecksumMismatch_ThrowsAndRemovesFile()
    {
        byte[] payload = Encoding.UTF8.GetBytes("distributive");
        var service = CreateService(payload, new string('0', 64));

        Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadAsync(CreateInfo(), progress: null, CancellationToken.None));

        string directory = Path.Combine(_tempRoot, "update-0.0.2");
        string[] files = Directory.Exists(directory) ? Directory.GetFiles(directory) : Array.Empty<string>();
        Assert.That(files, Is.Empty);
    }

    [Test]
    public async Task DownloadAsync_ReportsProgress()
    {
        byte[] payload = new byte[256 * 1024];
        string checksum = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var service = CreateService(payload, checksum);
        var reported = new ListProgress();

        await service.DownloadAsync(CreateInfo(), reported, CancellationToken.None);

        Assert.That(reported.Values, Is.Not.Empty);
        Assert.That(reported.Values[^1], Is.EqualTo(100), $"получено: {string.Join(", ", reported.Values)}");
    }

    private UpdateService CreateService(byte[] payload, string checksum)
        => new(new StubFeed(), new HttpClient(new StubHandler(payload, checksum)), _tempRoot);

    private static UpdateInfo CreateInfo() => new(
        new Version(0, 0, 2),
        "v0.0.2",
        "YamBassPlayer-0.0.2-win-x64.zip",
        "https://example.org/app.zip",
        0,
        null,
        "https://example.org/app.zip.sha256");

    private sealed class StubFeed : IReleaseFeed
    {
        public Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<UpdateInfo?>(null);
    }

    /// <summary>Собирает проценты синхронно, в порядке вызова — <see cref="Progress{T}"/> так не умеет.</summary>
    private sealed class ListProgress : IProgress<int>
    {
        public List<int> Values { get; } = [];

        public void Report(int value) => Values.Add(value);
    }

    private sealed class StubHandler(byte[] payload, string checksum) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            bool isChecksum = request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase);

            HttpContent content = isChecksum
                ? new StringContent($"{checksum}  YamBassPlayer-0.0.2-win-x64.zip")
                : new ByteArrayContent(payload);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
