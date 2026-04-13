using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autofac;
using Autofac.Core;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using DevExpress.Mvvm.UI;
using TIG.TotalLink.Client.Core.Interface;

namespace TIG.TotalLink.Client.Core
{
    public class AutofacViewLocator : IViewLocator, IFactoryLocator
    {
        #region Private Fields

        private static Assembly _entryAssembly;
        private readonly IEnumerable<Assembly> _assemblies;

        #endregion


        #region Constructors

        public AutofacViewLocator()
            : this(EntryAssembly != null && !EntryAssembly.IsInDesignMode() ? new[] { EntryAssembly } : new Assembly[0])
        {
        }

        public AutofacViewLocator(IEnumerable<Assembly> assemblies)
        {
            _assemblies = assemblies;
        }

        public AutofacViewLocator(params Assembly[] assemblies)
            : this((IEnumerable<Assembly>)assemblies)
        {
        }

        #endregion


        #region Static Properties

        /// <summary>
        /// The Autofac container that will be used to resolve views.
        /// </summary>
        public static IContainer Container { get; set; }

        /// <summary>
        /// Returns the default instance of AutofacViewLocator, or creates one if the default view locator has not yet been assigned.
        /// </summary>
        public static AutofacViewLocator Default
        {
            get
            {
                // Attempt to get the default view locator as an AutofacViewLocator
                var autofacViewLocator = ViewLocator.Default as AutofacViewLocator;
                if (autofacViewLocator == null)
                {
                    // If the default view locator is not an AutofacViewLocator, then we are probably in design mode, so create a dummy one to use
                    autofacViewLocator = new AutofacViewLocator();
                    ViewLocator.Default = autofacViewLocator;
                }

                return autofacViewLocator;
            }
        }

        #endregion


        #region Protected Properties

        protected Dictionary<string, Type> Types = new Dictionary<string, Type>();
        protected IEnumerator<Type> Enumerator;

        /// <summary>
        /// The entry assembly.
        /// </summary>
        protected static Assembly EntryAssembly
        {
            get
            {
                // Get the entry assembly if we don't already have it
                if (_entryAssembly == null)
                    _entryAssembly = Assembly.GetEntryAssembly();

                // Return the entry assembly
                return _entryAssembly;
            }
            set { _entryAssembly = value; }
        }

        /// <summary>
        /// All assemblies that this view locator will search for views in.
        /// </summary>
        protected IEnumerable<Assembly> Assemblies
        {
            get { return _assemblies; }
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Returns default content when a view cannot be found.
        /// </summary>
        /// <param name="documentType">The type of view that was being created.</param>
        /// <returns>Content describing the view that could not be found.</returns>
        protected virtual object CreateFallbackView(string documentType)
        {
            var tb = new TextBlock();
            var res = new ContentPresenter()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Content = tb,
            };
            if (ViewModelBase.IsInDesignMode)
            {
                tb.Text = string.Format("[{0}]", documentType);
                tb.FontSize = 18;
                tb.Foreground = new SolidColorBrush(Colors.Gray);
            }
            else
            {
                tb.Text = string.Format("\"{0}\" type not found.", documentType);
                tb.FontSize = 25;
                tb.Foreground = new SolidColorBrush(Colors.Red);
            }
            return res;
        }

        /// <summary>
        /// Returns default content when an exception ocurrs while creating a view.
        /// </summary>
        /// <param name="documentType">The type of view that was being created.</param>
        /// <param name="exception">The exception that ocurred while creating the view.</param>
        /// <returns>Content describing the exception that ocurred while creating the view.</returns>
        protected virtual object CreateFallbackView(string documentType, Exception exception)
        {
            return Helper.ViewHelper.CreateErrorView(documentType, "creating", exception.ToString());
        }

        /// <summary>
        /// Returns all types contained in the relevant assemblies.
        /// </summary>
        /// <returns>All types contained in the relevant assemblies.</returns>
        protected virtual IEnumerator<Type> GetTypes()
        {
            foreach (Assembly asm in Assemblies)
            {
                foreach (Type type in asm.GetTypes())
                {
                    yield return type;
                }
            }
        }

        #endregion


        #region IFactoryLocator

        /// <summary>
        /// Resolve a service.
        /// </summary>
        /// <typeparam name="T">The type of service to resolve.</typeparam>
        /// <returns>An instance of <typeparamref name="T"/>.</returns>
        public T Resolve<T>()
        {
            return Resolve<T>(null);

            //// Abort if the Container is null
            //if (Container == null)
            //    return default(T);

            //// Attempt to resolve the service
            //using (var scope = Container.BeginLifetimeScope())
            //{
            //    return scope.Resolve<T>();
            //}
        }

        /// <summary>
        /// Resolve a service.
        /// </summary>
        /// <typeparam name="T">The type of service to resolve.</typeparam>
        /// <param name="parameters">Parameters to pass to the constructor.</param>
        /// <returns>An instance of <typeparamref name="T"/>.</returns>
        public T Resolve<T>(params Parameter[] parameters)
        {
            // Abort if the Container is null
            if (Container == null)
                return default(T);

            // Attempt to resolve the service
            using (var scope = Container.BeginLifetimeScope())
            {
                return (parameters != null ? scope.Resolve<T>(parameters) : scope.Resolve<T>());
            }
        }

        /// <summary>
        /// Resolve a service.
        /// </summary>
        /// <param name="type">The type of service to resolve.</param>
        /// <returns>An instance of <paramref name="type"/>.</returns>
        public object Resolve(Type type)
        {
            return Resolve(type, null);
            //// Abort if the Container is null
            //if (Container == null)
            //    return null;

            //// Attempt to resolve the service
            //using (var scope = Container.BeginLifetimeScope())
            //{
            //    return scope.Resolve(type);
            //}
        }

        /// <summary>
        /// Resolve a service.
        /// </summary>
        /// <param name="type">The type of service to resolve.</param>
        /// <param name="parameters">Parameters to pass to the constructor.</param>
        /// <returns>An instance of <paramref name="type"/>.</returns>
        public object Resolve(Type type, params Parameter[] parameters)
        {
            // Abort if the Container is null
            if (Container == null)
                return null;

            // Attempt to resolve the service
            using (var scope = Container.BeginLifetimeScope())
            {
                return (parameters != null ? scope.Resolve(type, parameters) : scope.Resolve(type));
            }
        }

        #endregion


        #region IViewLocator

        object IViewLocator.ResolveView(string viewName)
        {
            if (Container == null)
                return null;

            try
            {
                using (var scope = Container.BeginLifetimeScope())
                {
                    var viewType = ((IViewLocator)this).ResolveViewType(viewName);
                    if (viewType != null)
                        return scope.Resolve(viewType);
                }
            }
            catch (Exception ex)
            {
                return CreateFallbackView(viewName, ex);
            }

            return CreateFallbackView(viewName);
        }

        public Type ResolveViewType(string viewName)
        {
            // Abort if the viewName is empty
            if (string.IsNullOrEmpty(viewName))
                return null;

            // Attempt to find the type from the list of cached types
            Type typeFromDictionary;
            if (Types.TryGetValue(viewName, out typeFromDictionary))
                return typeFromDictionary;

            // Attempt to find the type in all existing types
            if (Enumerator == null)
                Enumerator = GetTypes();
            while (Enumerator.MoveNext())
            {
                if (!Types.ContainsKey(Enumerator.Current.Name))
                {
                    Types[Enumerator.Current.Name] = Enumerator.Current;
                }
                if (Enumerator.Current.Name == viewName)
                    return Enumerator.Current;
            }

            return null;
        }

        #endregion
    }
}
