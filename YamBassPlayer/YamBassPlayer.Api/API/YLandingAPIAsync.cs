using System.Threading.Tasks;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Feed;
using YamBassPlayer.Api.Models.Landing;
using YamBassPlayer.Api.Requests.Feed;
using YamBassPlayer.Api.Requests.Landing;

namespace YamBassPlayer.Api.API
{
    /// <summary>
    /// API для взаимодействия с главной страницей
    /// </summary>
    public partial class YLandingAPI: YCommonAPI
    {
        #region Основные функции

        public YLandingAPI(YandexMusicApi yandex) : base(yandex)
        {
        }

        /// <summary>
        /// Получение персональных списков
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <param name="blocks">Типы запрашиваемых блоков</param>
        /// <returns></returns>
        public Task<YResponse<YLanding>> GetAsync(AuthStorage storage, params YLandingBlockType[] blocks)
        {
            if (blocks == null)
                return null;

            return new YGetLandingBuilder(api, storage)
                .Build(blocks)
                .GetResponseAsync();
        }

        /// <summary>
        /// Получение ленты
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <returns></returns>
        public Task<YResponse<YFeed>> GetFeedAsync(AuthStorage storage)
        {
            return new YGetFeedBuilder(api, storage)
                .Build(null)
                .GetResponseAsync();
        }

        /// <summary>
        /// Получение лендинга детского раздела
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <returns></returns>
        public Task<YResponse<YChildrenLanding>> GetChildrenLandingAsync(AuthStorage storage)
        {
            return new YGetChildrenLandingBuilder(api, storage)
                .Build(null)
                .GetResponseAsync();
        }

        #endregion Основные функции
    }
}