using System.IO;
using System.Windows.Input;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Core.Serialization;
using DevExpress.Xpf.Grid;
using TIG.TotalLink.Client.Editor.Behavior;

namespace TIG.TotalLink.Client.Editor.Control
{
    public class GridControlEx : GridControl
    {
        //#region Private Fields

        //private Dictionary<string, GridColumnReplacement> _columnReplacements;

        //#endregion


        #region Public Methods

        /// <summary>
        /// Gets the grid layout.
        /// </summary>
        /// <returns>A Stream containing the grid layout.</returns>
        public Stream GetLayout()
        {
            // Save the layout to a stream
            var layout = new MemoryStream();
            SaveLayoutToStream(layout);
            layout.Seek(0, SeekOrigin.Begin);

            // Return the stream
            return layout;
        }

        /// <summary>
        /// Sets the grid layout.
        /// </summary>
        /// <param name="layout">A Stream containing the grid layout to apply.</param>
        public void SetLayout(Stream layout)
        {
            // Abort if no layout was supplied
            if (layout == null)
                return;

            // Restore the layout
            RestoreLayoutFromStream(layout);
            layout.Dispose();
        }

        #endregion


        #region Overrides

        //protected override void OnItemsSourceChanged(object oldValue, object newValue)
        //{
        //    // If the ItemsSource is a WcfInstantFeedbackSourceEx, store the column replacements
        //    var instantFeedbackSource = newValue as WcfInstantFeedbackSourceEx;
        //    if (instantFeedbackSource != null)
        //        _columnReplacements = instantFeedbackSource.ColumnReplacements;

        //    //if (instantFeedbackSource != null)
        //        //instantFeedbackSource.FixedFilterCriteria = CriteriaOperator.Parse("State.Country.Oid = ?", new Guid("04D5CFE3-C2C1-4305-BBF8-373BD2B1C74B"));
        //    //FilterCriteria = CriteriaOperator.Parse("State.Country.Oid = ?", new Guid("04D5CFE3-C2C1-4305-BBF8-373BD2B1C74B"));

        //    base.OnItemsSourceChanged(oldValue, newValue);
        //}

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // If an editor is active, we need to decide if the keypress should be processed by that editor instead of the grid
            if (View.ActiveEditor != null)
            {
                // Attempt to find a child element with a GridEditStrategy property attached
                var editor = LayoutHelper.FindElement(View.ActiveEditor, el => GridEditStrategyBehavior.GetGridEditStrategy(el) != null);
                if (editor != null)
                {
                    // Attempt to get the GridEditStrategy from the editor
                    var gridEditStrategy = GridEditStrategyBehavior.GetGridEditStrategy(editor);
                    if (gridEditStrategy != null)
                    {
                        // Allow the GridEditStrategy to process the key press
                        gridEditStrategy.ProcessPreviewKeyDown(editor, e);
                    }
                }
            }

            base.OnPreviewKeyDown(e);
        }

        //protected override FilterColumn GetFilterColumnFromGridColumn(ColumnBase column)
        //{
        //    // Allow the base to create a filter column
        //    var filterColumn = base.GetFilterColumnFromGridColumn(column);
        //    if (filterColumn == null)
        //        return null;

        //    // If there is a column replacement for this field, tell the filter editor to use the type of the replacement property
        //    GridColumnReplacement columnReplacement;
        //    if (_columnReplacements.TryGetValue(filterColumn.FieldName, out columnReplacement))
        //        filterColumn.ColumnType = columnReplacement.SortProperty.PropertyType;

        //    // Return the filter column
        //    return filterColumn;
        //}

        protected override bool OnDeserializeAllowProperty(AllowPropertyEventArgs e)
        {
            // Add these additional properties to the serialized layout
            if (e.DependencyProperty == SelectionModeProperty)
                return true;

            return base.OnDeserializeAllowProperty(e);
        }

        #endregion
    }
}
