using System;
using System.Reflection;
using TIG.TotalLink.Client.Editor.Definition.Interface;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class LookUpEditorDefinition : GridEditorDefinition, IAliasedEditorDefinition
    {
        #region Private Fields

        private string _displayMember;
        private string _actualDisplayMember;
        private PropertyInfo _actualDisplayProperty;
        private Type _actualDisplayType;
        //private int _maxVisibleItems = 10;

        #endregion


        #region Public Properties


        /// <summary>
        /// Overrides the default field on the lookup entity that will be used for display and filtering.
        /// </summary>
        public string DisplayMember
        {
            get { return _displayMember; }
            set { SetProperty(ref _displayMember, value, () => DisplayMember, UpdateActualDisplayMember); }
        }

        ///// <summary>
        ///// The maximum number of items to show in the popup grid.
        ///// </summary>
        //public int MaxVisibleItems
        //{
        //    get { return _maxVisibleItems; }
        //    set { SetProperty(ref _maxVisibleItems, value, () => MaxVisibleItems); }
        //}

        #endregion


        #region Private Methods

        /// <summary>
        /// Updates the ActualDisplayMember property.
        /// </summary>
        private void UpdateActualDisplayMember()
        {
            ActualDisplayMember = GetActualDisplayMember();
            ActualDisplayProperty = EntityType.GetProperty(ActualDisplayMember, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            ActualDisplayType = ActualDisplayProperty != null ? ActualDisplayProperty.PropertyType : null;
        }

        /// <summary>
        /// Returns the correct ActualDisplayMember based on the DisplayMember and DisplayFieldAttribute.
        /// </summary>
        /// <returns>An ActualDisplayMember based on the DisplayMember and DisplayFieldAttribute.</returns>
        private string GetActualDisplayMember()
        {
            // If a DisplayMember has been set, use that as the ActualDisplayMember
            if (!string.IsNullOrWhiteSpace(DisplayMember))
                return DisplayMember;

            // If the entity has a DisplayFieldAttribute, use that as the ActualDisplayMember
            var displayFieldAttribute = EntityType.GetCustomAttribute<DisplayFieldAttribute>(true);
            if (displayFieldAttribute != null)
                return displayFieldAttribute.FieldName;

            // If both the DisplayMember and DisplayFieldAttribute have not been set, return "Oid"
            return "Oid";
        }

        #endregion


        #region Overrides

        public override Type EntityType
        {
            get { return base.EntityType; }
            set
            {
                base.EntityType = value;
                UpdateActualDisplayMember();
            }
        }

        #endregion


        #region IAliasedEditorDefinition

        /// <summary>
        /// The actual field on the lookup entity that will be used for display and filtering.
        /// If DisplayMember is empty, this will return the field specified by the DisplayFieldAttribute on the related entity.
        /// Otherwise it will return DisplayMember.
        /// </summary>
        public string ActualDisplayMember
        {
            get { return _actualDisplayMember; }
            private set { SetProperty(ref _actualDisplayMember, value, () => ActualDisplayMember); }
        }
        
        /// <summary>
        /// The actual property on the lookup entity that will be used for display and filtering.
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
