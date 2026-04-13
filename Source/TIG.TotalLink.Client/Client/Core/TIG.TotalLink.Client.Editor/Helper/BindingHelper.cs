using System;
using System.Windows;
using System.Windows.Data;

namespace TIG.TotalLink.Client.Editor.Helper
{
    public class BindingHelper
    {
        /// <summary>
        /// Forces a binding to update the source value.
        /// </summary>
        /// <param name="element">The UIElement that contains the property to update.</param>
        /// <param name="property">The DependencyProperty to update.</param>
        public static void UpdateSource(UIElement element, DependencyProperty property)
        {
            // If no UIElement or property was supplied, then abort
            if (element == null || property == null)
                return;

            // Attempt to get the BindingExpression from the UIElement
            var binding = BindingOperations.GetBindingExpression(element, property);
            if (binding == null)
                return;

            // Force the binding to update
            binding.UpdateSource();
        }

        /// <summary>
        /// Forces a binding to update the target value.
        /// </summary>
        /// <param name="element">The UIElement that contains the property to update.</param>
        /// <param name="property">The DependencyProperty to update.</param>
        public static void UpdateTarget(UIElement element, DependencyProperty property)
        {
            // If no UIElement or property was supplied, then abort
            if (element == null || property == null)
                return;

            // Attempt to get the BindingExpression from the UIElement
            var binding = BindingOperations.GetBindingExpression(element, property);
            if (binding == null)
                return;

            // Force the binding to update
            binding.UpdateTarget();
        }

        /// <summary>
        /// Copies a binding from one FrameworkElement to another.
        /// </summary>
        /// <param name="srcElement">The source element that has the binding that will be copied.</param>
        /// <param name="srcProperty">The property binding on the source element that will be copied.</param>
        /// <param name="destElement">The destination element that the binding will be copied to.</param>
        /// <param name="destProperty">The property on the destination element that will be replaced.</param>
        /// <param name="converter">A converter to apply to the new binding.</param>
        /// <param name="converterParameter">Parameter value to be passed to the converter.</param>
        /// <param name="forceTwoWay">If set to true, the target binding will be set to 'Mode = BindingMode.TwoWay' instead of copying the Mode from the source.</param>
        /// <param name="forcePropertyChanged">If set to true, the target binding will be set to 'UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged' instead of copying the UpdateSourceTrigger from the source.</param>
        /// <param name="forceNotifyOnSourceUpdated">If set to true, the target binding will be set to 'NotifyOnSourceUpdated = true' instead of copying the NotifyOnSourceUpdated from the source.</param>
        /// <param name="appendPath">A string that should be appended to the original property path.</param>
        public static void CopyBinding(FrameworkElement srcElement, DependencyProperty srcProperty, FrameworkElement destElement, DependencyProperty destProperty, IValueConverter converter = null, object converterParameter = null, bool forceTwoWay = false, bool forcePropertyChanged = false, bool forceNotifyOnSourceUpdated = false, string appendPath = null)
        {
            // Abort if srcElement or destElement are null
            if (srcElement == null || destElement == null)
            {
                return;
            }

            // Get the binding expression from srcElement
            var srcBindingExpression = srcElement.GetBindingExpression(srcProperty);
            if (srcBindingExpression == null)
            {
                return;
            }

            // Get the srcBinding from the expression
            var srcBinding = srcBindingExpression.ParentBinding;
            BindingOperations.ClearBinding(srcElement, srcProperty);

            // Create a new destBinding as a copy of srcBinding
            var destBinding = new Binding(string.Format("{0}{1}", srcBinding.Path.Path, appendPath))
            {
                BindingGroupName = srcBinding.BindingGroupName,
                BindsDirectlyToSource = srcBinding.BindsDirectlyToSource,
                Mode = (forceTwoWay ? BindingMode.TwoWay : srcBinding.Mode),
                NotifyOnSourceUpdated = (forceNotifyOnSourceUpdated || srcBinding.NotifyOnSourceUpdated),
                NotifyOnTargetUpdated = srcBinding.NotifyOnTargetUpdated,
                NotifyOnValidationError = srcBinding.NotifyOnValidationError,
                UpdateSourceTrigger = (forcePropertyChanged ? UpdateSourceTrigger.PropertyChanged : srcBinding.UpdateSourceTrigger),
                ValidatesOnDataErrors = srcBinding.ValidatesOnDataErrors,
                ValidatesOnNotifyDataErrors = srcBinding.ValidatesOnNotifyDataErrors,
                ValidatesOnExceptions = srcBinding.ValidatesOnExceptions,
                Converter = converter ?? srcBinding.Converter,
                ConverterParameter = converterParameter ?? srcBinding.ConverterParameter,
                StringFormat = srcBinding.StringFormat
            };

            try
            {
                // Apply the destBinding to the destElement
                destElement.SetBinding(destProperty, destBinding);
            }
            catch (InvalidOperationException ex)
            {
                // If we get an InvalidOperationException then it's probably because we are trying to create a TwoWay binding on a read-only property
                // So attempt again with a OneWay binding
                destBinding = new Binding(string.Format("{0}{1}", srcBinding.Path.Path, appendPath))
                {
                    BindingGroupName = srcBinding.BindingGroupName,
                    BindsDirectlyToSource = srcBinding.BindsDirectlyToSource,
                    Mode = BindingMode.OneWay,
                    NotifyOnSourceUpdated = srcBinding.NotifyOnSourceUpdated,
                    NotifyOnTargetUpdated = srcBinding.NotifyOnTargetUpdated,
                    NotifyOnValidationError = srcBinding.NotifyOnValidationError,
                    UpdateSourceTrigger = (forcePropertyChanged ? UpdateSourceTrigger.PropertyChanged : srcBinding.UpdateSourceTrigger),
                    ValidatesOnDataErrors = srcBinding.ValidatesOnDataErrors,
                    ValidatesOnNotifyDataErrors = srcBinding.ValidatesOnNotifyDataErrors,
                    ValidatesOnExceptions = srcBinding.ValidatesOnExceptions,
                    Converter = converter ?? srcBinding.Converter,
                    ConverterParameter = converterParameter ?? srcBinding.ConverterParameter,
                    StringFormat = srcBinding.StringFormat
                };
                destElement.SetBinding(destProperty, destBinding);
            }
        }
    }
}
