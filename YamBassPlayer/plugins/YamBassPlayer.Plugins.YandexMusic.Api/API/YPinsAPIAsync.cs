using System.Threading.Tasks;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Pins;
using YamBassPlayer.Api.Requests.Pins;

namespace YamBassPlayer.Api.API
{
    /// <summary>
    /// API для взаимодействия с прикреплёнными объектами
    /// </summary>
    public partial class YPinsAPI : YCommonAPI
    {
        #region Основные функции

        public YPinsAPI(YandexMusicApi yandex): base(yandex)
        {
        }

        /// <summary>
        /// Получение списка прикреплённых объектов
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <returns></returns>
        public Task<YResponse<YPins>> GetAsync(AuthStorage storage)
        {
            return new YGetPinsBuilder(api, storage)
                .Build(null)
                .GetResponseAsync();
        }

        #endregion Основные функции
    }
}