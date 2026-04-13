using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Test.Uploader;
using TIG.TotalLink.Shared.DataModel.Test;
using TIG.TotalLink.Shared.Facade.Test;

namespace TIG.TotalLink.Client.Module.Test.ViewModel.Widget
{
    public class TestObjectImporterViewModel : ImporterViewModelBase<TestObjectUploaderDataModel>
    {
        #region Private Fields

        private readonly ITestFacade _testFacade;
        private UnitOfWork _unitOfWork;

        #endregion


        #region Constructors

        public TestObjectImporterViewModel(ITestFacade testFacade)
            : this()
        {
            _testFacade = testFacade;
        }

        public TestObjectImporterViewModel()
        {
            FirstHeaderCellReference = "A1";

            Fields.AddRange(new SpreadsheetFieldDefinitionBase[]
            {
                CreateNamedField(p => p.Text, SpreadsheetDataType.String, "Text"),
                CreateNamedField(p => p.SpinInt, SpreadsheetDataType.Numeric, ConvertSpin, "Spin"),
                CreateNamedField(p => p.Checkbox, SpreadsheetDataType.Boolean, "Checkbox"),
                CreateNamedField(p => p.DateTime, SpreadsheetDataType.DateTime, "DateTime"),
                CreateNamedField(p => p.HyperLink, SpreadsheetDataType.String, "Hyperlink"),
                CreateNamedField(p => p.LookUp, SpreadsheetDataType.String, ConvertLookUp, "LookUp"),
                CreateNamedField(p => p.Currency, SpreadsheetDataType.Numeric, ConvertCurrency, "Currency"),
                CreateNumberedField(p => p.Label, SpreadsheetDataType.String, 7),
                CreateConstantField(p => p.Progress, 45)
            });
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// An example of how a converter can be used to convert a numeric spreadsheet value (which will always be returned as a double) to an int.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The converted value.</returns>
        private object ConvertSpin(object value, Dictionary<string, object> currentRow)
        {
            // If the value is null the default validations will handle it
            if (value == null)
                return null;

            // Return the double converted to an int
            return Convert.ToInt32(value);
        }

        /// <summary>
        /// An example of how a converter can be used to convert a string spreadsheet value to an entity.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The converted value.</returns>
        private object ConvertLookUp(object value, Dictionary<string, object> currentRow)
        {
            // If the value is null the default validations will handle it
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find a TestObjectLookUp whose name matches the value
            var lookup = _unitOfWork.Query<TestObjectLookUp>().FirstOrDefault(l => l.Name == stringValue);
            if (lookup == null)
                throw new SpreadsheetFieldException("Value must be the Name of an existing TestObjectLookUp.");
            
            // Return the found TestObjectLookUp
            return lookup;
        }

        /// <summary>
        /// An example of how a converter can be used to convert a numeric spreadsheet value (which will always be returned as a double) to a satisfactory currency value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The converted value.</returns>
        private object ConvertCurrency(object value, Dictionary<string, object> currentRow)
        {
            // If the value is null the default validations will handle it
            if (value == null)
                return null;

            // Convert the double to a decimal rounded to 4 decimal places
            return Math.Round(Convert.ToDecimal(value), 4);
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the TestFacade
                ConnectToFacade(_testFacade);
                if (_testFacade != null)
                    _unitOfWork = _testFacade.CreateUnitOfWork();
            });
        }

        protected override void OnWidgetClosed(EventArgs e)
        {
            base.OnWidgetClosed(e);

            // Dispose the UnitOfWork
            try
            {
                _unitOfWork.Dispose();
            }
            catch (Exception)
            {
                // Ignore dispose exceptions
            }
        }

        #endregion
    }
}
