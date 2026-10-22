using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Feed;
using YamBassPlayer.Api.Models.Landing;

namespace YamBassPlayer.Api.API
{
    /// <summary>
    /// API для взаимодействия с главной страницей
    /// </summary>
    public partial class YLandingAPI
    {
        #region Основные функции

        /// <summary>
        /// Получение персональных списков
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <param name="blocks">Типы запрашиваемых блоков</param>
        /// <returns></returns>
        public YResponse<YLanding> Get(AuthStorage storage, params YLandingBlockType[] blocks)
        {
            return GetAsync(storage, blocks).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Получение ленты
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <returns></returns>
        public YResponse<YFeed> GetFeed(AuthStorage storage)
        {
            return GetFeedAsync(storage).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Получение лендинга детского раздела
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <returns></returns>
        public YResponse<YChildrenLanding> GetChildrenLanding(AuthStorage storage)
        {
            return GetChildrenLandingAsync(storage).GetAwaiter().GetResult();
        }

        #endregion Основные функции
    }
}