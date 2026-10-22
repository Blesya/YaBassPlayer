using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Pins;

namespace YamBassPlayer.Api.API
{
    /// <summary>
    /// API для взаимодействия с прикреплёнными объектами
    /// </summary>
    public partial class YPinsAPI
    {
        #region Основные функции

        /// <summary>
        /// Получение списка прикреплённых объектов
        /// </summary>
        /// <param name="storage">Хранилище</param>
        /// <returns></returns>
        public YResponse<YPins> Get(AuthStorage storage)
        {
            return GetAsync(storage).GetAwaiter().GetResult();
        }

        #endregion Основные функции
    }
}