using EasyIIS.Models;
using log4net;
using Microsoft.Web.Administration;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;

namespace EasyIIS
{
    /// <summary>
    /// Simple command line tool to automate turning on IIS appPools and websites, and any related windows services.
    /// Usage:
    /// EasyIIS.exe (default / no command line parameters) = all on.
    /// EasyIIS.exe all up = (turns all appPool, website and all services off)
    /// EasyIIS.exe all down = (turns all appPool, website and services off)
    /// EasyIIS.exe mysite up (turns specific site appPool, website and services on)
    /// EasyIIS.exe mysite down (turns specific site appPool, website and services off)
    /// </summary>
    public static class Program
    {
        private static readonly ILog _log;

        private static ServerManager _serverManager;

        /// <summary>
        /// Static constructor for <see cref="Program"/>.
        /// </summary>
        static Program()
        {
            _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            _serverManager = new ServerManager();
        }

        /// <summary>
        /// Entry point of the console application.
        /// </summary>
        public static void Main(string[] args)
        {
            // Define global exception handler.
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            // Process command Line args.
            bool all = false;
            string siteName = null;
            bool up = true;

            LoadCommandLineArguments(args, out all, out siteName, out up);

            _log.InfoFormat("EasyIIS started. all: {0}; siteName: {1}; up: {2}", all, siteName, up);

            // Load site config.
            SiteConfiguration siteConfig = LoadSiteConfiguration();

            // Load the site config.
            Models.Site commandLineSite = !all && !string.IsNullOrWhiteSpace(siteName)
                ? siteConfig.Sites.FirstOrDefault(s => s.SiteName.Equals(siteName))
                : null;

            // Determine if we are processing the single site from command line, 
            // or if none was provided, process all sites.
            Models.Site[] sites = commandLineSite != null
                ? new[] { commandLineSite }.ToArray()
                : siteConfig.Sites;
            
            foreach (Models.Site site in sites)
            {
                ProcessAppPools(site, up);

                ProcessWebsites(site, up);

                ProcessServices(site, up);
            }

            // TODO: warm urls? and/or launch url in browser?
        }

        #region infrastructure

        /// <summary>
        /// Processes IIS app pools.
        /// </summary>
        private static void ProcessAppPools(Models.Site site, bool up)
        {
            foreach (string appPoolName in site.AppPools)
            {
                ApplicationPool appPool = _serverManager.ApplicationPools[appPoolName];

                // Start the app pool.
                if (up && !new[] { ObjectState.Started, ObjectState.Starting }.Contains(appPool.State))
                {
                    _log.InfoFormat("Starting AppPool '{0}'", appPool.Name);
                    appPool.Start();
                }

                // Stop the app pool.
                if (!up && !new[] { ObjectState.Stopping, ObjectState.Stopped }.Contains(appPool.State))
                {
                    _log.InfoFormat("Stopping AppPool '{0}'", appPool.Name);
                    appPool.Stop();
                }
            }
        }

        /// <summary>
        /// Process IIS websites.
        /// </summary>
        private static void ProcessWebsites(Models.Site site, bool up)
        {
            foreach (string websiteName in site.Websites)
            {
                var iisWebsite = _serverManager.Sites[websiteName];

                // Start the app pool.
                if (up && !new[] { ObjectState.Started, ObjectState.Starting }.Contains(iisWebsite.State))
                {
                    _log.InfoFormat("Starting site '{0}'", iisWebsite.Name);
                    iisWebsite.Start();
                }

                // Stop the app pool.
                if (!up && !new[] { ObjectState.Stopping, ObjectState.Stopped }.Contains(iisWebsite.State))
                {
                    _log.InfoFormat("Stopping site '{0}'", iisWebsite.Name);
                    iisWebsite.Stop();
                }
            }
        }

        /// <summary>
        /// Processes services.
        /// </summary>
        private static void ProcessServices(Models.Site site, bool bringSiteUp)
        {
            ServiceController[] services = ServiceController.GetServices();
            foreach (string serviceName in site.Services)
            {
                ServiceController service = services.FirstOrDefault(s => s.ServiceName.Equals(serviceName, StringComparison.InvariantCultureIgnoreCase));

                // Start the service.
                if (bringSiteUp && service.Status != ServiceControllerStatus.Running)
                {
                    _log.InfoFormat("Starting Service '{0}'", service.ServiceName);
                    service.Start();
                }

                // Stop the service.
                if (!bringSiteUp && service.Status != ServiceControllerStatus.Stopped)
                {
                    _log.InfoFormat("Stopping Service '{0}'", service.ServiceName);
                    service.Stop();
                }
            }
        }

        /// <summary>
        /// Loads the site configuration from json file.
        /// </summary>
        private static SiteConfiguration LoadSiteConfiguration(string configFileName = "config.json")
        {
            string workingDir  = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string configFilePath = Path.Combine(workingDir, configFileName);

            string rawJson = File.ReadAllText(configFilePath);

            return JsonConvert.DeserializeObject<SiteConfiguration>(rawJson);
        }

        /// <summary>
        /// Loads the command line arguments.
        /// </summary>
        private static void LoadCommandLineArguments(
            string[] args,
            out bool all,
            out string siteName,
            out bool up
        )
        {
            // Default (no arguments, or "allup".
            if (args.Length == 0 || args[0].Equals("allup", StringComparison.CurrentCultureIgnoreCase))
            {
                all = true;
                siteName = null;
                up = true;

                return;
            }

            // If command line argument is "alldown".
            if (args[0].Equals("alldown", StringComparison.InvariantCultureIgnoreCase))
            {
                all = true;
                siteName = null;
                up = false;

                return;
            }

            // Disable all functionality.
            all = false;

            // Site name will have a value.
            siteName = args[0];

            if (args.Length > 1)
            {
                up = args[1].Equals("up", StringComparison.InvariantCultureIgnoreCase);
            }
            else
            {
                // default to up.
                up = true;
            }
        }

        /// <summary>
        /// Exception handler that logs uncaught application exceptions in log4net.
        /// </summary>
        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            _log.Fatal("Uncaught Exception", (Exception)e.ExceptionObject);
        }

        #endregion
    }
}
