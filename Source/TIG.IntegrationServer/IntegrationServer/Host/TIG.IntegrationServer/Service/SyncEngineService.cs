using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Core;
using TIG.IntegrationServer.Common;
using TIG.IntegrationServer.DI.Autofac;
using TIG.IntegrationServer.SyncEngine.Core.Interface;
using Topshelf;
using Topshelf.Logging;

namespace TIG.IntegrationServer.Service
{
    internal partial class SyncEngineService : ServiceControl
    {
        #region Static Fields

        private static LogWriter _hostLogger;

        #endregion


        #region Private Fields

        private readonly ISyncMachine _syncMachine;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public SyncEngineService()
        {
            // Get a Topshelf logger for logging the service startup process
            _hostLogger = HostLogger.Get(typeof(HostFactory));

            // Load and register all necessary assemblies
            PreLoadAssemblies();
            RegisterModules();

            // Get an instance of the sync machine
            _syncMachine = AutofacLocator.Resolve<ISyncMachine>();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Pre-loads all assemblies in the base path and the Facade and Plugin sub-folders.
        /// </summary>
        private static void PreLoadAssemblies()
        {
            // Load assemblies in the base directory
            _hostLogger.Info("Loading built-ins...");
            PreLoadAssemblies(AppDomain.CurrentDomain.BaseDirectory, false, "SQLite.Interop");

            // Load Facades
            _hostLogger.Info("Loading facades...");
            var facadePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Facade");
            PreLoadAssemblies(facadePath);

            // Load Plugins
            _hostLogger.Info("Loading plugins...");
            var pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugin");
            PreLoadAssemblies(pluginsPath);
        }

        /// <summary>
        /// Pre-loads all assemblies in the specified path.
        /// </summary>
        /// <param name="path">The path to load asseblies from.</param>
        /// <param name="includeSubDirectories">Indicates if assemblies should also be loaded from sub directories.</param>
        /// <param name="excludePatterns">Ignore assemblies name patterns.</param>
        private static void PreLoadAssemblies(string path, bool includeSubDirectories = true, params string[] excludePatterns)
        {
            // Abort if the directory doesn't exist
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            // Get a list of the assemblies to load
            var files = new List<FileInfo>(new DirectoryInfo(path).GetFiles("*.dll", includeSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));

            // Remove files that contain any of the exclude patterns
            if (excludePatterns != null && excludePatterns.Length > 0)
            {
                files = files.Where(f => excludePatterns.Any(p => !f.FullName.Contains(p))).ToList();
            }

            // Process all files
            try
            {
                foreach (var file in files)
                {
                    _hostLogger.Info(file.Name);

                    // Load the assembly if it isn't already loaded
                    var assemblyName = AssemblyName.GetAssemblyName(file.FullName);
                    if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => AssemblyName.ReferenceMatchesDefinition(assemblyName, assembly.GetName())))
                    {
                        Assembly.Load(assemblyName);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error loading assemblies.", ex);
            }
        }

        /// <summary>
        /// Register all Autofac modules.
        /// </summary>
        private static void RegisterModules()
        {
            _hostLogger.Info("Registering modules...");

            // Prepare the composition root container
            var builder = new ContainerBuilder();

            // Register core modules first
            var beaconAssembly = AssemblyBeacon.Assembly;
            RegisterModules(builder, new[] { beaconAssembly });

            // Register all remaining modules
            RegisterModules(builder, AppDomain.CurrentDomain.GetAssemblies().Where(a => !ReferenceEquals(a, beaconAssembly)));

            // Build the container
            var container = builder.Build();
            AutofacLocator.Container = container;
        }

        /// <summary>
        /// Register all Autofac modules in the specified assemblies.
        /// </summary>
        /// <param name="builder">The ContainerBuilder to register modules in.</param>
        /// <param name="assemblies">The assemblies to register modules from.</param>
        private static void RegisterModules(ContainerBuilder builder, IEnumerable<Assembly> assemblies)
        {
            // Process all supplied assemblies and register types that implement IModule
            try
            {
                foreach (var assembly in assemblies)
                {
                    foreach (var type in assembly.GetTypes().Where(t => t != typeof(IModule) && t != typeof(Autofac.Module) && typeof(IModule).IsAssignableFrom(t)))
                    {
                        // Create an instance of the module and register it
                        _hostLogger.Info(type.FullName);
                        var module = (IModule)Activator.CreateInstance(type);
                        builder.RegisterModule(module);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error during module initialization.", ex);
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Start integration service.
        /// </summary>
        /// <param name="hostControl">Service host.</param>
        /// <returns>Return indicate service be started or not.</returns>
        public bool Start(HostControl hostControl)
        {
            _syncMachine.Start();

            return true;
        }

        /// <summary>
        /// Stop integration service.
        /// </summary>
        /// <param name="hostControl">Service host.</param>
        /// <returns>Return indicate service be stoped or not.</returns>
        public bool Stop(HostControl hostControl)
        {
            _syncMachine.Stop();
            _syncMachine.Dispose();

            AutofacLocator.Container.Dispose();

            return true;
        }

        /// <summary>
        /// Pause integration service.
        /// </summary>
        /// <param name="hostControl">Service host.</param>
        /// <returns>Return indicate service be paused or not.</returns>
        public bool Pause(HostControl hostControl)
        {
            _syncMachine.Pause();

            return true;
        }

        /// <summary>
        /// Continue integration service.
        /// </summary>
        /// <param name="hostControl">Service host.</param>
        /// <returns>Return indicate service be continued or not.</returns>
        public bool Continue(HostControl hostControl)
        {
            _syncMachine.Continue();

            return true;
        }

        #endregion
    }
}
