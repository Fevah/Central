using System;

namespace TIG.TotalLink.Client.Core.Interface
{
    public interface IFactoryLocator
    {
        #region IFactoryLocator

        /// <summary>
        /// Resolve a service.
        /// </summary>
        /// <typeparam name="T">The type of service to resolve.</typeparam>
        /// <returns>An instance of <typeparamref name="T"/>.</returns>
        T Resolve<T>();

        /// <summary>
        /// Resolve a service.
        /// </summary>
        /// <param name="type">The type of service to resolve.</param>
        /// <returns>An instance of <paramref name="type"/>.</returns>
        object Resolve(Type type);

        #endregion
    }
}
