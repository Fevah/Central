using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Crm.Uploader;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact
{
    public class CompanyImporterViewModel : ImporterViewModelBase<CompanyUploaderDataModel>
    {
        #region Private Fields

        private readonly ICrmFacade _crmFacade;
        private UnitOfWork _unitOfWork;

        #endregion


        #region Constructors

        public CompanyImporterViewModel(ICrmFacade crmFacade)
            : this()
        {
            _crmFacade = crmFacade;
        }

        public CompanyImporterViewModel()
        {
            FirstHeaderCellReference = "A2";

            Fields.AddRange(new SpreadsheetFieldBase[]
            {
                CreateNamedField(p => p.LegacyReference, SpreadsheetDataType.String, "Customer No.", "CustomerNo.", "Customer No","CustomerNo", "Legacy Reference", "LegacyReference"),
                CreateNamedField(p => p.ChainName, SpreadsheetDataType.String, "Chain Name", "ChainName"),
                CreateNamedField(p => p.CompanyName, SpreadsheetDataType.String, "Company Name", "CompanyName"),
                CreateNamedField(p => p.CompanySearchName, SpreadsheetDataType.String, "Company Search Name", "CompanySearchName"),
                CreateNamedField(p => p.BranchName, SpreadsheetDataType.String, "Branch"),
                CreateNamedField(p => p.ExternalReference,SpreadsheetDataType.String, "Branch ID", "BranchID"),
                CreateConstantField(p => p.BranchType, ConvertToBranchType, "Branch"),
                CreateNamedField(p => p.OwnershipType, SpreadsheetDataType.String, "Ownership Type", "OwnershipType"),
                CreateNamedField(p => p.Industry, SpreadsheetDataType.String, "Industry"),
                CreateNamedField(p => p.IndustryClass, SpreadsheetDataType.String, "Industry Class", "IndustryClass"),
                CreateNamedField(p => p.Email, SpreadsheetDataType.String, "E-Mail", "Email"),
                CreateNamedField(p => p.HomePage, SpreadsheetDataType.String, "Home Page", "HomePage"),
                CreateNamedField(p => p.HomePhone, SpreadsheetDataType.String, "Home Phone", "HomePhone"),
                CreateNamedField(p => p.Fax, SpreadsheetDataType.String, "Fax No.", "Fax No", "FaxNo.", "FaxNo", "Fax"),
                CreateNamedField(p => p.Phone, SpreadsheetDataType.String, "Phone No.", "PhoneNo.", "PhoneNo", "Phone"),
                CreateNamedField(p => p.Phone2, SpreadsheetDataType.String, "Phone 2", "Phone2"),
                CreateNamedField(p => p.MobilePhone, SpreadsheetDataType.String, "Mobile Phone", "MobilePhone"),
                CreateNamedField(p => p.ContactGroup, SpreadsheetDataType.String, "Customer Group", "CustomerGroup"),
                CreateNamedField(p => p.NumberOfStores, SpreadsheetDataType.Numeric, ConvertToInt32, "Number Of Stores", "NumberOfStores"),
                CreateNamedField(p => p.NumberOfEmployees, SpreadsheetDataType.Numeric, ConvertToInt32, "Number Of Employees", "NumberOfEmployees"),
                CreateNamedField(p => p.CreditLimit, SpreadsheetDataType.Numeric, ConvertToDecimal, "Credit Limit (LCY)", "CreditLimit(LCY)", "Credit Limit", "CreditLimit"),
                CreateNamedField(p => p.PaymentTerms, SpreadsheetDataType.String, ConvertToPaymentTerms, "Payment Terms Code", "PaymentTermsCode"),
                CreateNamedField(p => p.PaymentMethod, SpreadsheetDataType.String, ConvertToPaymentMethod, "Payment Method Code", "PaymentMethodCode"),
                CreateNamedField(p => p.ABN, SpreadsheetDataType.String, "ABN"),
                CreateNamedField(p => p.BranchNumber, SpreadsheetDataType.String, "Branch ID", "BranchID", "Branch Number", "BranchNumber"),
                CreateNamedField(p => p.BusinessType, SpreadsheetDataType.String, "Business Type", "BusinessType"),
                CreateNamedField(p => p.CustomerPostingGroup, SpreadsheetDataType.String, ConvertToPostingGroup, "Customer Posting Group", "CustomerPostingGroup"),
                CreateNamedField(p => p.GeneralBusinessPostingGroup, SpreadsheetDataType.String, ConvertToPostingGroup, "Gen. Bus. Posting Group", "GeneralBusinessPostingGroup", "Gen Bus Posting Group"),
                CreateNamedField(p => p.GSTBusinessPostingGroup, SpreadsheetDataType.String, ConvertToPostingGroup, "VAT Bus. Posting Group", "VATBusinessPostingGroup", "GST Bus. Posting Group", "GSTBusinessPostingGroup", "GST Bus Posting Group"),
                CreateNamedField(p => p.ShipmentMethod, SpreadsheetDataType.String, ConvertToShipmentMethod, "Shipping Method", "ShippingMethod"),
                CreateFieldGroup(new GroupableSpreadsheetFieldDefinitionBase[]
                {
                    CreateNamedField(p => p.AddressType, SpreadsheetDataType.String, ConvertToAddressType, "Address Type {0}", "AddressType{0}"),
                    CreateNamedField(p => p.Address1, SpreadsheetDataType.String, ConvertAddressLine, "Address {0}-1", "Address{0}-1"),
                    CreateNamedField(p => p.Address2, SpreadsheetDataType.String, ConvertAddressLine, "Address {0}-2", "Address{0}-2"),
                    CreateNamedField(p => p.City, SpreadsheetDataType.String, "City {0}", "City{0}"),
                    CreateNamedField(p => p.Suburb, SpreadsheetDataType.String, "Suburb {0}", "Suburb{0}"),
                    CreateNamedField(p => p.Country, SpreadsheetDataType.String, ConvertToCountry, "Country Code {0}", "CountryCode{0}"),
                    CreateNamedField(p => p.State, SpreadsheetDataType.String, ConvertToState, "State Code {0}", "StateCode{0}"),
                    CreateNamedField(p => p.Postcode, SpreadsheetDataType.String, ConvertToPostcode, "Post Code {0}", "PostCode{0}")
                })
            });
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Converts a numeric spreadsheet value to an Int32.
        /// </summary>
        /// <param name="value">The value to convert (double).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as an Int32.</returns>
        private object ConvertToInt32(object value, Dictionary<string, object> currentRow)
        {
            if (value == null)
                return null;

            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Converts a numeric spreadsheet value to a Decimal.
        /// </summary>
        /// <param name="value">The value to convert (double).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Decimal.</returns>
        private object ConvertToDecimal(object value, Dictionary<string, object> currentRow)
        {
            if (value == null)
                return null;

            return Convert.ToDecimal(value);
        }

        ///<summary>
        ///Converts a string spreadsheet value to a PaymentTerms object.
        ///</summary>
        ///<param name="value">The value to convert (string).</param>
        ///<param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        ///<returns>The value as a PaymentTerms object.</returns>
        private object ConvertToPaymentTerms(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the PaymentTerms in the database
            var paymentTerms = _unitOfWork.Query<PaymentTerms>().FirstOrDefault(p => p.Code == stringValue);
            if (paymentTerms != null)
                return paymentTerms;

            // Error if no PaymentTerms were found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Payment Terms record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a PaymentMethod object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a PaymentMethod object.</returns>
        private object ConvertToPaymentMethod(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the PaymentMethod in the database
            var paymentMethod = _unitOfWork.Query<PaymentMethod>().FirstOrDefault(p => p.Code == stringValue);
            if (paymentMethod != null)
                return paymentMethod;

            // Error if no PaymentMethod was found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Payment Method record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a PostingGroup object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as an PostingGroup object.</returns>
        private object ConvertToPostingGroup(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Convert FREE to GSTFREE
            if (stringValue.ToUpper() == "FREE")
                stringValue = "GSTFREE";

            // Attempt to find the PostingGroup in the database
            var postingGroup = _unitOfWork.Query<PostingGroup>().FirstOrDefault(p => p.Code == stringValue);
            if (postingGroup != null)
                return postingGroup;

            // Error if no PostingGroup was found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Posting Group record.");
        }

        ///<summary>
        /// Converts a string spreadsheet value to a ShipmentMethod object.
        ///</summary>
        ///<param name="value">The value to convert (string).</param>
        ///<param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        ///<returns>The value as a ShipmentMethod object.</returns>
        private object ConvertToShipmentMethod(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the ShipmentMethod in the database
            var shipmentMethod = _unitOfWork.Query<ShipmentMethod>().FirstOrDefault(p => p.Code == stringValue);
            if (shipmentMethod != null)
                return shipmentMethod;

            // Error if no ShipmentMethod was found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Shipment Method record.");
        }

        /// <summary>
        /// Reformats an address line string.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The formatted value.</returns>
        private object ConvertAddressLine(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Return the value with leading and trailing commas and spaces removed
            return stringValue.Trim(',').Trim();
        }

        /// <summary>
        /// Converts a string spreadsheet value to a AddressType object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a AddressType object.</returns>
        private object ConvertToAddressType(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the AddressType in the database
            var addressType = _unitOfWork.Query<AddressType>().FirstOrDefault(a => a.Name == stringValue);
            if (addressType != null)
                return addressType;

            // Error if no AddressType was found
            throw new SpreadsheetFieldException("Value must be the Name of an existing Address Type record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a Country object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Country object.</returns>
        private object ConvertToCountry(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the Country in the database
            var country = _unitOfWork.Query<Country>().FirstOrDefault(p => p.Code == stringValue);
            if (country != null)
                return country;

            // Error if no Country was found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Country record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a State object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a State object.</returns>
        private object ConvertToState(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to get the parent Country
            Country country = null;
            if (currentRow.ContainsKey("Country"))
                country = currentRow["Country"] as Country;
            if (country == null)
                throw new SpreadsheetFieldException("Unable to validate State as Country is invalid.");

            // Attempt to find the State in the database
            var state = _unitOfWork.Query<State>().FirstOrDefault(p => p.Country.Oid == country.Oid && p.Code == stringValue);
            if (state != null)
                return state;

            // Error if no State was found
            throw new SpreadsheetFieldException(string.Format("Value must be the Code of an existing State record which exists within Country '{0}'.", country.Name));
        }

        /// <summary>
        /// Converts a string spreadsheet value to a Postcode object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Postcode object.</returns>
        private object ConvertToPostcode(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to get the Suburb
            string suburb = null;
            if (currentRow.ContainsKey("Suburb"))
                suburb = currentRow["Suburb"] as string;
            if (string.IsNullOrWhiteSpace(suburb))
                throw new SpreadsheetFieldException("Unable to validate Postcode as Suburb is empty.");

            // Attempt to get the parent State
            State state = null;
            if (currentRow.ContainsKey("State"))
                state = currentRow["State"] as State;
            if (state == null)
                throw new SpreadsheetFieldException("Unable to validate Postcode as State is invalid.");

            // Attempt to find the Postcode in the database
            var postcodes = _unitOfWork.Query<Postcode>().Where(p => p.State.Oid == state.Oid && p.Code == stringValue && p.Name == suburb).ToList();
            if (postcodes.Count == 1)
                return postcodes[0];

            // Error if multiple postcodes were found
            if (postcodes.Count > 1)
                throw new SpreadsheetFieldException(string.Format("Multiple Postcodes found in State '{0}' with a Code of '{1}' and a Name of '{2}'.", state.Name, stringValue, suburb));

            // Error if no Postcode was found
            throw new SpreadsheetFieldException(string.Format("Value must be the Code of an existing Postcode record which exists within State '{0}', and has a Name matching '{1}'.", state.Name, suburb));
        }

        /// <summary>
        /// Converts a string spreadsheet value to a BranchType object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a BranchType object.</returns>
        private object ConvertToBranchType(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the BranchType in the database
            var branchType = _unitOfWork.Query<BranchType>().FirstOrDefault(a => a.Name == stringValue);
            if (branchType != null)
                return branchType;

            // Error if no BranchType was found
            throw new SpreadsheetFieldException("Value must be the Name of an existing Branch Type record.");
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
