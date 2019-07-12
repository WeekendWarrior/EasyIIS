using Newtonsoft.Json;
using System.Diagnostics;

namespace EasyIIS.Models
{
    public class SiteConfiguration
    {
        [JsonProperty("sites")]
        public Site[] Sites { get; set; }
    }

    [DebuggerDisplay("SiteName = {SiteName}")]
    public class Site
    {
        [JsonProperty("name")]
        public string SiteName { get; set; }

        [JsonProperty("appPools")]
        public string[] AppPools { get; set; }

        [JsonProperty("websites")]
        public string[] Websites { get; set; }

        [JsonProperty("services")]
        public string[] Services { get; set; }
    }

}
