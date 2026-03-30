namespace YamBassPlayer.Enums;

public enum PlaylistType
{
	Favorite,
	PlaylistOfTheDaily,
	Custom,
	Cached,
	Top10,
	TopEvenings,
	LocalFavorite,
	LocalSearch,
	TopByDay,
	YandexSearch,
	Artist,
	Queue,
	OnSameWave,
	MyWave,
	/// <summary>Represents a playlist sourced from an individual local music folder.</summary>
	LocalFolder,
	/// <summary>Represents a playlist for a single local-library artist (file-based, not Yandex).</summary>
	LocalArtist,
	/// <summary>Represents a playlist for a single local-library album under a specific artist.</summary>
	LocalAlbum
}