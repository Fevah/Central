using System.Windows;
using DevExpress.Xpf.LayoutControl;

namespace TIG.TotalLink.Client.Editor.Control.EventArgs
{
    public class LayoutModifiedEventArgs : System.EventArgs
    {
        #region Public Enums

        public enum LayoutChangeTypes
        {
            Add,
            Remove,
            PropertyChange
        }

        #endregion


        #region Constructors

        public LayoutModifiedEventArgs(LayoutGroup group, LayoutChangeTypes changeType, FrameworkElement child)
        {
            Group = group;
            ChangeType = changeType;
            Child = child;
        }

        public LayoutModifiedEventArgs(LayoutGroup group, LayoutChangeTypes changeType, FrameworkElement child, DependencyProperty propertyListener, object oldValue, object newValue)
            : this(group, changeType, child)
        {
            // Extract the PropertyName from the propertyListener.Name
            // propertyListener.Name will always be in the format "Child{PropertyName}Listener"
            PropertyName = propertyListener.Name.Substring(5, propertyListener.Name.Length - 13);

            OldPropertyValue = oldValue;
            NewPropertyValue = newValue;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The parent LayoutGroup that contains the changed child element.
        /// </summary>
        public LayoutGroup Group { get; private set; }

        /// <summary>
        /// The type of change that occured.
        /// </summary>
        public LayoutChangeTypes ChangeType { get; private set; }

        /// <summary>
        /// The child item that was added/removed or modified.
        /// </summary>
        public FrameworkElement Child { get; private set; }

        /// <summary>
        /// The name of the property that changed when the change type is PropertyChange.
        /// </summary>
        public string PropertyName { get; private set; }

        /// <summary>
        /// The old value of the property that changed when the change type is PropertyChange.
        /// </summary>
        public object OldPropertyValue { get; private set; }

        /// <summary>
        /// The new value of the property that changed when the change type is PropertyChange.
        /// </summary>
        public object NewPropertyValue { get; private set; }

        #endregion
    }
}
