using System;
using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Common
{
    public class YPermissions
    {
        public List<string> Default { get; set; }
        public DateTime Until { get; set; }
        public List<string> Values { get; set; }
    }
}