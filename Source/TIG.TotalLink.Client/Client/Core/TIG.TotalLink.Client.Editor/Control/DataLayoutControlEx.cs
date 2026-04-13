using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml;
using DevExpress.Entity.Model;
using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Validation;
using DevExpress.Xpf.LayoutControl;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Control.EventArgs;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Enum;
using TIG.TotalLink.Client.Editor.Helper;
using TIG.TotalLink.Client.Editor.Interface;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;
using TIG.TotalLink.Client.Editor.Wrapper.Type;
using DataObjectBase = TIG.TotalLink.Shared.DataModel.Core.DataObjectBase;

namespace TIG.TotalLink.Client.Editor.Control
{
    public class DataLayoutControlEx : DataLayoutControl, INotifyPropertyChanged
    {
        #region Dependency Properties

        public static readonly DependencyProperty EditModeProperty = DependencyProperty.RegisterAttached("EditMode", typeof(DetailEditMode), typeof(DataLayoutControlEx), new PropertyMetadata(DetailEditMode.Edit));
        public static readonly DependencyProperty EditorGeneratorTemplateSelectorProperty = DependencyProperty.RegisterAttached("EditorGeneratorTemplateSelector", typeof(DataTemplateSelector), typeof(DataLayoutControlEx));
        public static readonly DependencyProperty IsValidProperty = DependencyProperty.RegisterAttached("IsValid", typeof(bool), typeof(DataLayoutControlEx), new PropertyMetadata(true));
        public static readonly DependencyProperty IsModifiedProperty = DependencyProperty.RegisterAttached("IsModified", typeof(bool), typeof(DataLayoutControlEx), new PropertyMetadata(false));
        public static readonly DependencyProperty ExternalCustomizationControlProperty = DependencyProperty.Register("ExternalCustomizationControl", typeof(LayoutControlCustomizationControl), typeof(DataLayoutControlEx), new PropertyMetadata((o, e) => ((DataLayoutControlEx)o).OnExternalCustomizationControlChanged()));


        /// <summary>
        /// The mode that the form is using to edit.
        /// </summary>
        public DetailEditMode EditMode
        {
            get { return (DetailEditMode)GetValue(EditModeProperty); }
            set { SetValue(EditModeProperty, value); }
        }

        /// <summary>
        /// A DataTemplateSelector that will be used to resolve editor templates.
        /// </summary>
        public DataTemplateSelector EditorGeneratorTemplateSelector
        {
            get { return (DataTemplateSelector)GetValue(EditorGeneratorTemplateSelectorProperty); }
            set { SetValue(EditorGeneratorTemplateSelectorProperty, value); }
        }

        /// <summary>
        /// Indicates if all fields on the form are valid.
        /// </summary>
        public bool IsValid
        {
            get { return (bool)GetValue(IsValidProperty); }
            set
            {
                if (Equals(IsValid, value))
                    return;

                SetValue(IsValidProperty, value);
            }
        }

        /// <summary>
        /// Indicates if any of the fields on the form have been modified.
        /// </summary>
        public bool IsModified
        {
            get { return (bool)GetValue(IsModifiedProperty); }
            set
            {
                if (Equals(IsModified, value))
                    return;

                SetValue(IsModifiedProperty, value);
            }
        }

        /// <summary>
        /// An external customization control that this DataLayoutControl will use for customization.
        /// </summary>
        public LayoutControlCustomizationControl ExternalCustomizationControl
        {
            get { return (LayoutControlCustomizationControl)GetValue(ExternalCustomizationControlProperty); }
            set { SetValue(ExternalCustomizationControlProperty, value); }
        }

        #endregion


        #region Public Events

        public delegate void LayoutModifiedEventHandler(object sender, LayoutModifiedEventArgs e);

        public event LayoutModifiedEventHandler LayoutModified;

        #endregion


        #region Private Fields

        private readonly Dictionary<string, DataLayoutItemWrapper> _itemWrappers = new Dictionary<string, DataLayoutItemWrapper>();
        private DataLayoutControlWrapper _typeWrapper;
        private static readonly MethodInfo _getIdMethod;
        private bool _showHeader = true;
        private bool _layoutUpdating;

