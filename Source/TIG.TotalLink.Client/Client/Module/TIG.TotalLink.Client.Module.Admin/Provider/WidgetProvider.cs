using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.Provider
{
    /// <summary>
    /// Provides a list of all widgets in the loaded assemblies.
    /// </summary>
    public class WidgetProvider : IWidgetProvider
    {
        #region Constructors

        public WidgetProvider()
        {
            // Initialize collections
            Widgets = new ObservableCollection<WidgetViewModel>();

            // Initialize the list of available widgets
            Task.Run(() => InitializeWidgets());
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// All available widgets.
        /// </summary>
        public ObservableCollection<WidgetViewModel> Widgets { get; private set; }

        #endregion


        #region Private Methods

        /// <summary>
        /// Generates a list of widgets in all loaded assemblies.
        /// </summary>
        private void InitializeWidgets()
        {
            // Add the Blank Document widget
            Widgets.Add(new WidgetViewModel(new WidgetAttribute("Blank Document", "Global", "Creates an empty document."), "Admin"));

            // Add all widgets with WidgetAttributes
            foreach (var type in GetTypesWithWidgetAttribute())
            {
                var type1 = type;

                Application.Current.Dispatcher.Invoke(() => Widgets.Add(new WidgetViewModel(type1)));
            }
        }

        /// <summary>
        /// Gets the current host type from the entry assembly name.
        /// </summary>
        /// <returns>The current host type.</returns>
        private static HostTypes GetHostType()
        {
            var hostClientName = Assembly.GetEntryAssembly().GetName().Name.Split('.').Last();

            HostTypes hostType;

            if (!System.Enum.TryParse(hostClientName, out hostType))
            {
                hostType = HostTypes.None;
            }

            return hostType;
        }

        /// <summary>
        /// Finds all types that have a WidgetAttribute.
        /// </summary>
        /// <returns>All types that have a WidgetAttribute.</returns>
        private static IEnumerable<Type> GetTypesWithWidgetAttribute()
        {
            var hostType = GetHostType();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in
                    from type in assembly.GetTypes()
                    let hideWidgetAttribute = type.GetCustomAttribute<HideWidgetAttribute>()
                    where type.GetCustomAttributes(typeof(WidgetAttribute), true).Length > 0
                    && (hideWidgetAttribute == null || !hideWidgetAttribute.HostTypes.HasFlag(hostType))
                    select type)
                {
                    yield return type;
                }
            }
        }

        #endregion
    }
}
