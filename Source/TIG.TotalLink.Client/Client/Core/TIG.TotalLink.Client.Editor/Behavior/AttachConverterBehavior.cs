using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using DevExpress.Mvvm.UI.Interactivity;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Editor.Helper;

namespace TIG.TotalLink.Client.Editor.Behavior
{
    /// <summary>
    /// Replaces the converter on a property binding with one selected from the mappings based on the target type.
    /// </summary>
    public class AttachConverterBehavior : Behavior<FrameworkElement>
    {
        #region Dependency Properties

        public static readonly DependencyProperty PropertyNameProperty = DependencyProperty.RegisterAttached(
            "PropertyName", typeof(string), typeof(AttachConverterBehavior));
        public static readonly DependencyProperty TargetTypeProperty = DependencyProperty.RegisterAttached(
            "TargetType", typeof(Type), typeof(AttachConverterBehavior));
        public static readonly DependencyProperty MappingsProperty = DependencyProperty.RegisterAttached(
            "Mappings", typeof(List<ConverterMapping>), typeof(AttachConverterBehavior), new PropertyMetadata(new List<ConverterMapping>()));

        /// <summary>
        /// The name of the property whose binding will be altered.
        /// </summary>
        public string PropertyName
        {
            get { return (string)GetValue(PropertyNameProperty); }
            set { SetValue(PropertyNameProperty, value); }
        }

        /// <summary>
        /// The target type for the property value.
        /// </summary>
        public Type TargetType
        {
            get { return (Type)GetValue(TargetTypeProperty); }
            set { SetValue(TargetTypeProperty, value); }
        }

        /// <summary>
        /// A list of mappings that define which converter should be used based on the source value type.
        /// </summary>
        public List<ConverterMapping> Mappings
        {
            get { return (List<ConverterMapping>)GetValue(MappingsProperty); }
            set { SetValue(MappingsProperty, value); }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Returns a mapping which matches the specified type.
        /// </summary>
        /// <param name="type">The type to match.</param>
        /// <returns>A ConverterMapping which matches the specified type.</returns>
        private ConverterMapping GetMappingForType(Type type)
        {
            foreach (var mapping in Mappings)
            {
                // Attempt to match the type directly
                if (mapping.Type == type)
                    return mapping;

                // Attempt to match with the nullable version of the type
                try
                {
                    var nullableMappingType = mapping.Type.GetNullableType();
                    if (nullableMappingType == type)
                        return mapping;
                }
                catch (Exception)
                {
                    // Ignore exceptions
                }
            }

            // No match was found, so return null
            return null;
        }

        #endregion


        #region Overrides

        protected override void OnAttached()
        {
            base.OnAttached();

            // Attempt to match a mapping, and abort if none was found
            var mapping = GetMappingForType(TargetType);
            if (mapping == null)
                return;

            // Replace the target property binding with a copy which includes the additional converter
            var propertyDescriptor = DependencyPropertyDescriptor.FromName(PropertyName, AssociatedObject.GetType(), AssociatedObject.GetType());
            BindingHelper.CopyBinding(AssociatedObject, propertyDescriptor.DependencyProperty, AssociatedObject, propertyDescriptor.DependencyProperty, mapping.Converter, mapping.ConverterParameter);
        }

        #endregion
    }
}
