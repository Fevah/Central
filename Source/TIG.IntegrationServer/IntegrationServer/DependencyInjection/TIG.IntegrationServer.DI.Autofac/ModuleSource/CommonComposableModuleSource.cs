using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;
using Autofac.Core;

namespace TIG.IntegrationServer.DI.Autofac.ContainerBuilding.ModuleSource
{
    internal class CommonComposableModuleSource : ComposableModuleSource
    {
        #region Constructors

        /// <summary>
        /// Constructor with catalog factory
        /// </summary>
        /// <param name="catalogFactory">Catalog factory</param>
        public CommonComposableModuleSource(Func<ComposablePartCatalog> catalogFactory)
            : base(catalogFactory) { }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Build composition context
        /// </summary>
        /// <returns>Composition context</returns>
        protected override CompositionContext BuildCompositionContext()
        {
            return new CommonCompositionContext();
        }

        #endregion


        #region Protected Class Defination

        protected class CommonCompositionContext : CompositionContext
        {
            // MEF to import all module
            [ImportMany("common", typeof(IModule))]
            private IEnumerable<IModule> _modules;

            internal override IEnumerable<IModule> Modules
            {
                get { return _modules; }
            }
        }

        #endregion
    }
}
