namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Centralizes cover URL/path resolution logic previously duplicated across
/// TrackInfoProvider and LocalLibraryService.
/// </summary>
public static class CoverMetadataResolver
{
	public static string? ResolveRemoteCoverUrl(string? remoteCoverUrl, string? coverUrl)
	{
		if (!string.IsNullOrWhiteSpace(remoteCoverUrl))
			return remoteCoverUrl;

		return IsRootedPath(coverUrl) ? null : coverUrl;
	}

	public static string? ResolveLocalCoverPath(string? localCoverPath, string? coverUrl)
	{
		if (!string.IsNullOrWhiteSpace(localCoverPath))
			return localCoverPath;

		return IsRootedPath(coverUrl) ? coverUrl : null;
	}

	public static string? ResolveLegacyCoverUrl(string? coverUrl, string? remoteCoverUrl, string? localCoverPath)
	{
		if (!string.IsNullOrWhiteSpace(coverUrl))
			return coverUrl;

		return ResolveLocalCoverPath(localCoverPath, coverUrl)
			?? ResolveRemoteCoverUrl(remoteCoverUrl, coverUrl);
	}

	private static bool IsRootedPath(string? value)
		=> !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value);
}
