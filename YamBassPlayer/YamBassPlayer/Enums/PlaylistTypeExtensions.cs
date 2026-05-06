namespace YamBassPlayer.Enums;

public static class PlaylistTypeExtensions
{
    public static PlaylistCategory GetCategory(this PlaylistType type) => type switch
    {
        PlaylistType.Favorite or PlaylistType.Custom or PlaylistType.PlaylistOfTheDaily
            or PlaylistType.LocalFolder or PlaylistType.LocalFavorite => PlaylistCategory.Data,
        PlaylistType.Top10 or PlaylistType.TopEvenings or PlaylistType.TopByDay
            or PlaylistType.Cached => PlaylistCategory.Computed,
        PlaylistType.Queue or PlaylistType.MyWave
            or PlaylistType.LocalSearch or PlaylistType.YandexSearch => PlaylistCategory.Transient,
        PlaylistType.Artist or PlaylistType.LocalArtist or PlaylistType.LocalAlbum => PlaylistCategory.Entity,
        _ => PlaylistCategory.Data
    };
}
