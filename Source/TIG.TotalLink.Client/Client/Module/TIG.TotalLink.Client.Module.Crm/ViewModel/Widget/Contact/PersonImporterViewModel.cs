using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Crm.Uploader;
using TIG.TotalLink.Shared.DataModel.Core.Enum.CRM;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact
{
    public class PersonImporterViewModel : ImporterViewModelBase<PersonUploaderDataModel>
    {
        #region Private Fields

        private readonly ICrmFacade _crmFacade;
        private UnitOfWork _unitOfWork;

        #endregion


        #region Constructors

        public PersonImporterViewModel(ICrmFacade crmFacade)
            : this()
        {
            _crmFacade = crmFacade;
        }

        public PersonImporterViewModel()
        {
            FirstHeaderCellReference = "A6";

            Fields.AddRange(new SpreadsheetFieldBase[]
            {
                CreateFixedField(p => p.Company, SpreadsheetDataType.String, "C2"),
                CreateNamedField(p => p.Title, SpreadsheetDataType.String, ConvertToTitle, "Title"),
                CreateNamedField(p => p.FirstName, SpreadsheetDataType.String, "First Name", "FirstName"),
                CreateNamedField(p => p.LastName, SpreadsheetDataType.String, "Last Name", "LastName"),
                CreateNamedField(p => p.Gender, SpreadsheetDataType.String, ConvertToGender, "Gender"),
                CreateNamedField(p => p.ExternalReference, SpreadsheetDataType.String,  "Employee No", "EmployeeNo"),
                CreateNamedField(p => p.StaffRole, SpreadsheetDataType.String, "Staff Role", "StaffRole"),
                CreateNamedField(p => p.SubsidyValue, SpreadsheetDataType.Numeric, ConvertToDecimal, "SubsidyValue", "Subsidy Value"),
                CreateNamedField(p => p.SubsidyName, SpreadsheetDataType.String, "Subsidy Name","SubsidyName"),
                CreateNamedField(p => p.Branch, SpreadsheetDataType.String, ConvertToBranch, "Branch", "Branch Name"),
                CreateNamedField(p => p.Email, SpreadsheetDataType.String, "E-Mail", "Email"),
                CreateNamedField(p => p.BusinessPhone, SpreadsheetDataType.String, "Work Phone", "WorkPhone"),
                CreateNamedField(p => p.HomePhone, SpreadsheetDataType.String, "Home Phone", "HomePhone"),
                CreateNamedField(p => p.Fax, SpreadsheetDataType.String, "Fax No.", "Fax No", "FaxNo.", "FaxNo", "Fax"),
                CreateNamedField(p => p.Mobile, SpreadsheetDataType.String, "Mobile Phone", "MobilePhone", "Mobile")
            });
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Converts a numeric spreadsheet value to a Decimal.
        /// </summary>
        /// <param name="value">The value to convert (double).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Decimal.</returns>
        private object ConvertToDecimal(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is null
            if (value == null)
                return null;

            // Return the value as a Decimal
            return Convert.ToDecimal(value);
        }

        ///<summary>
        ///Converts a string spreadsheet value to a Title object.
        ///</summary>
        ///<param name="value">The value to convert (string).</param>
        ///<param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        ///<returns>The value as a Title object.</returns>
        private object ConvertToTitle(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the Title in the database
            var title = _unitOfWork.Query<Title>().FirstOrDefault(p => p.Name == stringValue);
            if (title != null)
                return title;

            // Error if no Title was found
            throw new SpreadsheetFieldException("Value must be the Name of an existing Title record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a Gender object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Gender object.</returns>
        private object ConvertToGender(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Return the correct Gender value based on the stringValue
            stringValue = stringValue.ToUpper();
            switch (stringValue)
            {
                case "M":
                case "MALE":
                    return Gender.Male;

                case "F":
                case "FEMALE":
                    return Gender.Female;
            }

            return null;
        }

        /// <summary>
        /// Converts a string spreadsheet value to a Branch object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as an Branch object.</returns>
        private object ConvertToBranch(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            var company = currentRow["Company"] as string;

            // Attempt to find the branch in the database
            var branches = string.IsNullOrEmpty(company) ? _unitOfWork.Query<Branch>().Where(p => p.FirstName == stringValue).ToList()
                 : _unitOfWork.Query<Branch>().Where(p => p.FirstName == stringValue && p.SourceContactLinks.Any(t => t.Target.FirstName == company)).ToList();

            if (branches.Count == 1)
                return branches[0];

            // Error if multiple branches were found
            if (branches.Count > 1)
                throw new SpreadsheetFieldException(string.Format("Multiple branches found with a Name of '{0}'.", stringValue));

            // Error if no branch was found
            throw new SpreadsheetFieldException(string.Format("Value must be the Name of an existing Branch record, and has a Name matching '{0}'.", stringValue));
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the CrmFacade
                ConnectToFacade(_crmFacade);
                if (_crmFacade != null)
                    _unitOfWork = _crmFacade.CreateUnitOfWork();
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
