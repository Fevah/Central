using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Windows;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Module.Admin.Attribute;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core
{
    public abstract class WidgetCustomizerViewModelBase : ViewModelBase
    {
        #region Private Fields

        private string _name;
        private int _order;
        private bool _isCustomization;

        #endregion


        #region Constructors

        protected WidgetCustomizerViewModelBase()
        {
            // Attempt to get the WidgetCustomizerAttribute
            var type = GetType();
            var widgetCustomizerAttribute = type.GetCustomAttribute<WidgetCustomizerAttribute>(true);
            if (widgetCustomizerAttribute == null)
                return;

            // Initialize properties from the WidgetCustomizerAttribute
            Name = widgetCustomizerAttribute.Name;
            Order = widgetCustomizerAttribute.Order;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of this widget customizer.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public string Name
        {
            get { return _name; }
            private set { SetProperty(ref _name, value, () => Name); }
        }

        /// <summary>
        /// Order that this customizer should appear among other customizers.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public int Order
        {
            get { return _order; }
            private set { SetProperty(ref _order, value, () => Order); }
        }

        /// <summary>
        /// Indicates if this widget customizer is active.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IsCustomization
        {
            get { return _isCustomization; }
            set { SetProperty(ref _isCustomization, value, () => IsCustomization, OnIsCustomizationChanged); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Called by the WidgetCustomizationControl to allow each WidgetCustomizer to create an instance of itself if it is required.
        /// Inheriters should define a 'new' version of this method, inspect the content, and return an instance of the customizer if it is required.
        /// </summary>
        /// <param name="content">The content of the WidgetCustomizationControl.</param>
        /// <param name="widget">The widget contained within the content.</param>
        /// <returns>An instance of the widget customizer if the customizer is relevant to the content; otherwise null.</returns>
        public static WidgetCustomizerViewModelBase CreateCustomizer(FrameworkElement content, WidgetViewModelBase widget)
        {
            return null;
        }

        /// <summary>
        /// Called when the associated widget is closed.
        /// </summary>
        public virtual void OnWidgetClosed()
        {
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Called when the value of the IsCustomization property is changed.
        /// </summary>
        protected virtual void OnIsCustomizationChanged()
        {
        }

        #endregion
    }
}
