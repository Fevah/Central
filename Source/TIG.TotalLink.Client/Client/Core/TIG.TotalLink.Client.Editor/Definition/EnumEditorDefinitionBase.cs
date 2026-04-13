using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Media;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Definition.Helper;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public abstract class EnumEditorDefinitionBase : EditorDefinitionBase
    {
        #region Public Enums

        public enum DisplayModes
        {
            TextOnly,
            ImageAndText,
            ImageOnly
        }

        #endregion


        #region Private Fields

        private readonly Dictionary<object, EnumEditorItem> _items;
        private DisplayModes _displayMode = DisplayModes.TextOnly;

        #endregion


        #region Constructors

        protected EnumEditorDefinitionBase(Type enumType)
        {
            EnumType = enumType;

            // Populate the ItemsSource
            _items = new Dictionary<object, EnumEditorItem>();

            // Process all values in the enum type
            foreach (var value in System.Enum.GetValues(enumType))
            {
                // Get the name of the value
                var valueName = System.Enum.GetName(enumType, value);
                if (string.IsNullOrWhiteSpace(valueName))
                    continue;

                // Prepare default values
                var text = valueName.AddSpaces();
                string imageUri = null;
                string toolTip = null;
                var valueField = enumType.GetField(valueName, BindingFlags.Static | BindingFlags.Public);

                // Attempt to get a DescriptionAttribute for the value
                var descriptionAttribute = valueField.GetCustomAttribute<DescriptionAttribute>();
                if (descriptionAttribute != null)
                {
                    // If a description is found, but it is empty, skip this item
                    if (string.IsNullOrWhiteSpace(descriptionAttribute.Description))
                        continue;

                    // If a description is found, and it is not empty, use it as the display text
                    text = descriptionAttribute.Description;
                }

                // Attempt to get an EnumImageAttribute for the value
                // If one is found, use it as the imageUri
                var imageAttribute = valueField.GetCustomAttribute<EnumImageAttribute>();
                if (imageAttribute != null && !string.IsNullOrWhiteSpace(imageAttribute.ImageUri))
                    imageUri = imageAttribute.ImageUri;

                // Attempt to get an EnumTooltipAttribute for the value
                // If one is found, use it as the tooltip
                var toolTipAttribute = valueField.GetCustomAttribute<EnumToolTipAttribute>();
                if (toolTipAttribute != null && !string.IsNullOrWhiteSpace(toolTipAttribute.ToolTip))
                    toolTip = toolTipAttribute.ToolTip;

                // Add a new item
                _items.Add(value, new EnumEditorItem(text, imageUri, toolTip));
            }

            ItemsSource = _items;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The enum type that the possible options will be collected from.
        /// </summary>
        public Type EnumType { get; private set; }

        /// <summary>
        /// A list containing all of the possible options.
        /// </summary>
        public IEnumerable ItemsSource { get; private set; }

        /// <summary>
        /// The DisplayMode of the editor.
        /// Defaults to TextOnly.
        /// </summary>
        public DisplayModes DisplayMode
        {
            get { return _displayMode; }
            set
            {
                SetProperty(ref _displayMode, value, () => DisplayMode, () =>
                    RaisePropertiesChanged(() => IsImageVisible, () => IsTextVisible)
                );
            }
        }

        /// <summary>
        /// Indicates if the image is visible, based on the DisplayMode.
        /// </summary>
        public bool IsImageVisible
        {
            get { return (DisplayMode == DisplayModes.ImageAndText || DisplayMode == DisplayModes.ImageOnly); }
        }

        /// <summary>
        /// Indicates if the text is visible, based on the DisplayMode.
        /// </summary>
        public bool IsTextVisible
        {
            get { return (DisplayMode == DisplayModes.ImageAndText || DisplayMode == DisplayModes.TextOnly); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Returns the display text for the supplied value.
        /// </summary>
        /// <param name="value">The value to return the text for.</param>
        /// <returns>The display text for the supplied value, or null if the value doesn't exist in the enum.</returns>
        public string GetText(object value)
        {
            EnumEditorItem item;
            return (_items.TryGetValue(value, out item) ? item.Text : null);
        }

        /// <summary>
        /// Returns the image for the supplied value.
        /// </summary>
        /// <param name="value">The value to return the image for.</param>
        /// <returns>The image for the supplied value, or null if the value doesn't exist in the enum.</returns>
        public ImageSource GetImage(object value)
        {
            EnumEditorItem item;
            return (_items.TryGetValue(value, out item) ? item.Image : null);
        }

        /// <summary>
        /// Returns the tooltip for the supplied value.
        /// </summary>
        /// <param name="value">The value to return the tooltip for.</param>
        /// <returns>The tooltip for the supplied value, or null if the value doesn't exist in the enum.</returns>
        public string GetToolTip(object value)
        {
            EnumEditorItem item;
            return (_items.TryGetValue(value, out item) ? item.ToolTip : null);
        }

        #endregion
    }
}
