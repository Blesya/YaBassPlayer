using System;

namespace YamBassPlayer.Api.Models.Common
{
    public class YSubscriptionService
    {
        public DateTime Expires { get; set; }
        public bool Finished { get; set; }
        public decimal OrderId { get; set; }
        public string ProductId { get; set; }
        public YProduct Product { get; set; }
        public string Vendor { get; set; }
        public string VendorHelpUrl { get; set; }
    }
}
