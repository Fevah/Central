
using System;
using Autofac;

namespace TIG.IntegrationServer.Common
{
    public class AutofacLocator
    {
        #region Publuc properties

        /// <summary>
        /// Container for create component
        /// </summary>
        public static IContainer Container { get; set; }

        #endregion

        /// <summary>
        /// Resolve component by type
        /// </summary>
        /// <typeparam name="T">Regestied type to resolve</typeparam>
        /// <returns>Instance of T</returns>
        public static T Resolve<T>()
        {
            if (Container == null)
                return default(T);

            using (var scope = Container.BeginLifetimeScope())
            {
                return scope.Resolve<T>();
            }
        }

        /// <summary>
        /// Resolve by name key
        /// </summary>
        /// <typeparam name="T">Regestied type to resolve</typeparam>
        /// <param name="converterName">Conveter name</param>
        /// <returns>Instance of T</returns>
        public static T ResolveConverter<T>(string converterName)
        {
            if (Container == null)
                return default(T);

            using (var scope = Container.BeginLifetimeScope())
            {
                return scope.ResolveNamed<T>(converterName);
            }
        }

        /// <summary>
        /// Resolve by name key
        /// </summary>
        /// <typeparam name="T">Regestied type to resolve</typeparam>
        /// <param name="key">Key of agent plugin</param>
        /// <returns>Instance of T</returns>
        public static T ResolveAgentPlugin<T>(Guid key)
        {
            if (Container == null)
                return default(T);

            using (var scope = Container.BeginLifetimeScope())
            {
                return scope.ResolveKeyed<T>(key);
            }
        }

        /// <summary>
        /// Resolve mapperPlugin by name key
        /// </summary>
        /// <typeparam name="T">Regestied type to resolve</typeparam>
        /// <param name="key">Key of mapper plugin</param>
        /// <returns>Instance of T</returns>
        public static T ResolveMapperPlugin<T>(Guid key)
        {
            if (Container == null)
                return default(T);

            using (var scope = Container.BeginLifetimeScope())
            {
                return scope.ResolveKeyed<T>(key);
            }
        }

        /// <summary>
        /// Resolve changeTrackerPlugin by name key
        /// </summary>
        /// <typeparam name="T">Regestied type to resolve</typeparam>
        /// <param name="key">Key of agent plugin</param>
        /// <returns>Instance of T</returns>
        public static T ResolveChangeTrackerPlugin<T>(Guid key)
        {
            if (Container == null)
                return default(T);

            using (var scope = Container.BeginLifetimeScope())
            {
                return scope.ResolveKeyed<T>(key);
            }
        }
    }
}