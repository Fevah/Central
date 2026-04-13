using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using TIG.TotalLink.Client.Core.StartupWorker.Core;

namespace TIG.TotalLink.Client.Core.StartupWorker
{
    /// <summary>
    /// Pre-loads assemblies, so the ViewLocator and DocumentManager can find views in those assemblies.
    /// </summary>
    public class PreLoadAssembliesStartupWorker : StartupWorkerBase
    {
        #region Private Fields

        private readonly string _path;
        private readonly string _message;
        private List<FileInfo> _files;
        private readonly List<string> _excludePatterns;

        #endregion


        #region Constructors

        public PreLoadAssembliesStartupWorker(string path, string message)
        {
            _path = path;
            _message = message;
        }

        public PreLoadAssembliesStartupWorker(string path, string message, params string[] excludePatterns)
            : this(path, message)
        {
            _excludePatterns = new List<string>(excludePatterns);
        }

        #endregion


        #region Overrides

        public override void Initialize()
        {
            // Abort if the directory doesn't exist
            if (string.IsNullOrWhiteSpace(_path) || !Directory.Exists(_path))
                return;

            // Get a list of the assemblies to load
            _files = new List<FileInfo>(new DirectoryInfo(_path).GetFiles("*.dll", SearchOption.TopDirectoryOnly));

            // Remove files that contain any of the exclude patterns
            if (_excludePatterns != null && _excludePatterns.Count > 0)
            {
                _files = _files.Where(f => _excludePatterns.Any(p => !f.FullName.Contains(p))).ToList();
            }

            // Record the number of steps it will take to do the work
            Steps = _files.Count;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            base.OnDoWork(e);

            // Abort if there are no files to process
            if (_files == null || _files.Count == 0)
                return;

            // Process all files in the directory
            try
            {
                for (var i = 0; i < _files.Count; i++)
                {
                    var file = _files[i];

                    // Report progress
                    ReportProgress(i, string.Format(_message, file.Name));

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

        #endregion
    }
}
