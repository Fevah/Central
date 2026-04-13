using CommandLine;

namespace TIG.TotalLink.IisAdmin
{
    public class CommandLineOptions
    {
        [Option('p', "parent-process-id", DefaultValue = 0, HelpText = "The id of the parent process.", Required = true)]
        public int ParentProcessId { get; set; }

        [Option('e', "express", DefaultValue = false, HelpText = "Set this flag to manage IIS Express instead of full IIS.")]
        public bool Express { get; set; }
    }
}
