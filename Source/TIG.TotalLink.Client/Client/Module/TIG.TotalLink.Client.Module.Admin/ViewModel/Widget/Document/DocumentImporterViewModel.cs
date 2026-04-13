using System.Collections.Generic;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;
using TIG.TotalLink.Client.Module.Admin.Uploader.Document;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document
{
    public class DocumentImporterViewModel : ImporterViewModelBase<DocumentUploaderDataModel>
    {
        #region Constructors

        public DocumentImporterViewModel()
        {
            FirstHeaderCellReference = "A1";

            Fields.AddRange(new SpreadsheetFieldBase[]
            {
                CreateNamedField(p => p.Category, SpreadsheetDataType.String, "Category"),
                CreateNamedField(p => p.Page, SpreadsheetDataType.String, "Page"),
                CreateNamedField(p => p.Group, SpreadsheetDataType.String, "Group"),
                CreateNamedField(p => p.Item, SpreadsheetDataType.String, "Item"),
                CreateNamedField(p => p.Description, SpreadsheetDataType.String, "Description"),
                CreateNamedField(p => p.Document, SpreadsheetDataType.String, "Document"),
                CreateNamedField(p => p.DocumentView, SpreadsheetDataType.String, ConvertToDocumentView, "Document View", "DocumentView"),
                CreateNamedField(p => p.DocumentAction, SpreadsheetDataType.String, "Document Action", "DocumentAction"),
                CreateFieldGroup(new GroupableSpreadsheetFieldDefinitionBase[]
                {
                    CreateNamedField(p => p.WidgetName, SpreadsheetDataType.String, "Widget{0} Name"),
                    CreateNamedField(p => p.WidgetView, SpreadsheetDataType.String, "Widget{0} View"),
                    CreateNamedField(p => p.WidgetGroup, SpreadsheetDataType.String, "Widget{0} Group")
                }),
            });
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Converts a numeric spreadsheet value to a DocumentView enum.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a DocumentView enum.</returns>
        private object ConvertToDocumentView(object value, Dictionary<string, object> currentRow)
        {
            // If the value is empty, default to Flat
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return DocumentView.Flat;

            // Attempt to parse the string as a DocumentView
            var documentView = EnumHelper.Parse<DocumentView>(stringValue);
            if (documentView.HasValue)
                return documentView.Value;

            // Error if no DocumentView was found
            throw new SpreadsheetFieldException("Value must be the Name of one of the pre-defined DocumentViews, or left blank to default to Flat.");
        }

        #endregion
    }
}
