using System.Text;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Сопоставляет трек из ListenBrainz (артист + название) с нашим ключом.
/// Ищет только в кэше треков (SQLite), в сеть не ходит: рекомендация приходит
/// как MBID, а очередь адресуется <see cref="TrackKey"/>.
/// </summary>
public sealed class ListenBrainzTrackResolver(ITrackInfoProvider trackInfoProvider)
{
	private const int CandidateLimit = 25;

	/// <summary>Та часть артиста, которая пишется до «feat.» и подобных приставок.</summary>
	private static readonly string[] ArtistSeparators = [" feat.", " feat ", " ft.", " ft ", " with ", " vs "];

	public async Task<TrackKey?> ResolveAsync(string artist, string title, CancellationToken ct = default)
	{
		string wantedTitle = Normalize(title);
		string searchQuery = SanitizeLikeQuery(title);
		if (wantedTitle.Length == 0 || searchQuery.Length == 0)
			return null;

		string wantedArtist = Normalize(PrimaryArtist(artist));

		var candidates = (await trackInfoProvider.SearchTracks(searchQuery, CandidateLimit)).ToList();
		if (candidates.Count == 0)
			return null;

		// Сначала точное совпадение артиста и названия, затем — совпадение артиста
		// по вхождению: в кэше «Bonobo», в рекомендации «Bonobo feat. Miguel Atwood‐Ferguson».
		foreach (Track candidate in candidates)
		{
			if (Normalize(candidate.Title) == wantedTitle && Normalize(PrimaryArtist(candidate.Artist)) == wantedArtist)
				return candidate.Key;
		}

		foreach (Track candidate in candidates)
		{
			if (Normalize(candidate.Title) != wantedTitle)
				continue;

			string candidateArtist = Normalize(PrimaryArtist(candidate.Artist));
			if (ArtistsMatch(wantedArtist, candidateArtist))
				return candidate.Key;
		}

		return null;
	}

	internal static string PrimaryArtist(string artist)
	{
		string value = artist.Trim();

		foreach (string separator in ArtistSeparators)
		{
			int index = value.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
			if (index > 0)
				value = value[..index];
		}

		return value.Trim();
	}

	/// <summary>Нижний регистр, только буквы и цифры, одиночные пробелы между словами.</summary>
	internal static string Normalize(string value)
	{
		var builder = new StringBuilder(value.Length);
		bool pendingSpace = false;

		foreach (char ch in value)
		{
			if (char.IsLetterOrDigit(ch))
			{
				if (pendingSpace && builder.Length > 0)
					builder.Append(' ');

				builder.Append(char.ToLowerInvariant(ch));
				pendingSpace = false;
			}
			else
			{
				pendingSpace = true;
			}
		}

		return builder.ToString();
	}

	/// <summary>
	/// Убирает подстановочные знаки SQL LIKE: иначе «100 %» превратился бы в
	/// поиск по всей таблице.
	/// </summary>
	private static string SanitizeLikeQuery(string value)
		=> value.Replace("%", " ", StringComparison.Ordinal)
			.Replace("_", " ", StringComparison.Ordinal)
			.Trim();

	private static bool ArtistsMatch(string left, string right)
	{
		if (left.Length == 0 || right.Length == 0)
			return false;

		return left == right
			|| left.Contains(right, StringComparison.Ordinal)
			|| right.Contains(left, StringComparison.Ordinal);
	}
}
