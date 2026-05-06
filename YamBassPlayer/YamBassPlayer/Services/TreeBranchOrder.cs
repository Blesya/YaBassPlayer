namespace YamBassPlayer.Services;

/// <summary>
/// Centralized ordering for <see cref="ITreeBranchBuilder"/> implementations.
/// Replaces the previously scattered magic values 0/10/20.
/// </summary>
public static class TreeBranchOrder
{
	/// <summary>Sources node (Yandex + local library) — rendered first.</summary>
	public const int Sources = 0;

	/// <summary>Top-by-day-of-week node — rendered after the sources node.</summary>
	public const int TopByDay = 10;

	/// <summary>Global artists node — rendered last.</summary>
	public const int GlobalArtists = 20;
}
