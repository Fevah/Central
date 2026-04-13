using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using DevExpress.Data;
using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Editor.Helper;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Editor.Behavior
{
    /// <summary>
    /// A helper to bind to a property of a data object via a dynamic property name.
    /// </summary>
    public class DataObjectPropertyNameBindingBehavior : Behavior<FrameworkElement>
    {
        #region Dependency Properties

        public static readonly DependencyProperty TargetPropertyNameProperty = DependencyProperty.RegisterAttached(
            "TargetPropertyName", typeof(string), typeof(DataObjectPropertyNameBindingBehavior), new PropertyMetadata((s, e) => ((DataObjectPropertyNameBindingBehavior)s).OnTargetPropertyNameChanged(e)));

        public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached(
            "Path", typeof(string), typeof(DataObjectPropertyNameBindingBehavior), new PropertyMetadata((s, e) => ((DataObjectPropertyNameBindingBehavior)s).OnPathChanged(e)));

        public static readonly DependencyProperty InitializerProperty = DependencyProperty.RegisterAttached(
            "Initializer", typeof(EditorInitializer), typeof(DataObjectPropertyNameBindingBehavior));

        public static readonly DependencyProperty BindingModeProperty = DependencyProperty.RegisterAttached(
            "BindingMode", typeof(BindingMode), typeof(DataObjectPropertyNameBindingBehavior), new PropertyMetadata(BindingMode.TwoWay));

        /// <summary>
        /// The name of the target property on the target object.
        /// </summary>
        public string TargetPropertyName
        {
            get { return (string)GetValue(TargetPropertyNameProperty); }
            set { SetValue(TargetPropertyNameProperty, value); }
        }

        /// <summary>
        /// The path on the data object to create a binding to.
        /// </summary>
        public string Path
        {
            get { return (string)GetValue(PathProperty); }
            set { SetValue(PathProperty, value); }
        }

        /// <summary>
        /// The EditorInitializer for this instance of the behavior.
        /// </summary>
        public EditorInitializer Initializer
        {
            get { return (EditorInitializer)GetValue(InitializerProperty); }
            set { SetValue(InitializerProperty, value); }
        }

        /// <summary>
        /// The BindingMode to use on the new binding that is created.
        /// </summary>
        public BindingMode BindingMode
        {
            get { return (BindingMode)GetValue(BindingModeProperty); }
            set { SetValue(BindingModeProperty, value); }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Updates the binding to the data object.
        /// </summary>
        /// <param name="dataContext">The object that will be bound.</param>
        private void UpdateBinding(object dataContext)
        {
            // Abort if the dataContext is not loaded yet
            if (dataContext == null || dataContext is NotLoadedObject || (dataContext is IXPInvalidateableObject && ((IXPInvalidateableObject)dataContext).IsInvalidated))
                return;

            // Abort if TargetProperty or Path are blank
            if (string.IsNullOrWhiteSpace(TargetPropertyName) || string.IsNullOrWhiteSpace(Path))
                return;

            // If the target object and the property referenced by the first part of the Path are both DataObjectBase then the property value will return as an XPCollection instead
            // So we append a ! to the Path segment to make the binding connect to the contents instead of the collection
            // See https://www.devexpress.com/Support/Center/Question/Details/A2783
            var path = Path;
            if (dataContext is DataObjectBase)
            {
                var pathParts = Path.Split('.');
                var firstPathProperty = dataContext.GetType().GetProperty(pathParts.First());
                if (firstPathProperty != null && typeof(DataObjectBase).IsAssignableFrom(firstPathProperty.PropertyType))
                {
                    pathParts[0] = pathParts[0] + "!";
                    path = string.Join(".", pathParts);
                }
            }

            // Get a DependencyPropertyDescriptor for the target property
            var targetPropertyDescriptor = DependencyPropertyDescriptor.FromName(TargetPropertyName, AssociatedObject.GetType(), AssociatedObject.GetType());
            if (targetPropertyDescriptor == null)
                return;

            // Create a binding from the Path to the target property
            var binding = new Binding(path)
            {
                Source = dataContext,
                Mode = BindingMode
            };
            AssociatedObject.SetBinding(targetPropertyDescriptor.DependencyProperty, binding);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Called when the TargetPropertyName property changes.
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnTargetPropertyNameChanged(DependencyPropertyChangedEventArgs e)
        {
            if (Initializer != null)
                Initializer.AttemptInitialize();
        }

        /// <summary>
        /// Called when the Path property changes.
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnPathChanged(DependencyPropertyChangedEventArgs e)
        {
            if (Initializer != null)
                Initializer.AttemptInitialize();
        }

        #endregion


        #region Overrides

        protected override void OnAttached()
        {
            base.OnAttached();

            // Create an editorInitializer to manage the binding updates
            Initializer = new EditorInitializer(AssociatedObject, UpdateBinding);
            Initializer.AttemptInitialize();
        }

        #endregion
    }
}
