using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Library;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Library
{
    [YApiRequest(WebRequestMethods.Http.Get, "users/{uid}/{type}/{section}")]
    public class YGetLibrarySectionBuilder<T>: YRequestBuilder<YResponse<T>, (YLibrarySection section, YLibrarySectionType type)>
    {
        public YGetLibrarySectionBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions((YLibrarySection section, YLibrarySectionType type) tuple)
        {
            return new Dictionary<string, string> {
                { "uid", storage.User.Uid },
                { "type", tuple.type.ToString().ToLower() },
                { "section", tuple.section.ToString().ToLower() },
            };
        }
    }
}