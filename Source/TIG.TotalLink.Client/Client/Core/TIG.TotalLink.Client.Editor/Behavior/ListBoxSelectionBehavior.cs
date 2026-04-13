using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpf.Core.Native;

namespace TIG.TotalLink.Client.Editor.Behavior
{
    /// <summary>
    /// Attaches mouse events to ListBoxItems.
    /// This behavior should be attached to the root element within the ItemTemplate.
    /// </summary>
    public class ListBoxSelectionBehavior : Behavior<FrameworkElement>
    {
        #region Dependency Properties

        public static readonly DependencyProperty MouseEnterItemCommandProperty = DependencyProperty.RegisterAttached(
            "MouseEnterItemCommand", typeof(ICommand), typeof(ListBoxSelectionBehavior));
        public static readonly DependencyProperty MouseEnterItemCommandParameterProperty = DependencyProperty.RegisterAttached(
            "MouseEnterItemCommandParameter", typeof(object), typeof(ListBoxSelectionBehavior));
        public static readonly DependencyProperty MouseLeaveItemCommandProperty = DependencyProperty.RegisterAttached(
            "MouseLeaveItemCommand", typeof(ICommand), typeof(ListBoxSelectionBehavior));
        public static readonly DependencyProperty MouseLeaveItemCommandParameterProperty = DependencyProperty.RegisterAttached(
            "MouseLeaveItemCommandParameter", typeof(object), typeof(ListBoxSelectionBehavior));

        /// <summary>
        /// Command to execute when the mouse enters a list box item.
        /// </summary>
        public ICommand MouseEnterItemCommand
        {
            get { return (ICommand)GetValue(MouseEnterItemCommandProperty); }
            set { SetValue(MouseEnterItemCommandProperty, value); }
        }

        /// <summary>
        /// Parameter to pass to the MouseEnterItemCommand.
        /// </summary>
        public object MouseEnterItemCommandParameter
        {
            get { return GetValue(MouseEnterItemCommandParameterProperty); }
            set { SetValue(MouseEnterItemCommandParameterProperty, value); }
        }

        /// <summary>
        /// Command to execute when the mouse leaves a list box item.
        /// </summary>
        public ICommand MouseLeaveItemCommand
        {
            get { return (ICommand)GetValue(MouseLeaveItemCommandProperty); }
            set { SetValue(MouseLeaveItemCommandProperty, value); }
        }

        /// <summary>
        /// Parameter to pass to the MouseLeaveItemCommand.
        /// </summary>
        public object MouseLeaveItemCommandParameter
        {
            get { return GetValue(MouseLeaveItemCommandParameterProperty); }
            set { SetValue(MouseLeaveItemCommandParameterProperty, value); }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the ListBoxItem.MouseEvent event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ListBoxItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (MouseEnterItemCommand != null)
                MouseEnterItemCommand.Execute(MouseEnterItemCommandParameter);
        }

        /// <summary>
        /// Handles the ListBoxItem.MouseLeave event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ListBoxItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (MouseLeaveItemCommand != null)
                MouseLeaveItemCommand.Execute(MouseLeaveItemCommandParameter);
        }

        #endregion


        #region Overrides

        protected override void OnAttached()
        {
            base.OnAttached();

            // Find the parent ListBoxItem
            var listBoxItem = LayoutHelper.FindParentObject<ListBoxItem>(AssociatedObject);
            if (listBoxItem == null)
                return;

            // Attach events
            listBoxItem.MouseEnter += ListBoxItem_MouseEnter;
            listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            // Find the parent ListBoxItem
            var listBoxItem = LayoutHelper.FindParentObject<ListBoxItem>(AssociatedObject);
            if (listBoxItem == null)
                return;

            // Detach events
            listBoxItem.MouseEnter -= ListBoxItem_MouseEnter;
            listBoxItem.MouseLeave -= ListBoxItem_MouseLeave;
        }

        #endregion

    }
}
