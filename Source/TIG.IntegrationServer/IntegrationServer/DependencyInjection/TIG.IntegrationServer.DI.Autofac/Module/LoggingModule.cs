using System.ComponentModel.Composition;
using System.Diagnostics;
using Autofac;
using Autofac.Core;
using Microsoft.Practices.EnterpriseLibrary.Logging;
using Microsoft.Practices.EnterpriseLibrary.Logging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.Logging.TraceListeners;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.LogMessage.Enum;
using TIG.IntegrationServer.Logging.EnterpriseLib.LogAdapters;

namespace TIG.IntegrationServer.DI.Autofac.Module
{
    [Export("common", typeof(IModule))]
    internal class LoggingModule : global::Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder
                .Register(c =>
                {
                    var logFormatter = new XmlLogFormatter();

                    // Constructor common listener
                    var commonListener = new RollingFlatFileTraceListener(
                                            @"Log\Common\common.log",
                                            null,
                                            null,
                                            logFormatter,
                                            0,
                                            "yyyy-MM-dd HH-mm-ss",
                                            RollFileExistsBehavior.Increment,
                                            RollInterval.Day,
                                            0);

                    // Constructor error listener
                    var errorListener = new RollingFlatFileTraceListener(
                                            @"Log\Error\error.log",
                                            null,
                                            null,
                                            logFormatter,
                                            0,
                                            "yyyy-MM-dd HH-mm-ss",
                                            RollFileExistsBehavior.Increment,
                                            RollInterval.Day,
                                            0);

                    // Build Logging configuration
                    var logConfiguration = new LoggingConfiguration();
                    logConfiguration.AddLogSource(LogType.Debug.ToString(), SourceLevels.All, true, commonListener);
                    logConfiguration.AddLogSource(LogType.Trace.ToString(), SourceLevels.All, true, commonListener);
                    logConfiguration.AddLogSource(LogType.Info.ToString(), SourceLevels.All, true, commonListener);
                    logConfiguration.AddLogSource(LogType.Warn.ToString(), SourceLevels.All, true, commonListener);
                    logConfiguration.AddLogSource(LogType.Error.ToString(), SourceLevels.All, true, errorListener);
                    logConfiguration.AddLogSource(LogType.Fatal.ToString(), SourceLevels.All, true, errorListener);

                    // Builder log writer with log configuration
                    var logWriter = new LogWriter(logConfiguration);

                    return new LogAdapter(logWriter);
                })
                .As<ILog>()
                .SingleInstance();
        }

        #endregion
    }
}
