using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Centralizes cover URL/path resolution logic previously duplicated across
/// TrackInfoProvider and LocalLibraryService.
/// </summary>
public static class CoverMetadataResolver
{
	public static bool IsLocalSourceType(string sourceType)
		=> string.Equals(sourceType, SourceIds.Local, StringComparison.OrdinalIgnoreCase);

	public static string? ResolveRemoteCoverUrl(string sourceType, string? coverUrl, string? remoteCoverUrl)
	{
		if (!string.IsNullOrWhiteSpace(remoteCoverUrl))
			return remoteCoverUrl;

		return IsLocalSourceType(sourceType) ? null : coverUrl;
	}

	public static string? ResolveLocalCoverPath(string sourceType, string? coverUrl, string? localCoverPath)
	{
		if (!string.IsNullOrWhiteSpace(localCoverPath))
			return localCoverPath;

		return IsLocalSourceType(sourceType) ? coverUrl : null;
	}

	public static string? ResolveLegacyCoverUrl(
		string sourceType,
		string? coverUrl,
		string? remoteCoverUrl,
		string? localCoverPath)
	{
		if (!string.IsNullOrWhiteSpace(coverUrl))
			return coverUrl;

		return IsLocalSourceType(sourceType)
			? ResolveLocalCoverPath(sourceType, coverUrl, localCoverPath)
			: ResolveRemoteCoverUrl(sourceType, coverUrl, remoteCoverUrl);
	}
}
