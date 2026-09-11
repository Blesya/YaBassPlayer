namespace YamBassPlayer.Extensions;

/// <summary>
/// Normalizes a raw cover URL from the Yandex API into a complete absolute <c>https://</c> URL.
/// </summary>
public static class CoverUrl
{
	public static string Normalize(string rawUrl)
	{
		string normalized = rawUrl.Replace("%%", "400x400");

		if (normalized.StartsWith("//"))
			return $"https:{normalized}";

		if (!normalized.StartsWith("http://") && !normalized.StartsWith("https://"))
			return $"https://{normalized.TrimStart('/')}";

		return normalized;
	}
}
