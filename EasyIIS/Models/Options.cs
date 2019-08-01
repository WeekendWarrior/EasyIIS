using CommandLine;

namespace EasyIIS.Models
{
    [Verb("status", HelpText ="Displayes the current status of all sites and services.")]
    public class StatusOption
    {
        [Option(HelpText = "true to bring the site / services up, otherwise down is assumed.")]
        public bool Status { get; set; }
    }

    [Verb("up", HelpText = "Starts a site.")]
    public class UpOption
    {
        [Option("site",
            Required = true,
            HelpText = "The site name to turn on.")]
        public string SiteName { get; set; }
    }

    [Verb("down", HelpText = "Stops a site.")]
    public class DownOption
    {
        [Option("site",
            Required = true,
            HelpText = "The site name to turn off.")]
        public string SiteName { get; set; }
    }

    [Verb("allup", HelpText = "Starts all the sites.")]
    public class AllUpOption
    {
    }

    [Verb("alldown", HelpText = "Stops all the sites.")]
    public class AllDownOption
    {
    }
}
