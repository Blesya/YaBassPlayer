using System.Threading.Tasks;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Label;
using YamBassPlayer.Api.Requests.Label;

namespace YamBassPlayer.Api.API
{
    public partial class YLabelAPI : YCommonAPI
    {
        public YLabelAPI(YandexMusicApi yandex) : base(yandex)
        {
        }

        /// <summary>
        /// Постраничное получение альбомов лейбла
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <param name="label">Лейбл</param>
        /// <param name="page">Страница</param>
        public Task<YResponse<YLabelAlbums>> GetAlbumsByLabelAsync(AuthStorage storage, YLabel label, int page)
        {
            return new YGetLabelAlbumsBuilder(api, storage)
                .Build((label, page))
                .GetResponseAsync();
        }

        /// <summary>
        /// Постраничное получение артистов лейбла
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <param name="label">Лейбл</param>
        /// <param name="page">Страница</param>
        public Task<YResponse<YLabelArtists>> GetArtistsByLabelAsync(AuthStorage storage, YLabel label, int page)
        {
            return new YGetLabelArtistsBuilder(api, storage)
                .Build((label, page))
                .GetResponseAsync();
        }
    }
}