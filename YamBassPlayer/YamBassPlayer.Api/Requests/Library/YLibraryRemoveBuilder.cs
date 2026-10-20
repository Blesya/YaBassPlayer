using System.Collections.Generic;
using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Library;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Library
{
    [YApiRequest(WebRequestMethods.Http.Post, "users/{uid}/{type}/{section}/remove")]
    public class YLibraryRemoveBuilder<T>: YRequestBuilder<YResponse<T>, (string id, YLibrarySection section, YLibrarySectionType type)>
    {
        public YLibraryRemoveBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions((string id, YLibrarySection section, YLibrarySectionType type) tuple)
        {
            return new Dictionary<string, string> {
                { "uid", storage.User.Uid },
                { "type", tuple.type.ToString().ToLower() },
                { "section", tuple.section.ToString().ToLower() },
            };
        }

        protected override HttpContent GetContent((string id, YLibrarySection section, YLibrarySectionType type) tuple)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                { $"{tuple.section.ToString().ToLower().TrimEnd('s')}-ids", tuple.id }
            });
        }
    }
}