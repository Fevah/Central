using System.Collections.Generic;
using Autofac.Core;

namespace TIG.IntegrationServer.DI.Autofac.ContainerBuilding.ModuleSource
{
    internal interface IModuleSource
    {
        /// <summary>
        /// Get all modules for builder container.
        /// </summary>
        /// <returns>Modules</returns>
        IEnumerable<IModule> GetModules();
    }
}
