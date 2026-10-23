using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Radio;
using YamBassPlayer.Api.Models.Track;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Radio
{
    [YApiRequest(WebRequestMethods.Http.Post, "rotor/station/{type}:{tag}/feedback")]
    public class YSetStationFeedbackBuilder : YRequestBuilder<YResponse<string>, (YStationFeedbackType type, YStation station, YTrack track, string batchId, double totalPlayedSeconds)>
    {
        public YSetStationFeedbackBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions((YStationFeedbackType type, YStation station, YTrack track, string batchId, double totalPlayedSeconds) tuple)
        {
            return new Dictionary<string, string> {
                { "type", tuple.station.Station.Id.Type },
                { "tag", tuple.station.Station.Id.Tag }
            };
        }

        protected override HttpContent GetContent((YStationFeedbackType type, YStation station, YTrack track, string batchId, double totalPlayedSeconds) tuple)
        {
            // Яндекс ждёт время в ISO 8601 (пример из спецификации: "2023-02-26T09:41:23"),
            // а не unix-секунды числом.
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

            JsonSerializerOptions settings = new() {
                Converters = {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                },
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
            };

            YStationFeedback feedBack = new()
            {
                Type = tuple.type,
                Timestamp = timestamp
            };

            if (!string.IsNullOrWhiteSpace(tuple.station.Station.IdForFrom))
                feedBack.From = tuple.station.Station.IdForFrom;

            // radioStarted относится к запуску станции, трек в нём не указывается.
            if (tuple.type != YStationFeedbackType.RadioStarted && tuple.track != null)
                feedBack.TrackId = tuple.track.Id;

            if (tuple.totalPlayedSeconds > 0)
                feedBack.TotalPlayedSeconds = tuple.totalPlayedSeconds;

            return JsonContent.Create(feedBack, new MediaTypeHeaderValue("application/json"), settings);
        }

        protected override NameValueCollection GetQueryParams((YStationFeedbackType type, YStation station, YTrack track, string batchId, double totalPlayedSeconds) tuple)
        {
            NameValueCollection query = new();

            // batch-id нужен событиям трека и должен отсутствовать для radioStarted.
            if (tuple.type != YStationFeedbackType.RadioStarted && !string.IsNullOrWhiteSpace(tuple.batchId))
                query.Add("batch-id", tuple.batchId);

            return query;
        }
    }
}