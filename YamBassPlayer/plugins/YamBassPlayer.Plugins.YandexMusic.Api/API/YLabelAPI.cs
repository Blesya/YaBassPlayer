using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Label;

namespace YamBassPlayer.Api.API
{
    public partial class YLabelAPI
    {
        /// <summary>
        /// Постраничное получение альбомов лейбла
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <param name="label">Лейбл</param>
        /// <param name="page">Страница</param>
        public YResponse<YLabelAlbums> GetAlbumsByLabel(AuthStorage storage, YLabel label, int page)
        {
            return GetAlbumsByLabelAsync(storage, label, page).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Постраничное получение артистов лейбла
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <param name="label">Лейбл</param>
        /// <param name="page">Страница</param>
        public YResponse<YLabelArtists> GetArtistsByLabel(AuthStorage storage, YLabel label, int page)
        {
            return GetArtistsByLabelAsync(storage, label, page).GetAwaiter().GetResult();
        }
    }
}