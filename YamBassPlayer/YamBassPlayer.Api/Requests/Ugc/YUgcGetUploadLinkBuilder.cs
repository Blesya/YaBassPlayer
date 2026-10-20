using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Playlist;
using YamBassPlayer.Api.Models.Ugc;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Ugc
{
    [YWebApiRequest(WebRequestMethods.Http.Get, "handlers/ugc-upload.jsx")]
    public class YUgcGetUploadLinkBuilder : YRequestBuilder<YUgcUpload, (YPlaylist playlist, string fileName)>
    {
        private static readonly Lazy<Random> Random = new(() => new Random());

        public YUgcGetUploadLinkBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override NameValueCollection GetQueryParams((YPlaylist playlist, string fileName) tuple)
        {
            return new NameValueCollection {
                { "filename", tuple.fileName },
                { "kind", tuple.playlist.Kind },
                { "visibility", "private" },
                { "external-domain", "music.yandex.ru" },
                { "ncrnd", Random.Value.NextDouble().ToString(CultureInfo.InvariantCulture) }
            };
        }
    }
}