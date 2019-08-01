using EasyIIS.Models;
using log4net;
using Microsoft.Web.Administration;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using CommandLine;
using System.Configuration;

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

        private static ServiceController[] _services;

        /// <summary>
        /// Static constructor for <see cref="Program"/>.
        /// </summary>
        static Program()
        {
            _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            _serverManager = new ServerManager();

            _services = ServiceController.GetServices();
        }
       
        /// <summary>
        /// Entry point of the console application.
        /// </summary>
        public static void Main(string[] args)
        {
            // Define global exception handler.
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;
            
            // Load the configuration from JSON file.
            SiteConfiguration siteConfig = LoadSiteConfiguration();

            // Load the command line arguments and process accordingly.

            /*
             * Examples:  
             * EasyIIS.exe status
             * EasyIIS.exe up --site="mysite"
             * EasyIIS.exe down --site="mysite"
             * EasyIIS.exe allup
             * EasyIIS.exe alldown
             */

            // https://github.com/commandlineparser/commandline
            Parser.Default.ParseArguments<StatusOption, UpOption, DownOption, AllUpOption, AllDownOption>(args)
                .MapResult(
                    (StatusOption opts) => RenderStatus(siteConfig),
                    (UpOption opts) => ProcessSite(siteConfig, opts.SiteName, true),
                    (DownOption opts) => ProcessSite(siteConfig, opts.SiteName, false),
                    (AllUpOption opts) => PerformAll(siteConfig, true),
                    (AllDownOption opts) => PerformAll(siteConfig, false),
                    errs => 1
                );
        }

        #region infrastructure

        /// <summary>
        /// Renders status of all configured sites.
        /// </summary>
        private static int RenderStatus(SiteConfiguration siteConfig)
        {
            foreach (Models.Site site in siteConfig.Sites)
            {
                // App Pools
                foreach (string appPoolName in site.AppPools)
                {
                    RenderAppPoolStatus(appPoolName);
                }

                // Websites
                foreach (string websiteName in site.Websites)
                {
                    RenderWebsiteStatus(websiteName);
                }

                // Services
                foreach (string serviceName in site.Services)
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
            Models.Site site = siteConfig.Sites.FirstOrDefault(s => s.SiteName.Equals(siteName));

            if (site == null)
            {
                _log.WarnFormat("Could not find configured siteName '{0}'.", siteName);

                return 1;
            }

            ProcessAppPools(site, up);

            ProcessWebsites(site, up);

            ProcessServices(site, up);

            return 0;
        }

        /// <summary>
        /// Processes all sites.
        /// </summary>
        private static int PerformAll(SiteConfiguration siteConfig, bool up)
        {
            foreach (Models.Site site in siteConfig.Sites)
            {
                ProcessAppPools(site, up);

                ProcessWebsites(site, up);

                ProcessServices(site, up);
            }

            return 0;
        }

        /// <summary>
        /// Processes IIS app pools.
        /// </summary>
        private static void ProcessAppPools(Models.Site site, bool up)
        {
            foreach (string appPoolName in site.AppPools)
            {
                ApplicationPool appPool = _serverManager.ApplicationPools[appPoolName];                

                if (appPool == null)
                {
                    continue;
                }

                // Start the app pool.
                if (up)
                {
                    if (!new[] { ObjectState.Started, ObjectState.Starting }.Contains(appPool.State))
                    {
                        _log.InfoFormat("Starting AppPool '{0}'.", appPool.Name);
                        appPool.Start();
                    }
                    else
                    {
                        _log.InfoFormat("AppPool '{0}' is already {1}.", appPool.Name, appPool.State);
                    }
                }
                else
                {
                    // Stop the app pool.
                    if (!new[] { ObjectState.Stopping, ObjectState.Stopped }.Contains(appPool.State))
                    {
                        _log.InfoFormat("Stopping AppPool '{0}'.", appPool.Name);
                        appPool.Stop();
                    }
                    else
                    {
                        _log.InfoFormat("AppPool '{0}' is already {1}.", appPool.Name, appPool.State);
                    }
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

                if (iisWebsite == null)
                {
                    continue;
                }

                // Start the website.
                if (up)
                {
                    if (!new[] { ObjectState.Started, ObjectState.Starting }.Contains(iisWebsite.State))
                    {
                        _log.InfoFormat("Starting Website '{0}'", iisWebsite.Name);
                        iisWebsite.Start();
                    }
                    else
                    {
                        _log.InfoFormat("Website '{0}' is already {1}.", iisWebsite.Name, iisWebsite.State);
                    }
                }
                else
                {
                    // Stop the website.
                    if (!new[] { ObjectState.Stopping, ObjectState.Stopped }.Contains(iisWebsite.State))
                    {
                        _log.InfoFormat("Stopping Website '{0}'", iisWebsite.Name);
                        iisWebsite.Stop();
                    }
                    else
                    {
                        _log.InfoFormat("Website '{0}' is already {1}.", iisWebsite.Name, iisWebsite.State);
                    }
                }
            }
        }

        /// <summary>
        /// Processes services.
        /// </summary>
        private static void ProcessServices(Models.Site site, bool up)
        {
            foreach (string serviceName in site.Services)
            {
                ServiceController service = _services.FirstOrDefault(s => s.ServiceName.Equals(serviceName, StringComparison.InvariantCultureIgnoreCase));

                if (service == null)
                {
                    continue;
                }

                // Start the service.
                if (up)
                {
                    if (up && service.Status != ServiceControllerStatus.Running)
                    {
                        _log.InfoFormat("Starting Service '{0}'", service.ServiceName);
                        service.Start();
                    }
                    else
                    {
                        _log.InfoFormat("Service '{0}' is already {1}.", service.ServiceName, service.Status);
                    }
                }
                else
                {
                    // Stop the service.
                    if (!up && service.Status != ServiceControllerStatus.Stopped)
                    {
                        _log.InfoFormat("Stopping Service '{0}'", service.ServiceName);
                        service.Stop();
                    }
                    else
                    {
                        _log.InfoFormat("Service '{0}' is already {1}.", service.ServiceName, service.Status);
                    }
                }
            }
        }

        /// <summary>
        /// Loads the site configuration from json file.
        /// </summary>
        private static SiteConfiguration LoadSiteConfiguration()
        {
            string workingDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string configFileName = ConfigurationManager.AppSettings["configFileName"];

            string configFilePath = Path.Combine(workingDir, configFileName);

            string rawJson = File.ReadAllText(configFilePath);

            return JsonConvert.DeserializeObject<SiteConfiguration>(rawJson);
        }

        /// <summary>
        /// Renders the status of an app pool.
        /// </summary>
        private static void RenderAppPoolStatus(string appPoolName)
        {
            ApplicationPool appPool = _serverManager.ApplicationPools[appPoolName];
            if (appPool != null)
            {
                _log.InfoFormat("AppPool '{0}': {1}", appPool.Name, appPool.State);
            }
            else
            {
                _log.WarnFormat("AppPool '{0}': NOTFOUND", appPoolName);
            }
        }

        /// <summary>
        /// Renders the status of a website.
        /// </summary>
        private static void RenderWebsiteStatus(string websiteName)
        {
            var iisWebsite = _serverManager.Sites[websiteName];

            if (iisWebsite != null)
            {
                _log.InfoFormat("Website '{0}': {1}", iisWebsite.Name, iisWebsite.State);
            }
            else
            {
                _log.WarnFormat("Website '{0}': NOTFOUND", websiteName);
            }
        }


        /// <summary>
        /// Renders the status of a service.
        /// </summary>
        private static void RenderServiceStatus(string serviceName)
        {
            ServiceController service = _services.FirstOrDefault(s => s.ServiceName.Equals(serviceName, StringComparison.InvariantCultureIgnoreCase));

            if (service != null)
            {
                _log.InfoFormat("Service '{0}': {1}", service.ServiceName, service.Status);
            }
            else
            {
                _log.WarnFormat("Service '{0}': NOTFOUND", serviceName);
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
