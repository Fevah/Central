using System.Windows;
using TIG.TotalLink.Client.Editor.Core;

namespace TIG.TotalLink.Client.Editor.Behavior
{
    /// <summary>
    /// Apply this behaviour to an editor to allow it to use the specified GridEditStrategy to override key processing.
    /// This should only be used on editors within a GridControlEx.
    /// </summary>
    public class GridEditStrategyBehavior
    {
        #region Dependency Properties

        /// <summary>
        /// The GridEditStrategy to use.
        /// </summary>
        public static readonly DependencyProperty GridEditStrategyProperty = DependencyProperty.RegisterAttached(
            "GridEditStrategy", typeof(GridEditStrategyBase), typeof(GridEditStrategyBehavior));

        /// <summary>
        /// Gets the GridEditStrategy property.
        /// </summary>
        /// <param name="dp">The DependencyObject that the property will be collected from.</param>
        /// <returns>The value of the GridEditStrategy property.</returns>
        public static GridEditStrategyBase GetGridEditStrategy(DependencyObject dp)
        {
            return (GridEditStrategyBase)dp.GetValue(GridEditStrategyProperty);
        }

        /// <summary>
        /// Sets the GridEditStrategy property.
        /// </summary>
        /// <param name="dp">The DependencyObject that the property will be applied to.</param>
        /// <param name="value">The new value for the property.</param>
        public static void SetGridEditStrategy(DependencyObject dp, GridEditStrategyBase value)
        {
            dp.SetValue(GridEditStrategyProperty, value);
        }

        #endregion
    }
}
