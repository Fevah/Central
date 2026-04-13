using System;
using System.Windows;

namespace TIG.TotalLink.Client.Core.Helper
{
    public class PropertyChangeNotifierEventArgs : EventArgs
    {
        #region Private Fields

        private readonly WeakReference _propertySource;
        private readonly WeakReference _property;

        #endregion


        #region Constructors

        public PropertyChangeNotifierEventArgs(DependencyObject propertySource, DependencyProperty property)
        {
            _propertySource = new WeakReference(propertySource);
            _property = new WeakReference(property);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The object that the property value has changed on.
        /// </summary>
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

        /// <summary>
        /// The property that has changed.
        /// </summary>
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
    }
}