        #endregion


        #region Constructors

        static DataLayoutControlEx()
        {
            _getIdMethod = typeof(LayoutControl).GetMethod("GetID", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if the header is displayed at the top of the form.
        /// </summary>
        public bool ShowHeader
        {
            get { return _showHeader; }
            set
            {
                if (_showHeader == value)
                    return;

                _showHeader = value;
                RaisePropertyChanged(() => ShowHeader);
                RaiseLayoutModified(null);
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets the data form layout.
        /// </summary>
        /// <returns>A Stream containing the data form layout.</returns>
        public Stream GetLayout()
        {
            // Remove any manually created groups because they will not restore correctly
            // If there were any items within the group, they will be restored to the AvailableItems collection
            for (var i = AvailableItems.Count - 1; i > -1; i--)
            {
                var group = AvailableItems[i] as LayoutGroupEx;
                if (group != null && GetIsUserDefined(group))
                    AvailableItems.RemoveAt(i);
            }

            // Save the layout to a stream
            var layout = new MemoryStream();
            var writer = XmlWriter.Create(layout);
            WriteToXML(writer);
            writer.Flush();
            layout.Seek(0, SeekOrigin.Begin);

            // Return the stream
            return layout;
        }

        /// <summary>
        /// Sets the data form layout.
        /// </summary>
        /// <param name="layout">A Stream containing the data form layout to apply.</param>
        public void SetLayout(Stream layout)
        {
            // Abort if no layout was supplied
            if (layout == null)
                return;

            // Restore the layout
            using (var reader = XmlReader.Create(layout))
            {
                ReadFromXML(reader);
            }
        }

        /// <summary>
        /// Gets the Xml ID for the specified element.
        /// </summary>
        /// <param name="element">The element to collect the Xml ID for.</param>
        /// <returns>A string containing the Xml ID for the specified element.</returns>
        public string GetXmlId(FrameworkElement element)
        {
            return (string)_getIdMethod.Invoke(this, new object[] { element });
        }

        /// <summary>
        /// Finds an element based on its Xml ID.
        /// </summary>
        /// <param name="id">The Xml ID to search for.</param>
        /// <returns>The element with the specified Xml ID if one was found; otherwise null.</returns>
        public FrameworkElement FindElementByXmlId(string id)
        {
            return LayoutHelper.FindElement(this, e => id == GetXmlId(e));
        }

        /// <summary>
        /// Raises the LayoutModified event.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        public void RaiseLayoutModified(LayoutModifiedEventArgs e)
        {
            if (LayoutModified == null)
                return;

            //if (e.ChangeType == LayoutModifiedEventArgs.LayoutChangeTypes.PropertyChange)
            //    System.Diagnostics.Debug.WriteLine("{0} : RaiseLayoutModified  {1}  {2}.{3}  ({4}, {5})", e.Group.Name, e.ChangeType, e.Child.Name, e.PropertyName, e.OldPropertyValue, e.NewPropertyValue);
            //else
            //    System.Diagnostics.Debug.WriteLine("{0} : RaiseLayoutModified  {1}  {2}", e.Group.Name, e.ChangeType, e.Child.Name);

            LayoutModified(this, e);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Updates the value of IsValid based on the validation state of all contained editors.
        /// </summary>
        private void UpdateIsValid()
        {
            // The form is valid if none of the contained editors have a validation error
            IsValid = GeneratedItems.Select(item => item.Content).OfType<BaseEdit>().All(baseEdit => !baseEdit.HasValidationError);
        }

        /// <summary>
        /// Removes the HasValidationErrorChanged handlers from all contained editors.
        /// </summary>
        private void RemoveHasValidationErrorChangedHandlers()
        {
            var hasValidationErrorDescriptor = DependencyPropertyDescriptor.FromProperty(BaseEdit.HasValidationErrorProperty, typeof(BaseEdit));
            foreach (var baseEdit in GeneratedItems.Select(item => item.Content).OfType<BaseEdit>())
            {
                hasValidationErrorDescriptor.RemoveValueChanged(baseEdit, BaseEdit_HasValidationErrorChanged);
            }
        }

        /// <summary>
        /// Disconnects the DataLayoutControl from events.
        /// </summary>
        private void Deinitialize()
        {
            RemoveHasValidationErrorChangedHandlers();

            if (_typeWrapper != null)
            {
                _typeWrapper.Dispose();
                _typeWrapper = null;
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Called when the ExternalCustomizationControl property changes.
        /// </summary>
        private void OnExternalCustomizationControlChanged()
        {
            // Attempt to get the controller
            var dataLayoutControlController = Controller as DataLayoutControlControllerEx;
            if (dataLayoutControlController == null)
                return;

            // Store the ExternalCustomizationControl on the controller
            dataLayoutControlController.ExternalCustomizationControl = ExternalCustomizationControl;
        }

        /// <summary>
        /// Handles the SourceUpdated event on all EditValue bindings.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Binding_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            // Flag the form as modified
            IsModified = true;
        }

        /// <summary>
        /// Handles the Validate event on all contained BaseEdits.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        /// <param name="wrapper">The wrapper which contains this editor.</param>
        private void BaseEdit_Validate(object sender, ValidationEventArgs e, EditorWrapperBase wrapper)
        {
            // Validate the value and display any error
            var errorString = wrapper.Validate(e.Value, CurrentItem);
            if (!string.IsNullOrWhiteSpace(errorString))
                e.SetError(errorString);
        }

        /// <summary>
        /// Handles the HasValidationErrorChanged event on all contained BaseEdits.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void BaseEdit_HasValidationErrorChanged(object sender, System.EventArgs e)
        {
            // If the validation error changes on one of the contained editors, then we need to refresh the IsValid flag
            UpdateIsValid();
        }

        /// <summary>
        /// Handles the Closed event for the parent window.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Window_Closed(object sender, System.EventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("Window_Closed");

            var window = (Window)sender;
            window.Closed -= Window_Closed;

            Deinitialize();
        }

        /// <summary>
        /// Handles the WidgetClosed event for the parent widget.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Widget_WidgetClosed(object sender, System.EventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("Widget_WidgetClosed");

            var widget = (IWidgetEvents)sender;
            widget.WidgetClosed -= Widget_WidgetClosed;

            Deinitialize();
        }

        #endregion


        #region Overrides

        protected override void OnAutoGeneratingItem(Type valueType, IEdmPropertyInfo property, ref DataLayoutItem item)
        {
            base.OnAutoGeneratingItem(valueType, property, ref item);

            // Make sure we have an EditorGeneratorTemplateSelector
            if (EditorGeneratorTemplateSelector == null)
                return;

            // Get the layout item as a DataLayoutItemEx
            var layoutItem = item as DataLayoutItemEx;
            if (layoutItem == null)
                return;

            // Attempt to find the wrapper that matches the property this editor is being generated for
            DataLayoutItemWrapper wrapper;
            _itemWrappers.TryGetValue(property.Name, out wrapper);
            if (wrapper == null)
                return;

            // Select the appropriate DataTemplate from EditorGeneratorTemplateSelector
            var dataTemplate = EditorGeneratorTemplateSelector.SelectTemplate(wrapper, this);
            if (dataTemplate == null)
                return;

            // Load the content from the DataTemplate
            var templateItem = dataTemplate.LoadContent() as DataLayoutItemEx;
            if (templateItem == null)
                return;

            // Copy values from the wrapper to the DataLayoutItem
            layoutItem.Name = wrapper.FieldName;
            layoutItem.EditorWrapper = wrapper;
            layoutItem.IsRequired = wrapper.IsRequired;
            layoutItem.IsReadOnly = wrapper.IsReadOnly;

            // Copy properties from the DataLayoutItem within the DataTemplate to the auto-generated DataLayoutItem
            layoutItem.HorizontalAlignment = templateItem.HorizontalAlignment;
            layoutItem.VerticalAlignment = templateItem.VerticalAlignment;
            layoutItem.MinHeight = templateItem.MinHeight;
            layoutItem.MaxHeight = templateItem.MaxHeight;
            layoutItem.MinWidth = templateItem.MinWidth;
            layoutItem.MaxWidth = templateItem.MaxWidth;

            // Disconnect the content from the DataLayoutItem within the DataTemplate
            var oldContent = item.Content as FrameworkElement;
            var newContent = templateItem.Content;
            templateItem.Content = null;

            // Connect the content to the auto-generated DataLayoutItem
            layoutItem.Content = newContent;

            // Find the element within the new content called "PART_Editor"
            // This is the element we want to bind the EditValue to
            var editor = LogicalTreeHelper.FindLogicalNode(newContent, "PART_Editor");

            if (editor != null)
            {
                // Determine if we should force a two way binding based on whether the target property is writable
                var fieldProperty = CurrentItemType.GetProperty(wrapper.PropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var forceTwoWay = fieldProperty != null && fieldProperty.SetMethod != null && fieldProperty.SetMethod.IsPublic;

                // If the target object and property are both DataObjectBase then the property value will return as an XPCollection instead
                // So we append a ! to the path to make the binding connect to the contents instead of the collection
                // See https://www.devexpress.com/Support/Center/Question/Details/A2783
                var appendPath = ((typeof(DataObjectBase).IsAssignableFrom(CurrentItemType)) && (typeof(DataObjectBase).IsAssignableFrom(fieldProperty.PropertyType)) ? "!" : null);

                // Populate the EditValueConverterParameter with special values if it contains an EditorConverterParameter value
                var editValueConverterParameter = templateItem.EditValueConverterParameter;
                if (editValueConverterParameter is EditorConverterParameter)
                {
                    switch ((EditorConverterParameter)editValueConverterParameter)
                    {
                        case EditorConverterParameter.CurrentItem:
                            editValueConverterParameter = CurrentItem;
                            break;

                        case EditorConverterParameter.FieldValue:
                            editValueConverterParameter = fieldProperty.GetValue(CurrentItem);
                            break;
                    }
                }

                // Copy the EditValue binding from the oldContent to the newContent
                var editValueDependencyProperty = DependencyPropertyDescriptor.FromName(templateItem.EditValuePropertyName, editor.GetType(), editor.GetType());
                if (editValueDependencyProperty != null)
                    BindingHelper.CopyBinding(oldContent, BaseEdit.EditValueProperty, editor as FrameworkElement, editValueDependencyProperty.DependencyProperty, templateItem.EditValueConverter, editValueConverterParameter, forceTwoWay, false, true, appendPath);
            }

            // Attach UIElement events
            var uiElement = editor as UIElement;
            if (uiElement != null)
                uiElement.AddHandler(Binding.SourceUpdatedEvent, new EventHandler<DataTransferEventArgs>(Binding_SourceUpdated));

            // Attach BaseEdit events
            var hasValidationErrorDescriptor = DependencyPropertyDescriptor.FromProperty(BaseEdit.HasValidationErrorProperty, typeof(BaseEdit));
            var baseEdit = editor as BaseEdit;
            if (baseEdit != null)
            {
                baseEdit.Validate += (s, e) => BaseEdit_Validate(s, e, wrapper);
                hasValidationErrorDescriptor.AddValueChanged(baseEdit, BaseEdit_HasValidationErrorChanged);

                // If the form is not in Edit mode, allow the focus to leave invalid editors (otherwise an invalid form can't be cancelled)
                if (EditMode != DetailEditMode.Edit)
                    baseEdit.InvalidValueBehavior = InvalidValueBehavior.AllowLeaveEditor;
            }

            // Update Label details
            layoutItem.LabelPosition = wrapper.LabelPosition;

            if (wrapper.IsLabelVisible)
                layoutItem.Label = wrapper.DisplayName;
            else
                layoutItem.Label = string.Empty;

            // If a fixed width has been specified, apply it
            if (wrapper.Width.IsNumber())
            {
                layoutItem.MinWidth = wrapper.Width;
                layoutItem.MaxWidth = wrapper.Width;
            }

            // If the editor is hidden, add it to the AvailableItems collection instead of to the layout
            if (!wrapper.IsVisible)
            {
                AvailableItems.Add(layoutItem);
                item = null;
            }
        }

        protected override IEnumerable<IEdmPropertyInfo> GetCurrentItemTypeVisibleProperties()
        {
            // Get all visible properties for the entity being displayed
            var properties = CurrentItemType.GetVisibleAndAliasedProperties(LayoutType.DataForm).OrderBy(v => v.Property.Attributes.Order ?? int.MaxValue).ToList();

            // The CurrentItem has changed, so clear any AvailableItems left over from the old item
            AvailableItems.Clear();

            // Create a data layout control wrapper for the entity type
            if (_typeWrapper != null)
                _typeWrapper.Dispose();
            _typeWrapper = new DataLayoutControlWrapper(this);

            // Create data layout item wrappers for each visible property on the entity being displayed
            _itemWrappers.Clear();
            foreach (var visiblePropertyWrapper in properties)
            {
                _itemWrappers.Add(visiblePropertyWrapper.Property.Name, new DataLayoutItemWrapper(visiblePropertyWrapper));
            }

            // Call the EditorMetadataBuilder to allow extended editor customisation
            EditorMetadataBuilder.Build(CurrentItem, this, _itemWrappers.Values, _typeWrapper);

            return properties.Select(v => v.Property).ToList();
        }

        protected override void OnAutoGeneratedGroup(LayoutGroup group)
        {
            base.OnAutoGeneratedGroup(group);

            // If the group is a tabbed group...
            if (group.View == LayoutGroupView.Tabs)
            {
                // Force it to fit the available height
                group.VerticalAlignment = VerticalAlignment.Stretch;

                // If the TabbedGroup contains a Group named "Tracking", move the Tracking group to become the last child
                // HACK : This should be handled by metadata, but we don't have any way to append base class metadata to the existing layout groups
                var trackingGroup = group.Children.OfType<LayoutGroupEx>().FirstOrDefault(g => (g.Header as string) == "Tracking");
                if (trackingGroup != null)
                {
                    group.Children.Remove(trackingGroup);
                    group.Children.Add(trackingGroup);
                }
            }

            // Abort if the group does not have a header
            if (group.Header == null || string.IsNullOrWhiteSpace(group.Header.ToString()))
                return;

            // Generate a unique name for the group
            var name = string.Format("_{0}_{1}_", CurrentItem.GetType().FullName, group.Header).ToControlName();
            group.Name = name;

            // Unregister the name if it is already registered
            if (FindName(name) != null)
                UnregisterName(name);

            // Register the new name
            RegisterName(name, group);
        }

        protected override DataLayoutItem CreateItem()
        {
            return new DataLayoutItemEx();
        }

        public override LayoutGroup CreateGroup()
        {
            return new LayoutGroupEx();
        }

        protected override Type GetGroupType()
        {
            return typeof(LayoutGroupEx);
        }

        protected override FrameworkElement FindByXMLID(string id)
        {
            return FindElementByXmlId(id);
        }

        protected override void WriteCustomizablePropertiesToXML(XmlWriter xml)
        {
            base.WriteCustomizablePropertiesToXML(xml);

            xml.WriteAttributeString("AddColonToItemLabels", AddColonToItemLabels.ToString());
            xml.WriteAttributeString("AllowItemMovingDuringCustomization", AllowItemMovingDuringCustomization.ToString());
            xml.WriteAttributeString("AllowItemSizingDuringCustomization", AllowItemSizingDuringCustomization.ToString());
            xml.WriteAttributeString("AllowItemRenamingDuringCustomization", AllowItemRenamingDuringCustomization.ToString());
            xml.WriteAttributeString("ShowHeader", ShowHeader.ToString());
        }

        protected override void ReadCustomizablePropertiesFromXML(XmlReader xml)
        {
            base.ReadCustomizablePropertiesFromXML(xml);

            AddColonToItemLabels = xml.ReadAttributeAsBoolean("AddColonToItemLabels", true);
            AllowItemMovingDuringCustomization = xml.ReadAttributeAsBoolean("AllowItemMovingDuringCustomization", true);
            AllowItemSizingDuringCustomization = xml.ReadAttributeAsBoolean("AllowItemSizingDuringCustomization", true);
            AllowItemRenamingDuringCustomization = xml.ReadAttributeAsBoolean("AllowItemRenamingDuringCustomization", true);
            ShowHeader = xml.ReadAttributeAsBoolean("ShowHeader", true);
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();

            // If this DataLayoutControl is within a widget, handle the WidgetClosed event to clean up
            var widgetElement = LayoutHelper.FindLayoutOrVisualParentObject(this, d => d is FrameworkElement && ((FrameworkElement)d).DataContext is IWidgetEvents) as FrameworkElement;
            if (widgetElement != null)
            {
                var widget = widgetElement.DataContext as IWidgetEvents;
                if (widget != null)
                    widget.WidgetClosed += Widget_WidgetClosed;
            }
            else
            {
                // If this DataLayoutControl is within a window, handle the Closed event to clean up
                var window = LayoutHelper.FindParentObject<Window>(this);
                if (window != null)
                {
                    window.Closed += Window_Closed;
                }
            }
        }

        protected override PanelControllerBase CreateController()
        {
            //System.Diagnostics.Debug.WriteLine("CreateController");

            return new DataLayoutControlControllerEx(this) { ExternalCustomizationControl = ExternalCustomizationControl };
        }

        protected override void OnCurrentItemChanged(object oldValue, object newValue)
        {
            RemoveHasValidationErrorChangedHandlers();

            // Clear the AvailableItems when the CurrentItem is set to null
            if (CurrentItem == null)
                AvailableItems.Clear();

            // If the CurrentItem has changed, then the layout is about to be reloaded, and we don't want that to flag the document as modified
            _layoutUpdating = true;

            base.OnCurrentItemChanged(oldValue, newValue);
        }

        protected override void OnLayoutUpdated()
        {
            base.OnLayoutUpdated();

            // If the layout was being updated then the update will be complete now, so we can start watching for layout modifications again
            if (_layoutUpdating)
                _layoutUpdating = false;
        }

        protected override void OnChildAdded(FrameworkElement child)
        {
            base.OnChildAdded(child);

            // Abort if the layout is being updated
            if (_layoutUpdating)
                return;

            // Notify that the layout has changed
            RaiseLayoutModified(new LayoutModifiedEventArgs(this, LayoutModifiedEventArgs.LayoutChangeTypes.Add, child));
        }

        protected override void OnChildRemoved(FrameworkElement child)
        {
            base.OnChildRemoved(child);

            // Abort if the layout is being updated
            if (_layoutUpdating)
                return;

            // Notify that the layout has changed
            RaiseLayoutModified(new LayoutModifiedEventArgs(this, LayoutModifiedEventArgs.LayoutChangeTypes.Remove, child));
        }

        protected override void OnChildPropertyChanged(FrameworkElement child, DependencyProperty propertyListener, object oldValue, object newValue)
        {
            base.OnChildPropertyChanged(child, propertyListener, oldValue, newValue);

            // Abort if the layout is being updated
            if (_layoutUpdating)
                return;

            // Ignore visibility changes
            if (propertyListener.Name == "ChildVisibilityListener")
                return;

            // Notify that the layout has changed
            RaiseLayoutModified(new LayoutModifiedEventArgs(this, LayoutModifiedEventArgs.LayoutChangeTypes.PropertyChange, child, propertyListener, oldValue, newValue));
        }

        protected override void OnRequestBringIntoView(RequestBringIntoViewEventArgs e)
        {
            // We don't want to scroll some large controls into view because it will move to the top of the control when we are trying to edit further down
            if (e.TargetObject is ScrollViewer)
                return;

            // Execute the default behaviour for all other controls
            base.OnRequestBringIntoView(e);
        }

        #endregion


        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged<T>(Expression<Func<T>> expression)
        {
            var changedEventHandler = PropertyChanged;
            if (changedEventHandler == null)
                return;
            changedEventHandler(this, new PropertyChangedEventArgs(BindableBase.GetPropertyName(expression)));
        }

        #endregion
    }
}
