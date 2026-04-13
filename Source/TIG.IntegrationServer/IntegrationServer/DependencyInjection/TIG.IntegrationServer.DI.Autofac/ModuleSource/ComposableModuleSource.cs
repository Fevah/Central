using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using Autofac.Core;

namespace TIG.IntegrationServer.DI.Autofac.ContainerBuilding.ModuleSource
{
    internal abstract class ComposableModuleSource : IModuleSource
    {
        #region Private Properties

        private readonly Func<ComposablePartCatalog> _catalogFactory;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with catalog factory
        /// </summary>
        /// <param name="catalogFactory">Catalog factory</param>
        protected ComposableModuleSource(Func<ComposablePartCatalog> catalogFactory)
        {
            _catalogFactory = catalogFactory;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Get all modules for builder container.
        /// </summary>
        /// <returns>Modules</returns>
        public IEnumerable<IModule> GetModules()
        {
            var context = BuildCompositionContext();
            Compose(context);

            var modules = context.Modules ?? new IModule[0];
            return modules;
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Build composition context
        /// </summary>
        /// <returns>Composition context</returns>
        protected abstract CompositionContext BuildCompositionContext();

        #endregion


        #region Private Methods

        /// <summary>
        /// Compose parts (modules) from context.
        /// </summary>
        /// <param name="context">Composition context</param>
        private void Compose(CompositionContext context)
        {
            if (_catalogFactory == null)
                return;

            // Using MEF to compose parts
            using (var catalog = _catalogFactory())
            {
                using (var container = new CompositionContainer(catalog))
                {
                    container.ComposeParts(context);
                }
            }
        }

        #endregion


        #region Protected Class Defination

        protected abstract class CompositionContext
        {
            /// <summary>
            /// Modules
            /// </summary>
            internal abstract IEnumerable<IModule> Modules { get; }
        }

        #endregion
    }
}
