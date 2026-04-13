using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Core.Helper
{
    /// <summary>
    /// Helper class for hooking ValueChanged events on DependencyProperties without causing memory leaks.
    /// This class takes advantage of the fact that bindings use weak references to manage associations so
    /// the class will not root the object who property changes it is watching. It also uses a WeakReference
    /// to maintain a reference to the object whose property it is watching without rooting that object.
    /// In this way, you can maintain a collection of these objects so that you can unhook the property change
    /// later without worrying about that collection rooting the object whose values you are watching.
    /// https://agsmith.wordpress.com/2008/04/07/propertydescriptor-addvaluechanged-alternative/
    /// </summary>
    public sealed class PropertyChangeNotifier : DependencyObject, IDisposable
    {
        #region Private Fields

        private readonly WeakReference _propertySource;
        private readonly WeakReference _property;

        #endregion


        #region Constructors

        public PropertyChangeNotifier(DependencyObject propertySource, DependencyProperty property)
            : this(propertySource, new PropertyPath(property))
        {
            _property = new WeakReference(property);
        }

        private PropertyChangeNotifier(DependencyObject propertySource, PropertyPath property)
        {
            if (null == propertySource)
                throw new ArgumentNullException("propertySource");
            if (null == property)
                throw new ArgumentNullException("property");

            _propertySource = new WeakReference(propertySource);
            var binding = new Binding
            {
                Path = property,
                Mode = BindingMode.OneWay,
                Source = propertySource
            };
            BindingOperations.SetBinding(this, ValueProperty, binding);
        }

        #endregion


        #region Public Properties

        public DependencyObject PropertySource
        {
            get
            {
                try
                {
                    // note, it is possible that accessing the target property
                    // will result in an exception so i’ve wrapped this check
                    // in a try catch
                    return _propertySource.IsAlive
                    ? _propertySource.Target as DependencyObject
                    : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public DependencyProperty Property
        {
            get
            {
                try
                {
                    // note, it is possible that accessing the target property
                    // will result in an exception so i’ve wrapped this check
                    // in a try catch
                    return _property.IsAlive
                    ? _property.Target as DependencyProperty
                    : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        #endregion


        #region Value

        /// <summary>
        /// Identifies the <see cref="Value"/> dependency property
        /// </summary>
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value",
        typeof(object), typeof(PropertyChangeNotifier), new FrameworkPropertyMetadata(null, OnPropertyChanged));

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var notifier = (PropertyChangeNotifier)d;
            if (null != notifier.ValueChanged)
                notifier.ValueChanged(notifier, new PropertyChangeNotifierEventArgs(notifier.PropertySource, notifier.Property));
        }

        /// <summary>
        /// Returns/sets the value of the property
        /// </summary>
        /// <seealso cref="ValueProperty"/>
        [Description("Returns/sets the value of the property")]
        [Category("Behavior")]
        [Bindable(true)]
        public object Value
        {
            get
            {
                return GetValue(ValueProperty);
            }
            set
            {
                SetValue(ValueProperty, value);
            }
        }

        #endregion


        #region Events

        public event EventHandler<PropertyChangeNotifierEventArgs> ValueChanged;

        #endregion


        #region IDisposable

        public void Dispose()
        {
            BindingOperations.ClearBinding(this, ValueProperty);
        }

        #endregion
    }
}
