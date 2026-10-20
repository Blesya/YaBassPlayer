using System.Text.Json;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class GitHubReleaseFeedTests
{
    [Test]
    public void Parse_ReleaseWithAsset_ReturnsUpdateInfo()
    {
        const string json = """
        {
          "tag_name": "v0.0.2",
          "body": "Что нового",
          "assets": [
            { "name": "YamBassPlayer-0.0.2-win-x64.zip", "browser_download_url": "https://example.org/app.zip", "size": 1234 },
            { "name": "YamBassPlayer-0.0.2-win-x64.zip.sha256", "browser_download_url": "https://example.org/app.zip.sha256", "size": 64 }
          ]
        }
        """;

        var info = GitHubReleaseFeed.Parse(ParseJson(json));

        Assert.That(info, Is.Not.Null);
        Assert.That(info!.Version, Is.EqualTo(new Version(0, 0, 2)));
        Assert.That(info.TagName, Is.EqualTo("v0.0.2"));
        Assert.That(info.AssetName, Is.EqualTo("YamBassPlayer-0.0.2-win-x64.zip"));
        Assert.That(info.DownloadUrl, Is.EqualTo("https://example.org/app.zip"));
        Assert.That(info.SizeBytes, Is.EqualTo(1234));
        Assert.That(info.Notes, Is.EqualTo("Что нового"));
        Assert.That(info.ChecksumUrl, Is.EqualTo("https://example.org/app.zip.sha256"));
    }

    [Test]
    public void Parse_WithoutChecksumAsset_ReturnsNullChecksum()
    {
        const string json = """
        {
          "tag_name": "v1.2.3",
          "assets": [
            { "name": "YamBassPlayer-1.2.3-win-x64.zip", "browser_download_url": "https://example.org/app.zip" }
          ]
        }
        """;

        var info = GitHubReleaseFeed.Parse(ParseJson(json));

        Assert.That(info, Is.Not.Null);
        Assert.That(info!.ChecksumUrl, Is.Null);
        Assert.That(info.SizeBytes, Is.EqualTo(0));
    }

    [Test]
    public void Parse_WithoutMatchingAsset_ReturnsNull()
    {
        const string json = """
        {
          "tag_name": "v0.0.3",
          "assets": [
            { "name": "YamBassPlayer-0.0.3-linux-x64.zip", "browser_download_url": "https://example.org/linux.zip" }
          ]
        }
        """;

        Assert.That(GitHubReleaseFeed.Parse(ParseJson(json)), Is.Null);
    }

    [TestCase("v0.0.2-beta")]
    [TestCase("release-1")]
    [TestCase("v1.2")]
    [TestCase("")]
    public void Parse_NonSemanticTag_ReturnsNull(string tag)
    {
        string json = $$"""
        {
          "tag_name": "{{tag}}",
          "assets": [ { "name": "YamBassPlayer-0.0.2-win-x64.zip", "browser_download_url": "https://example.org/app.zip" } ]
        }
        """;

        Assert.That(GitHubReleaseFeed.Parse(ParseJson(json)), Is.Null);
    }

    [Test]
    public void Parse_WithoutTag_ReturnsNull()
    {
        Assert.That(GitHubReleaseFeed.Parse(ParseJson("""{ "assets": [] }""")), Is.Null);
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
