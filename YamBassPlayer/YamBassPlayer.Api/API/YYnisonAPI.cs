using System;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Common.Ynison;

namespace YamBassPlayer.Api.API
{
    /// <summary>
    /// API Ynison
    /// </summary>
    public partial class YYnisonAPI : YCommonAPI
    {
        #region Основные функции

        public YYnisonAPI(YandexMusicApi yandex) : base(yandex)
        {
        }

        public YnisonPlayer GetPlayer(AuthStorage storage)
        {
            if (string.IsNullOrEmpty(storage.Token))
                throw new Exception("Токен пользователя не задан.");

            return new(api, storage);
        }

        #endregion Основные функции
    }
}
