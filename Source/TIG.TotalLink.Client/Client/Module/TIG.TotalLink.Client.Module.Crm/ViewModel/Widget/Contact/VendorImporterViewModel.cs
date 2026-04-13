using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Crm.Uploader;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Enum.CRM;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact
{
    public class VendorImporterViewModel : ImporterViewModelBase<VendorUploaderDataModel>
    {
        #region Private Fields

        private readonly ICrmFacade _crmFacade;
        private UnitOfWork _unitOfWork;

        #endregion


        #region Constructors

        public VendorImporterViewModel(ICrmFacade crmFacade)
            : this()
        {
            _crmFacade = crmFacade;
        }

        public VendorImporterViewModel()
        {
            FirstHeaderCellReference = "A2";

            Fields.AddRange(new SpreadsheetFieldBase[]
            {
                CreateNamedField(p => p.LegacyReference, SpreadsheetDataType.String, "Vendor No.", "VendorNo.", "VendorNo", "Legacy Reference", "LegacyReference"),
                CreateNamedField(p => p.CompanyName, SpreadsheetDataType.String, "Company Name", "CompanyName"),
                CreateNamedField(p => p.SearchName, SpreadsheetDataType.String, "Search Name", "SearchName"),
                CreateConstantField(p => p.BranchType, ConvertToBranchType, "Branch"),
                CreateNamedField(p => p.OwnershipType, SpreadsheetDataType.String, "Ownership Type", "OwnershipType"),
                CreateNamedField(p => p.VendorPostingGroup, SpreadsheetDataType.String, ConvertToPostingGroup, "Vendor Posting Group", "VendorPostingGroup"),
                CreateNamedField(p => p.GeneralBusinessPostingGroup, SpreadsheetDataType.String, ConvertToPostingGroup, "Gen. Bus. Posting Group", "GeneralBusinessPostingGroup"),
                CreateNamedField(p => p.GSTBusinessPostingGroup, SpreadsheetDataType.String, ConvertToPostingGroup, "VAT Bus. Posting Group", "VATBusinessPostingGroup", "GST Bus. Posting Group", "GSTBusinessPostingGroup"),
                CreateNamedField(p => p.Currency, SpreadsheetDataType.String, ConvertToCurrency, "Currency", "Currency Code", "CurrencyCode"),
                CreateNamedField(p => p.PaymentTerms, SpreadsheetDataType.String, ConvertToPaymentTerms, "Payment Terms Code", "PaymentTermsCode"),
                CreateNamedField(p => p.CashFlowPaymentTerms, SpreadsheetDataType.String, ConvertToPaymentTerms, "Cash Flow Payment Terms Code", "CashFlowPaymentTermsCode"),
                CreateNamedField(p => p.ShipmentMethod, SpreadsheetDataType.String, ConvertToShipmentMethod, "Shipment Method Code", "ShipmentMethodCode"),
                CreateNamedField(p => p.PaymentMethod, SpreadsheetDataType.String, ConvertToPaymentMethod, "Payment Method Code", "PaymentMethodCode", "PaymentMethod"),
                CreateNamedField(p => p.Email, SpreadsheetDataType.String, RemoveSpaces, "E-Mail", "Email"),
                CreateNamedField(p => p.HomePage, SpreadsheetDataType.String, RemoveSpaces, "Home Page", "HomePage"),
                CreateNamedField(p => p.ABN, SpreadsheetDataType.String, "ABN"),
                CreateNamedField(p => p.Industry, SpreadsheetDataType.String, "Industry", "Industry Type", "IndustryType"),
                CreateNamedField(p => p.IndustryClass, SpreadsheetDataType.String, "Industry Class", "IndustryClass"),
                CreateNamedField(p => p.BusinessType, SpreadsheetDataType.String, "Business Type", "BusinessType"),
                CreateNamedField(p => p.LegacySource, SpreadsheetDataType.String, "Legacy"),
                CreateNamedField(p => p.ContactTitle, SpreadsheetDataType.String, ConvertToTitle, "Title"),
                CreateNamedField(p => p.ContactGender, SpreadsheetDataType.String, ConvertToGender, "Gender"),
                CreateNamedField(p => p.ContactFirstName, SpreadsheetDataType.String, ExtractFirstName, "Contact (View)", "Contact(View)", "Contact"),
                CreateNamedField(p => p.ContactLastName, SpreadsheetDataType.String, ExtractLastName, "Contact (View)", "Contact(View)", "Contact"),
                CreateNamedField(p => p.ContactRole, SpreadsheetDataType.String, "Contact Role", "ContactRole"),
                CreateNamedField(p => p.ContactPhone, SpreadsheetDataType.String, "Phone No.", "PhoneNo.", "PhoneNo", "Phone"),
                CreateNamedField(p => p.ContactFax, SpreadsheetDataType.String, "Fax No.", "Fax No", "FaxNo.", "FaxNo", "Fax"),
                CreateFieldGroup(new GroupableSpreadsheetFieldDefinitionBase[]
                {
                    CreateNamedField(p => p.AddressType, SpreadsheetDataType.String, ConvertToAddressType, "Address Type {0}", "AddressType{0}"),
                    CreateNamedField(p => p.Address1, SpreadsheetDataType.String, ConvertAddressLine, "Address {0}-1", "Address{0}-1"),
                    CreateNamedField(p => p.Address2, SpreadsheetDataType.String, ConvertAddressLine, "Address {0}-2", "Address{0}-2"),
                    CreateNamedField(p => p.City, SpreadsheetDataType.String, "City {0}", "City{0}"),
                    CreateNamedField(p => p.Suburb, SpreadsheetDataType.String, "Suburb {0}", "Suburb{0}"),
                    CreateNamedField(p => p.Country, SpreadsheetDataType.String, ConvertToCountry, "Country Code {0}", "CountryCode{0}", "Country/Region Code {0}", "Country/RegionCode{0}"),
                    CreateNamedField(p => p.State, SpreadsheetDataType.String, ConvertToState, "State Code {0}", "StateCode{0}"),
                    CreateNamedField(p => p.Postcode, SpreadsheetDataType.String, ConvertToPostcode, "Post Code {0}", "PostCode{0}")
                })
            });
        }

        #endregion


        #region Private Methods

        ///<summary>
        /// Remove all spaces from a string.
        ///</summary>
        ///<param name="value">The value to convert (string).</param>
        ///<param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        ///<returns>The value as a string without spaces.</returns>
        private object RemoveSpaces(object value, Dictionary<string, object> currentRow)
        {
            var stringValue = value as string;
            return stringValue == null ? null : stringValue.Replace(" ", string.Empty);
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
        /// Converts a string spreadsheet value to a Currency object.
        ///</summary>
        ///<param name="value">The value to convert (string).</param>
        ///<param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        ///<returns>The value as a Currency object.</returns>
        private object ConvertToCurrency(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the Currency in the database
            var currency = _unitOfWork.Query<Currency>().FirstOrDefault(p => p.Code == stringValue);
            if (currency != null)
                return currency;

            // Error if no Currency was found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Currency record.");
        }

        ///<summary>
        /// Converts a string spreadsheet value to a PaymentTerms object.
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
            var paymentTerms = _unitOfWork.Query<PaymentTerms>().FirstOrDefault(p => p.Code == stringValue || p.Code == stringValue.ReversePerWords());
            if (paymentTerms != null)
                return paymentTerms;

            // Error if no PaymentTerms were found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Payment Terms record.");
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
        /// Converts a string spreadsheet value to a Title object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Title object.</returns>
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
            throw new SpreadsheetFieldException("Value must be the Code of an existing Title record.");
        }

        /// <summary>
        /// Converts a numeric spreadsheet value to a Gender enum.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Gender enum.</returns>
        private object ConvertToGender(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Attempt to parse the string as a Gender
            var gender = EnumHelper.Parse<Gender>(stringValue);
            if (gender.HasValue)
                return gender.Value;

            // Error if no Gender was found
            throw new SpreadsheetFieldException("Value must be the Name of one of the pre-defined Genders.");
        }

        /// <summary>
        /// Extracts the first name from string containing multiple names.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The first name from the string.</returns>
        private object ExtractFirstName(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Split the value at spaces and return the first part, if there is at least one part
            var nameParts = stringValue.Split(' ');
            if (nameParts.Length > 0)
                return nameParts[0];

            // If the value didn't have any parts, return null
            return null;
        }

        /// <summary>
        /// Extracts the last name from string containing multiple names.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The last name from the string.</returns>
        private object ExtractLastName(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Split the value at spaces and return the last part, is there is at least two parts
            var nameParts = stringValue.Split(' ');
            if (nameParts.Length > 1)
                return nameParts[nameParts.Length - 1];

            // If the value didn't have more that one part, return null
            return null;
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

            switch (stringValue)
            {
                case "Billing Address":
                    stringValue = "Billing";
                    break;

                case "Ship To":
                    stringValue = "Shipping";
                    break;
            }

            // Attempt to find the AddressType in the database
            var addressType = _unitOfWork.Query<AddressType>().FirstOrDefault(a => a.Name == stringValue);
            if (addressType != null)
                return addressType;

            // Error if no AddressType was found
            throw new SpreadsheetFieldException("Value must be the Name of an existing Address Type record.");
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
