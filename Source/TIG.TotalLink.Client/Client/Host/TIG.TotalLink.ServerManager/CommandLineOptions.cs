using CommandLine;

namespace TIG.TotalLink.ServerManager
{
    /// <summary>
    /// Command Line Options for login or setting system in background.
    /// </summary>
    public class CommandLineOptions
    {
        #region Public Properties

        /// <summary>
        /// Delays each step in the startup workers by the specifed number of milliseconds.
        /// </summary>
        [Option('d', "delay-startup", DefaultValue = 0, HelpText = "Delays each step in the startup workers by the specifed number of milliseconds.")]
        public int DelayStartup { get; set; }

        #endregion
    }
}
