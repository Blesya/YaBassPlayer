using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Search
{
    public class YSearchResult<T>
    {
        public int Order { get; set; }
        public int PerPage { get; set; }
        public List<T> Results { get; set; }
        public int Total { get; set; }
    }
}