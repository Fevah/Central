using System.Windows;
using System.Windows.Data;
using DevExpress.Utils;
using DevExpress.Xpf.Grid;

namespace TIG.TotalLink.Client.Editor.Control
{
    public class GridColumnEx : GridColumn
    {
        #region Constructors

        public GridColumnEx()
        {
            // Apply default bindings
            SetBindingInternal(NameProperty, "FieldName", false);
            SetBindingInternal(FieldNameProperty, "FieldName", false);
            SetBindingInternal(HeaderProperty, "DisplayName", false);
            SetBindingInternal(VisibleProperty, "IsVisible");
            SetBindingInternal(SortIndexProperty, "SortIndex");
            SetBindingInternal(SortOrderProperty, "SortOrder");
            SetBindingInternal(SortModeProperty, "SortMode");
            SetBindingInternal(GroupIndexProperty, "GroupIndex");
            SetBindingInternal(FixedWidthProperty, "FixedWidth");
            SetBindingInternal(WidthProperty, "Width");
            
            // Apply default configuration
            AllowEditing = DefaultBoolean.True;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Applies a property binding using a consistent format required by our GridColumn bindings.
        /// </summary>
        /// <param name="dp">The property to bind to.</param>
        /// <param name="fieldName">The name of the field to bind to.</param>
        /// <param name="twoWay">Indicates if the binding should be created in TwoWay mode.</param>
        /// <returns>Records the conditions of the binding.</returns>
        private BindingExpressionBase SetBindingInternal(DependencyProperty dp, string fieldName, bool twoWay = true)
        {
            var binding = new Binding()
            {
                Path = new PropertyPath(string.Format("(0).{0}", fieldName), DataContextProperty),
                RelativeSource = new RelativeSource(RelativeSourceMode.Self)
            };
            if (twoWay)
            {
                binding.Mode = BindingMode.TwoWay;
                binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            }

            return SetBinding(dp, binding);
        }

        #endregion
    }
}
