using System.Windows;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.LayoutControl;
using TIG.TotalLink.Client.Editor.Control.EventArgs;

namespace TIG.TotalLink.Client.Editor.Control
{
    public class LayoutGroupEx : LayoutGroup
    {
        #region Dependency Properties

        protected static DependencyProperty ChildLabelListener = RegisterChildPropertyListener("Label", typeof(LayoutGroupEx));
        //protected static DependencyProperty ChildIsVisibleListener = RegisterChildPropertyListener("IsVisble", typeof(LayoutGroupEx));

        #endregion


        #region Private Fields

        private bool _layoutLoaded;

        #endregion


        #region Private Methods

        /// <summary>
        /// Raises the LayoutModified event on the parent DataLayoutControlEx
        /// </summary>
        /// <param name="e">Event arguments.</param>
        private void RaiseLayoutModified(LayoutModifiedEventArgs e)
        {
            // Attempt to get the layout root as a DataLayoutControlEx
            var dataLayoutControl = Root as DataLayoutControlEx;
            if (dataLayoutControl == null)
                return;

            // Raise the LayoutChildAdded event
            dataLayoutControl.RaiseLayoutModified(e);
        }

        #endregion


        #region Overrides

        protected override FrameworkElement FindByXMLID(string id)
        {
            return ((DataLayoutControlEx)Root).FindElementByXmlId(id);
        }

        protected override void OnHeaderChanged()
        {
            base.OnHeaderChanged();

            // If the Header is set to an empty string, change it to a space
            // Otherwise the group will display "[LayoutGroupEx]" when shown in AvailableItems
            if (string.IsNullOrEmpty(Header.ToString()))
                Header = " ";
        }

        protected override void AttachChildPropertyListeners(FrameworkElement child)
        {
            base.AttachChildPropertyListeners(child);

            //// Add child property listeners for FrameworkElement properties
            //AttachChildPropertyListener(child, "IsVisible", ChildIsVisibleListener);

            // Add child property listeners for ControlBase properties
            if (child is ControlBase)
            {
                AttachChildPropertyListener(child, "Label", ChildLabelListener);
            }
        }

        protected override void DetachChildPropertyListeners(FrameworkElement child)
        {
            base.DetachChildPropertyListeners(child);

            // Don't detach if the child is still parented
            if (child.Parent is LayoutGroup)
                return;

            //// Remove child property listeners for FrameworkElement properties
            //DetachChildPropertyListener(child, ChildIsVisibleListener);
            
            // Remove child property listeners for ControlBase properties
            if (child is ControlBase)
            {
                DetachChildPropertyListener(child, ChildLabelListener);
            }
        }

        protected override void OnLayoutUpdated()
        {
            base.OnLayoutUpdated();

            // Abort if the layout is already loaded
            if (_layoutLoaded)
                return;

            // Flag the layout as loaded
            _layoutLoaded = true;
        }

        protected override void OnChildAdded(FrameworkElement child)
        {
            base.OnChildAdded(child);

            // Abort if the layout is still loading
            if (!_layoutLoaded)
                return;

            // Notify that the layout has changed
            RaiseLayoutModified(new LayoutModifiedEventArgs(this, LayoutModifiedEventArgs.LayoutChangeTypes.Add, child));
        }

        protected override void OnChildRemoved(FrameworkElement child)
        {
            base.OnChildRemoved(child);

            // Abort if the layout is still loading
            if (!_layoutLoaded)
                return;

            // Notify that the layout has changed
            RaiseLayoutModified(new LayoutModifiedEventArgs(this, LayoutModifiedEventArgs.LayoutChangeTypes.Remove, child));
        }

        protected override void OnChildPropertyChanged(FrameworkElement child, DependencyProperty propertyListener, object oldValue, object newValue)
        {
            base.OnChildPropertyChanged(child, propertyListener, oldValue, newValue);
            
            // Abort if the layout is still loading
            if (!_layoutLoaded)
                return;

            // Ignore visibility changes
            if (propertyListener.Name == "ChildVisibilityListener")
                return;
            //if (propertyListener.Name == "ChildIsVisbleListener" || propertyListener.Name == "ChildVisibilityListener")
            //    return;

            // Notify that the layout has changed
            RaiseLayoutModified(new LayoutModifiedEventArgs(this, LayoutModifiedEventArgs.LayoutChangeTypes.PropertyChange, child, propertyListener, oldValue, newValue));
        }

        #endregion
    }
}
