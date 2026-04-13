using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DevExpress.Data;
using DevExpress.Xpf.LayoutControl;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;

namespace TIG.TotalLink.Client.Editor.Builder
{
    public class PropertyMetadataBuilder<T, TProperty> : EditorMetadataBuilderBase<T>
        where T : class
    {
        #region Private Fields

        private readonly Expression<Func<T, TProperty>> _propertyExpression;
        private readonly string _propertyName;
        private EditorWrapperBase _editorWrapper;
        private IEnumerable<EditorWrapperBase> _editorWrappers; 

        #endregion


        #region Constructors

        public PropertyMetadataBuilder(Expression<Func<T, TProperty>> propertyExpression, EditorMetadataBuilderBase<T> parent)
            : base(parent)
        {
            _propertyExpression = propertyExpression;

            // Get the property name
            var memberExpression = (MemberExpression)propertyExpression.Body;
            var property = memberExpression.Member as PropertyInfo;
            if (property == null)
                throw new ArgumentException("propertyExpression");
            _propertyName = property.Name;
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// The wrapper that applies to this property.
        /// </summary>
        private EditorWrapperBase EditorWrapper
        {
            get { return _editorWrapper ?? (_editorWrapper = EditorWrappers.SingleOrDefault(w => w.PropertyName == _propertyName)); }
        }

        /// <summary>
        /// A list of wrappers that will be searched for this property.
        /// </summary>
        private IEnumerable<EditorWrapperBase> EditorWrappers
        {
            get { return _editorWrappers ?? (_editorWrappers = GetEditorWrappers()); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Returns the wrapper for this property.
        /// </summary>
        /// <returns>The wrapper for this property.</returns>
        public EditorWrapperBase GetEditorWrapper()
        {
            return EditorWrapper;
        }

        /// <summary>
        /// Returns the wrapper for this property.
        /// </summary>
        /// <typeparam name="TEditorWrapper">The type of editor wrapper to return.</typeparam>
        /// <returns>The wrapper for this property, cast to TWrapper.</returns>
        public TEditorWrapper GetEditorWrapper<TEditorWrapper>()
            where TEditorWrapper : EditorWrapperBase
        {
            return EditorWrapper as TEditorWrapper;
        }

        /// <summary>
        /// Returns the editor for this property.
        /// </summary>
        /// <returns>The editor for this property.</returns>
        public EditorDefinitionBase GetEditor()
        {
            if (EditorWrapper == null)
                return null;

            return EditorWrapper.Editor;
        }

        /// <summary>
        /// Returns the editor for this property.
        /// </summary>
        /// <typeparam name="TEditor">The type of editor definition to return.</typeparam>
        /// <returns>The editor for this property, cast to TEditor.</returns>
        public TEditor GetEditor<TEditor>()
            where TEditor : EditorDefinitionBase
        {
            if (EditorWrapper == null)
                return null;

            return EditorWrapper.Editor as TEditor;
        }

        /// <summary>
        /// Replaces the editor for this property.
        /// </summary>
        /// <param name="editor">The new editor to use for the property.</param>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> ReplaceEditor(EditorDefinitionBase editor)
        {
            if (EditorWrapper != null)
                EditorWrapper.Editor = editor;

            return this;
        }

        /// <summary>
        /// Changes the display name for this editor.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> DisplayName(string name)
        {
            if (EditorWrapper != null)
                EditorWrapper.DisplayName = name;

            return this;
        }

        /// <summary>
        /// Hides the editor for this property.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> Hidden()
        {
            if (EditorWrapper != null)
                EditorWrapper.IsVisible = false;

            return this;
        }

        /// <summary>
        /// Makes the editor for this property visible.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> Visible()
        {
            if (EditorWrapper != null)
                EditorWrapper.IsVisible = true;

            return this;
        }

        /// <summary>
        /// Makes the editor for this property read-only.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> ReadOnly()
        {
            if (EditorWrapper != null)
                EditorWrapper.IsReadOnly = true;

            return this;
        }

        /// <summary>
        /// Makes the editor for this property not read-only.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> NotReadOnly()
        {
            if (EditorWrapper != null)
                EditorWrapper.IsReadOnly = false;

            return this;
        }

        /// <summary>
        /// Makes the editor allow null values.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> AllowNull()
        {
            if (EditorWrapper != null)
                EditorWrapper.AllowNull = true;

            return this;
        }

        /// <summary>
        /// Makes the editor not allow null values.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> DontAllowNull()
        {
            if (EditorWrapper != null)
                EditorWrapper.AllowNull = false;

            return this;
        }

        /// <summary>
        /// Shows the label for this property.
        /// Only affects editors displayed in a DataLayoutControl.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> ShowLabel()
        {
            var dataLayoutItemWrapper = GetEditorWrapper<DataLayoutItemWrapper>();
            if (dataLayoutItemWrapper != null)
            {
                dataLayoutItemWrapper.IsLabelVisible = true;
            }

            return this;
        }

        /// <summary>
        /// Shows the label for this property.
        /// Only affects editors displayed in a DataLayoutControl.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> HideLabel()
        {
            var dataLayoutItemWrapper = GetEditorWrapper<DataLayoutItemWrapper>();
            if (dataLayoutItemWrapper != null)
            {
                dataLayoutItemWrapper.IsLabelVisible = false;
            }

            return this;
        }
        
        /// <summary>
        /// Repositions the label for this property.
        /// Only affects editors displayed in a DataLayoutControl.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> LabelPosition(LayoutItemLabelPosition position)
        {
            var dataLayoutItemWrapper = GetEditorWrapper<DataLayoutItemWrapper>();
            if (dataLayoutItemWrapper != null)
            {
                dataLayoutItemWrapper.LabelPosition = position;
            }

            return this;
        }

        /// <summary>
        /// Makes the column for this property have a fixed width.
        /// Only affects editors displayed in a Grid.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> FixedWidth()
        {
            var gridColumnWrapper = GetEditorWrapper<GridColumnWrapperBase>();
            if (gridColumnWrapper != null)
            {
                gridColumnWrapper.FixedWidth = true;
            }

            return this;
        }

        /// <summary>
        /// Makes the column for this property not have a fixed width.
        /// Only affects editors displayed in a Grid.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> NotFixedWidth()
        {
            var gridColumnWrapper = GetEditorWrapper<GridColumnWrapperBase>();
            if (gridColumnWrapper != null)
            {
                gridColumnWrapper.FixedWidth = false;
            }

            return this;
        }

        /// <summary>
        /// Sets the width of columns for this property.
        /// Only affects editors displayed in a Grid.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> ColumnWidth(double width)
        {
            var gridColumnWrapper = GetEditorWrapper<GridColumnWrapperBase>();
            if (gridColumnWrapper != null)
            {
                gridColumnWrapper.Width = width;
            }

            return this;
        }

        /// <summary>
        /// Sets the width of controls for this property.
        /// Only affects editors displayed in a DataLayoutControl.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> ControlWidth(double width)
        {
            var dataLayoutItemWrapper = GetEditorWrapper<DataLayoutItemWrapper>();
            if (dataLayoutItemWrapper != null)
            {
                dataLayoutItemWrapper.Width = width;
            }

            return this;
        }

        /// <summary>
        /// Configures this property to allow an unlimited length string.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> UnlimitedLength()
        {
            var wrapper = GetEditorWrapper();
            if (wrapper != null)
            {
                wrapper.MaxLength = 0;
            }

            return this;
        }

        /// <summary>
        /// Adds a validation method to this editor.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> AddValidation(Func<object, object, string> validationMethod)
        {
            if (EditorWrapper != null)
                EditorWrapper.AddValidation(validationMethod);

            return this;
        }

        /// <summary>
        /// Applies a sort for this property.
        /// Only affects editors displayed in a Grid.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> Sort(int sortIndex, ColumnSortOrder sortOrder)
        {
            var gridColumnWrapper = GetEditorWrapper<GridColumnWrapperBase>();
            if (gridColumnWrapper != null)
            {
                gridColumnWrapper.SortIndex = sortIndex;
                gridColumnWrapper.SortOrder = sortOrder;
            }

            return this;
        }

        /// <summary>
        /// Applies grouping for this property.
        /// Only affects editors displayed in a Grid.
        /// </summary>
        /// <returns>A PropertyMetadataBuilder to continue building with.</returns>
        public PropertyMetadataBuilder<T, TProperty> Group(int groupIndex)
        {
            var gridColumnWrapper = GetEditorWrapper<GridColumnWrapperBase>();
            if (gridColumnWrapper != null)
            {
                gridColumnWrapper.GroupIndex = groupIndex;
            }

            return this;
        }

        /// <summary>
        /// Ends the property metadata.
        /// </summary>
        /// <returns>The parent metadata builder.</returns>
        public RootMetadataBuilderBase<T> EndProperty()
        {
            return _parent as RootMetadataBuilderBase<T>;
        }

        #endregion
    }
}
