namespace YamBassPlayer.Api.Models.Artist
{
    /// <summary>
    /// Ответ ручки «Об исполнителе»: артист, статистика и текстовое описание.
    /// </summary>
    public class YArtistAbout
    {
        public YArtist Artist { get; set; }
        public string Description { get; set; }
        public string ArtistType { get; set; }
    }
}
