using System;
using System.Windows;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Helper
{
    public class ConverterMapping : DependencyObject
    {
        #region Dependency Properties

        public static readonly DependencyProperty TypeProperty = DependencyProperty.RegisterAttached(
            "Type", typeof(Type), typeof(ConverterMapping));
        public static readonly DependencyProperty ConverterProperty = DependencyProperty.RegisterAttached(
            "Converter", typeof(IValueConverter), typeof(ConverterMapping));
        public static readonly DependencyProperty ConverterParameterProperty = DependencyProperty.RegisterAttached(
            "ConverterParameter", typeof(object), typeof(ConverterMapping));
        
        /// <summary>
        /// The type to assign this converter to.
        /// This will also match the nullable version of this type.
        /// </summary>
        public Type Type
        {
            get { return (Type)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        /// <summary>
        /// The converter to apply when the type is matched.
        /// </summary>
        public IValueConverter Converter
        {
            get { return (IValueConverter)GetValue(ConverterProperty); }
            set { SetValue(ConverterProperty, value); }
        }

        /// <summary>
        /// The parameter value to pass to the converter.
        /// </summary>
        public object ConverterParameter
        {
            get { return GetValue(ConverterParameterProperty); }
            set { SetValue(ConverterParameterProperty, value); }
        }

        #endregion
    }
}
