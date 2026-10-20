using Newtonsoft.Json;

using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Search.Album;
using YamBassPlayer.Api.Models.Search.Artist;
using YamBassPlayer.Api.Models.Search.Playlist;
using YamBassPlayer.Api.Models.Search.Track;
using YamBassPlayer.Api.Models.Search.User;
using YamBassPlayer.Api.Models.Search.Video;

namespace YamBassPlayer.Api.Models.Search
{
    public class YSearch
    {
        public YSearchResult<YSearchAlbumModel> Albums { get; set; }
        public YSearchResult<YSearchArtistModel> Artists { get; set; }

        public YSearchBest Best { get; set; }

        public bool MisspellCorrected { get; set; }
        public string MisspellOriginal { get; set; }
        public string MisspellResult { get; set; }
        public bool NoCorrect { get; set; }
        public int Page { get; set; }
        public int PerPage { get; set; }
        public YSearchResult<YSearchPlaylistModel> Playlists { get; set; }
        [JsonProperty("podcast_episodes")]
        public YSearchResult<YSearchTrackModel> PodcastEpisode { get; set; }
        public string SearchRequestId { get; set; }
        public string Text { get; set; }
        public YSearchResult<YSearchTrackModel> Tracks { get; set; }

        public YSearchType Type { get; set; }
        public YSearchResult<YSearchUserModel> Users { get; set; }
        public YSearchResult<YSearchVideoModel> Videos { get; set; }
    }
}