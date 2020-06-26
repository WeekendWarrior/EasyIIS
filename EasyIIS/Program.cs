using CommandLine;
using EasyIIS.Models;
using log4net;
using Microsoft.Web.Administration;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace EasyIIS
{
    /*
     * Command line examples:  
     * EasyIIS.exe <no arguments> = shows help
     * EasyIIS.exe status
     * EasyIIS.exe up --site="mysite"
     * EasyIIS.exe down --site="mysite"
     * EasyIIS.exe allup
     * EasyIIS.exe alldown
     */

    /// <summary>
    /// Simple command line tool to automate turning on IIS appPools and websites, and any related windows services.
    /// </summary>
    public static class Program
    {
        private static readonly ILog Log;

        private static readonly ServerManager ServerManager;

        private static readonly ServiceController[] Services;

        /// <summary>
        /// Static constructor for <see cref="Program"/>.
        /// </summary>
        static Program()
        {
            Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            ServerManager = new ServerManager();

            Services = ServiceController.GetServices();
        }
       
        /// <summary>
        /// Entry point of the console application.
        /// </summary>
        public static void Main(string[] args)
        {
            // Define global exception handler.
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;
            
            // Load the configuration from JSON file.
            var siteConfig = LoadSiteConfiguration();

            // Load the command line arguments and process accordingly.
            // https://github.com/commandlineparser/commandline
            Parser.Default.ParseArguments<StatusOption, UpOption, DownOption, AllUpOption, AllDownOption>(args)
                .MapResult(
                    (StatusOption opts) => RenderStatus(siteConfig),
                    (UpOption opts) => ProcessSite(siteConfig, opts.SiteName, true),
                    (DownOption opts) => ProcessSite(siteConfig, opts.SiteName, false),
                    (AllUpOption opts) => ProcessAllSites(siteConfig, true),
                    (AllDownOption opts) => ProcessAllSites(siteConfig, false),
                    errs => 1
                );
        }

        #region infrastructure

        /// <summary>
        /// Renders status of all configured sites.
        /// </summary>
        private static int RenderStatus(SiteConfiguration siteConfig)
        {
            foreach (var site in siteConfig.Sites)
            {
                // App Pools
                foreach (var appPoolName in site.AppPools)
                {
                    RenderAppPoolStatus(appPoolName);
                }

                // Websites
                foreach (var websiteName in site.Websites)
                {
                    RenderWebsiteStatus(websiteName);
                }

                // Services
                foreach (var serviceName in site.Services)
                {
                    RenderServiceStatus(serviceName);
                }
            }

            return 0;
        }

        /// <summary>
        /// Processes a site.
        /// </summary>
        private static int ProcessSite(SiteConfiguration siteConfig, string siteName, bool up)
        {
            // Load the site config.
            var site = siteConfig.Sites.FirstOrDefault(s => s.SiteName.Equals(siteName));

            if (site == null)
            {
                Log.WarnFormat("Could not find configured siteName '{0}'.", siteName);

                return 1;
            }

            ProcessAppPools(site, up);

            ProcessWebsites(site, up);

            ProcessServices(site, up);

            if (up)
            {
                ProcessWarm(site);
            }

            return 0;
        }

        /// <summary>
        /// Processes all sites.
        /// </summary>
        private static int ProcessAllSites(SiteConfiguration siteConfig, bool up)
        {
            var firstRun = true;

            foreach (var site in siteConfig.Sites)
            {
                // Render a new line into the log to visually break up the logs between each site.
                if (!firstRun) Log.Info(Environment.NewLine);

                ProcessAppPools(site, up);

                ProcessWebsites(site, up);

                ProcessServices(site, up);

                if (up)
                {
                    ProcessWarm(site);
                }

                firstRun = false;
            }

            return 0;
        }

        /// <summary>
        /// Processes IIS app pools.
        /// </summary>
        private static void ProcessAppPools(Models.Site site, bool up)
        {
            foreach (var appPoolName in site.AppPools)
            {
                var appPool = ServerManager.ApplicationPools[appPoolName];                

                if (appPool == null)
                {
                    continue;
                }

                // Start the app pool.
                if (up)
                {
                    if (!new[] { ObjectState.Started, ObjectState.Starting }.Contains(appPool.State))
                    {
                        try
                        {
                            appPool.Start();
                            Log.InfoFormat("Starting AppPool '{0}'.", appPool.Name);
                        }
                        catch (COMException ex)
                        {
                            Log.WarnFormat("Cannot start AppPool '{0}': Reason: '{1}'.\r\n\t--> Was this AppPool recently stopped?", appPool.Name, ex.Message);
                        }
                    }
                    else
                    {
                        Log.InfoFormat("AppPool '{0}' is already {1}.", appPool.Name, appPool.State);
                    }
                }
                else
                {
                    // Stop the app pool.
                    if (!new[] { ObjectState.Stopping, ObjectState.Stopped }.Contains(appPool.State))
                    {
                        try
                        {
                            appPool.Stop();
                            Log.InfoFormat("Stopping AppPool '{0}'.", appPool.Name);
                        }
                        catch (COMException ex)
                        {
                            Log.WarnFormat("Cannot stop AppPool '{0}': Reason: '{1}'.\r\n\t--> Was this AppPool recently started?", appPool.Name, ex.Message);
                        }
                    }
                    else
                    {
                        Log.InfoFormat("AppPool '{0}' is already {1}.", appPool.Name, appPool.State);
                    }
                }
            }
        }

        /// <summary>
        /// Process IIS websites.
        /// </summary>
        private static void ProcessWebsites(Models.Site site, bool up)
        {
            foreach (var websiteName in site.Websites)
            {
                var iisWebsite = ServerManager.Sites[websiteName];

                if (iisWebsite == null)
                {
                    continue;
                }

                // Start the website.
                if (up)
                {
                    if (!new[] { ObjectState.Started, ObjectState.Starting }.Contains(iisWebsite.State))
                    {
                        Log.InfoFormat("Starting Website '{0}'", iisWebsite.Name);
                        iisWebsite.Start();
                    }
                    else
                    {
                        Log.InfoFormat("Website '{0}' is already {1}.", iisWebsite.Name, iisWebsite.State);
                    }
                }
                else
                {
                    // Stop the website.
                    if (!new[] { ObjectState.Stopping, ObjectState.Stopped }.Contains(iisWebsite.State))
                    {
                        Log.InfoFormat("Stopping Website '{0}'", iisWebsite.Name);
                        iisWebsite.Stop();
                    }
                    else
                    {
                        Log.InfoFormat("Website '{0}' is already {1}.", iisWebsite.Name, iisWebsite.State);
                    }
                }
            }
        }

        /// <summary>
        /// Processes services.
        /// </summary>
        private static void ProcessServices(Models.Site site, bool up)
        {
            foreach (var serviceName in site.Services)
            {
                var service = Services.FirstOrDefault(s => s.ServiceName.Equals(serviceName, StringComparison.InvariantCultureIgnoreCase));

                if (service == null)
                {
                    continue;
                }

                // Start the service.
                if (up)
                {
                    if (!new[] {ServiceControllerStatus.Running, ServiceControllerStatus.StartPending }.Contains(service.Status))
                    {
                        try
                        {
                            service.Start();
                            Log.InfoFormat("Starting Service '{0}'", service.ServiceName);
                        }
                        catch (InvalidOperationException ex)
                        {
                            Log.WarnFormat("Cannot start Service '{0}': Reason: '{1}'.\r\n\t--> Was this service started already in this run?", service.ServiceName, ex.Message);
                        }
                    }
                    else
                    {
                        Log.InfoFormat("Service '{0}' is already {1}.", service.ServiceName, service.Status);
                    }
                }
                else
                {
                    // Stop the service.
                    if (!new[]{ ServiceControllerStatus.Stopped, ServiceControllerStatus.StopPending }.Contains(service.Status))
                    {
                        // System.InvalidOperationException: Cannot stop MongoDB 3.4 service on computer '.'. ---> System.ComponentModel.Win32Exception: The service has not been started
                        try
                        {
                            service.Stop();
                            Log.InfoFormat("Stopping Service '{0}'", service.ServiceName);
                        }
                        catch (InvalidOperationException ex)
                        {
                            Log.WarnFormat("Cannot stop Service '{0}': Reason: '{1}'.\r\n\t--> Was this service stopped already in this run?", service.ServiceName, ex.Message);
                        }
                    }
                    else
                    {
                        Log.InfoFormat("Service '{0}' is already {1}.", service.ServiceName, service.Status);
                    }
                }
            }
        }

        /// <summary>
        /// Warms the websites.
        /// </summary>
        private static void ProcessWarm(Models.Site site)
        {
            if (site.Warm == null || site.Warm.Length == 0) return;

            foreach (var url in site.Warm)
            {
                WarmUrl(url);

                Log.InfoFormat("Warming url '{0}'", url);
            }
        }

        /// <summary>
        /// Fire and forget "Warms" a url by sending an http request to it.
        /// </summary>
        private static void WarmUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            Task.Factory.StartNew(async () =>
            {
                using (var client = new HttpClient())
                {
                    await client.PostAsync(url, new StringContent(""));
                }
            });
        }

        /// <summary>
        /// Loads the site configuration from json file.
        /// </summary>
        private static SiteConfiguration LoadSiteConfiguration()
        {
            var workingDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (workingDir == null)
            {
                throw new ApplicationException("Could not determine working directory of EasyIIS executable.");
            }

            var configFileName = ConfigurationManager.AppSettings["configFileName"];

            var configFilePath = Path.Combine(workingDir, configFileName);

            var rawJson = File.ReadAllText(configFilePath);

            return JsonConvert.DeserializeObject<SiteConfiguration>(rawJson);
        }

        /// <summary>
        /// Renders the status of an app pool.
        /// </summary>
        private static void RenderAppPoolStatus(string appPoolName)
        {
            var appPool = ServerManager.ApplicationPools[appPoolName];
            if (appPool != null)
            {
                Log.InfoFormat("AppPool '{0}': {1}", appPool.Name, appPool.State);
            }
            else
            {
                Log.WarnFormat("AppPool '{0}': NOTFOUND", appPoolName);
            }
        }

        /// <summary>
        /// Renders the status of a website.
        /// </summary>
        private static void RenderWebsiteStatus(string websiteName)
        {
            var iisWebsite = ServerManager.Sites[websiteName];

            if (iisWebsite != null)
            {
                Log.InfoFormat("Website '{0}': {1}", iisWebsite.Name, iisWebsite.State);
            }
            else
            {
                Log.WarnFormat("Website '{0}': NOTFOUND", websiteName);
            }
        }


        /// <summary>
        /// Renders the status of a service.
        /// </summary>
        private static void RenderServiceStatus(string serviceName)
        {
            var service = Services.FirstOrDefault(s => s.ServiceName.Equals(serviceName, StringComparison.InvariantCultureIgnoreCase));

            if (service != null)
            {
                Log.InfoFormat("Service '{0}': {1}", service.ServiceName, service.Status);
            }
            else
            {
                Log.WarnFormat("Service '{0}': NOTFOUND", serviceName);
            }
        }

        /// <summary>
        /// Exception handler that logs uncaught application exceptions in log4net.
        /// </summary>
        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal("Uncaught Exception", (Exception)e.ExceptionObject);
        }

        #endregion
    }
}
