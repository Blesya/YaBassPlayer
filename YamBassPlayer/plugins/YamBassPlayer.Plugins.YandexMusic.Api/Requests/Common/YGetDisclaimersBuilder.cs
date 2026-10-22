using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Common
{
    /// <summary>
    /// Глобальный словарь дисклеймеров. Ответ — голый массив, без обёртки result.
    /// </summary>
    [YApiRequest(WebRequestMethods.Http.Get, "disclaimers")]
    public class YGetDisclaimersBuilder : YRequestBuilder<List<YDisclaimer>, object>
    {
        public YGetDisclaimersBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }
    }
}
