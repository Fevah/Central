using System;
using System.Reflection;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Definition.Interface;
namespace TIG.TotalLink.Client.Editor.Definition
{
    public class PopupGridEditorDefinition : GridEditorDefinition, IAliasedEditorDefinition
    {
        #region Private Fields

        private string _actualDisplayMember;
        private PropertyInfo _actualDisplayProperty;
        private Type _actualDisplayType;

        #endregion


        #region Public Properties

        /// <summary>
        /// Popup grid don't need showToolBar property, so set it to false always.
        /// </summary>
        public new bool ShowToolBar
        {
            get { return false; }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Updates the ActualDisplayMember property.
        /// </summary>
        private void UpdateActualDisplayMember()
        {
            if (Wrapper == null)
                return;

            ActualDisplayMember = "Count";
            ActualDisplayProperty = Wrapper.Property.PropertyType.GetProperty(ActualDisplayMember, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            ActualDisplayType = ActualDisplayProperty != null ? ActualDisplayProperty.PropertyType : null;
        }

        #endregion


        #region Overrides

        /// <summary>
        /// The parent EditorWrapperBase that contains this EditorDefinitionBase.
        /// </summary>
        public override EditorWrapperBase Wrapper
        {
            get { return base.Wrapper; }
            set
            {
                var wrapper = base.Wrapper;
                SetProperty(ref wrapper, value, (string)null, () =>
                {
                    base.Wrapper = value;
                    UpdateActualDisplayMember();
                });
            }
        }
        
        #endregion


        #region IAliasedEditorDefinition

        /// <summary>
        /// The actual field on the entity that will be used for display and filtering.
        /// </summary>
        public string ActualDisplayMember
        {
            get { return _actualDisplayMember; }
            private set { SetProperty(ref _actualDisplayMember, value, () => ActualDisplayMember); }
        }

        /// <summary>
        /// The actual property on the entity that will be used for display and filtering.
        /// </summary>
        public PropertyInfo ActualDisplayProperty
        {
            get { return _actualDisplayProperty; }
            private set { SetProperty(ref _actualDisplayProperty, value, () => ActualDisplayProperty); }
        }

        /// <summary>
        /// The type of value that is contained in the ActualDisplayMember.
        /// </summary>
        public Type ActualDisplayType
        {
            get { return _actualDisplayType; }
            private set { SetProperty(ref _actualDisplayType, value, () => ActualDisplayType); }
        }

        #endregion
    }
}
