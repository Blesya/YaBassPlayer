using YamBassPlayer.Api.Models.Common;

namespace YamBassPlayer.Api.Models.Playlist
{
    public class YPlaylistMadeFor
    {
        public class YMadeForCaseForms
        {
            #region Свойства

            public string Accusative { get; set; }
            public string Dative { get; set; }
            public string Genitive { get; set; }
            public string Instrumental { get; set; }
            public string Nominative { get; set; }
            public string Prepositional { get; set; }

            #endregion Свойства
        }

        #region Свойства

        public YMadeForCaseForms CaseForms { get; set; }
        public YOwner UserInfo { get; set; }

        #endregion Свойства
    }
}