using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Popups;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.RichEdit;
using DevExpress.XtraRichEdit;
using TIG.TotalLink.Client.Editor.Helper;

namespace TIG.TotalLink.Client.Editor.View
{
    /// <summary>
    /// Interaction logic for RichTextEdit.xaml
    /// </summary>
    public partial class RichTextEdit : INotifyPropertyChanged
    {
        #region Dependency Properties

        public static readonly DependencyProperty EditValueProperty = DependencyProperty.RegisterAttached("EditValue", typeof(string), typeof(RichTextEdit),
            new FrameworkPropertyMetadata { BindsTwoWayByDefault = true, DefaultUpdateSourceTrigger = UpdateSourceTrigger.LostFocus });

        public static readonly DependencyProperty EditModeProperty = DependencyProperty.RegisterAttached("EditMode", typeof(EditMode), typeof(RichTextEdit), new PropertyMetadata(EditMode.Standalone, (d, e) => ((RichTextEdit)d).OnEditModeChanged()));

        public static readonly DependencyProperty SaveCommandProperty = DependencyProperty.RegisterAttached("SaveCommand", typeof(ICommand), typeof(RichTextEdit));

        public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.RegisterAttached("CancelCommand", typeof(ICommand), typeof(RichTextEdit));

        /// <summary>
        /// Stores the main EditValue for this editor.
        /// </summary>
        public string EditValue
        {
            get { return (string)GetValue(EditValueProperty); }
            set { SetValue(EditValueProperty, value); }
        }

        /// <summary>
        /// Indicates the mode that the editor is running in.
        /// </summary>
        public EditMode EditMode
        {
            get { return (EditMode)GetValue(EditModeProperty); }
            set { SetValue(EditModeProperty, value); }
        }

        /// <summary>
        /// A command to save the changes.
        /// </summary>
        public ICommand SaveCommand
        {
            get { return (ICommand)GetValue(SaveCommandProperty); }
            set { SetValue(SaveCommandProperty, value); }
        }

        /// <summary>
        /// A command to cancel the changes.
        /// </summary>
        public ICommand CancelCommand
        {
            get { return (ICommand)GetValue(CancelCommandProperty); }
            set { SetValue(CancelCommandProperty, value); }
        }

        #endregion


        #region Private Fields

        private bool _toolBarsVisible = true;
        private bool _scrollBarsVisible = true;
        private bool _bordersVisible = true;

        #endregion


        #region Constructors

        public RichTextEdit()
        {
            InitializeComponent();

            // Initialize commands
            DefaultSaveCommand = new DelegateCommand(OnDefaultSaveExecute);
            DefaultCancelCommand = new DelegateCommand(OnDefaultCancelExecute);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The default command to execute when the Save button is pressed.
        /// </summary>
        public ICommand DefaultSaveCommand { get; private set; }

        /// <summary>
        /// The default command to execute when the Cancel button is pressed.
        /// </summary>
        public ICommand DefaultCancelCommand { get; private set; }

        /// <summary>
        /// Indicates if the toolbars are visible.
        /// </summary>
        public bool ToolBarsVisible
        {
            get { return _toolBarsVisible; }
            private set
            {
                if (_toolBarsVisible != value)
                {
                    _toolBarsVisible = value;
                    RaisePropertyChanged(() => ToolBarsVisible);
                }
            }
        }

        /// <summary>
        /// Indicates if the scroll bars are visible.
        /// </summary>
        public bool ScrollBarsVisible
        {
            get { return _scrollBarsVisible; }
            private set
            {
                if (_scrollBarsVisible != value)
                {
                    _scrollBarsVisible = value;
                    RaisePropertyChanged(() => ScrollBarsVisible);
                }
            }
        }

        /// <summary>
        /// Indicates if the borders are visible.
        /// </summary>
        public bool BordersVisible
        {
            get { return _bordersVisible; }
            private set
            {
                if (_bordersVisible != value)
                {
                    _bordersVisible = value;
                    RaisePropertyChanged(() => BordersVisible);
                }
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Forces the EditValue to be written to the data source.
        /// </summary>
        private void PostEditor()
        {
            // Force the EditValue binding to update
            BindingHelper.UpdateSource(this, EditValueProperty);

            // If this editor is located within a grid, we will need to instruct the grid to post changes...

            // Attempt to find a PopupContentContainer containing this RichTextEdit
            var popupContainer = LayoutHelper.FindParentObject<PopupContentContainer>(this);
            if (popupContainer == null)
                return;

            // Attempt to get the PopupBaseEdit which owns the PopupContentContainer
            var popup = PopupBaseEdit.GetPopupOwnerEdit(popupContainer);
            if (popup == null)
                return;

            // Attempt to get the DataViewBase which contains the editor
            var dataViewBase = LayoutHelper.FindParentObject<DataViewBase>(popup);
            if (dataViewBase == null)
                return;

            // Post the editor
            dataViewBase.PostEditor();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the DefaultSaveCommand.
        /// </summary>
        private void OnDefaultSaveExecute()
        {
            // Save the modified value
            PostEditor();

            // Clear the undo list
            // Disabling and re-enabling the undo system ensures that the toolbar is refreshed (which doesn't happen if we just call ClearUndo)
            RichEditControl.Options.DocumentCapabilities.Undo = DocumentCapability.Disabled;
            RichEditControl.Options.DocumentCapabilities.Undo = DocumentCapability.Enabled;
        }

        /// <summary>
        /// Execute method for the DefaultCancelCommand.
        /// </summary>
        private void OnDefaultCancelExecute()
        {
            // Undo all
            while (RichEditControl.CanUndo)
            {
                RichEditControl.Undo();
            }

            // Clear the undo list
            // Disabling and re-enabling the undo system ensures that the toolbar is refreshed (which doesn't happen if we just call ClearUndo)
            RichEditControl.Options.DocumentCapabilities.Undo = DocumentCapability.Disabled;
            RichEditControl.Options.DocumentCapabilities.Undo = DocumentCapability.Enabled;
        }

        /// <summary>
        /// Called when the EditMode changes.
        /// </summary>
        private void OnEditModeChanged()
        {
            ToolBarsVisible = EditMode != EditMode.InplaceInactive;
            ScrollBarsVisible = EditMode != EditMode.InplaceInactive;
            BordersVisible = EditMode != EditMode.InplaceInactive;
        }

        /// <summary>
        /// Handles errors in the RichEditControl, such as trying to save to a locked file.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event parameters.</param>
        private void RichEditControl_UnhandledException(object sender, RichEditUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            MessageBox.Show(e.Exception.Message, "Error");
        }

        /// <summary>
        /// Handles the Loaded event for the RichEditControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event parameters.</param>
        private void RichEditControl_GotFocus(object sender, RoutedEventArgs e)
        {
            // When the RichEditControl gets focus, move the focus to the text area (but only if the toolbars are visible)
            if (ToolBarsVisible && RichEditControl != null && RichEditControl.KeyCodeConverter != null)
                RichEditControl.KeyCodeConverter.Focus();
        }

        #endregion


        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged<T1>(Expression<Func<T1>> expression)
        {
            var changedEventHandler = PropertyChanged;
            if (changedEventHandler == null)
                return;
            changedEventHandler(this, new PropertyChangedEventArgs(BindableBase.GetPropertyName(expression)));
        }

        #endregion
    }
}