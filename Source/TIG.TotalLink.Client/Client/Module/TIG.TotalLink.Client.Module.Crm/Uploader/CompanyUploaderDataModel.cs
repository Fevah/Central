using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Uploader.Core;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Crm;

namespace TIG.TotalLink.Client.Module.Crm.Uploader
{
    public class CompanyUploaderDataModel : UploaderDataModelBase
    {
        #region Private Fields

        private string _legacyReference;
        private string _ownershipType;
        private string _industry;
        private string _industryClass;
        private string _email;
        private string _homePage;
        private string _chainName;
        private string _companyName;
        private string _companySearchName;
        private string _branchName;
        private string _homePhone;
        private string _fax;
        private string _phone;
        private string _phone2;
        private string _mobilePhone;
        private string _contactGroup;
        private int _numberOfStores;
        private int _numberOfEmployees;
        private decimal _creditLimit;
        private PaymentTerms _paymentTerms;
        private PaymentMethod _paymentMethod;
        private string _abn;
        private string _branchNumber;
        private string _businessType;
        private PostingGroup _customerPostingGroup;
        private PostingGroup _generalBusinessPostingGroup;
        private PostingGroup _gstBusinessPostingGroup;
        private ShipmentMethod _shipmentMethod;
        private AddressType _addressType;
        private string _address1;
        private string _address2;
        private string _city;
        private string _suburb;
        private Postcode _postcode;
        private State _state;
        private Country _country;
        private BranchType _branchType;
        private string _externalReference;

        #endregion


        #region Public Properties

        public string ExternalReference
        {
            get { return _externalReference; }
            set { SetProperty(ref _externalReference, value, () => ExternalReference); }
        }

        public string LegacyReference
        {
            get { return _legacyReference; }
            set { SetProperty(ref _legacyReference, value, () => LegacyReference); }
        }

        public string ChainName
        {
            get { return _chainName; }
            set { SetProperty(ref _chainName, value, () => ChainName); }
        }

        public string CompanyName
        {
            get { return _companyName; }
            set { SetProperty(ref _companyName, value, () => CompanyName); }
        }

        public string CompanySearchName
        {
            get { return _companySearchName; }
            set { SetProperty(ref _companySearchName, value, () => CompanySearchName); }
        }

        public string BranchName
        {
            get { return _branchName; }
            set { SetProperty(ref _branchName, value, () => BranchName); }
        }

        public BranchType BranchType
        {
            get { return _branchType; }
            set { SetProperty(ref _branchType, value, () => BranchType); }
        }

        public string BranchNumber
        {
            get { return _branchNumber; }
            set { SetProperty(ref _branchNumber, value, () => BranchNumber); }
        }

        public string ABN
        {
            get { return _abn; }
            set { SetProperty(ref _abn, value, () => ABN); }
        }

        public string OwnershipType
        {
            get { return _ownershipType; }
            set { SetProperty(ref _ownershipType, value, () => OwnershipType); }
        }

        public string Industry
        {
            get { return _industry; }
            set { SetProperty(ref _industry, value, () => Industry); }
        }

        public string IndustryClass
        {
            get { return _industryClass; }
            set { SetProperty(ref _industryClass, value, () => IndustryClass); }
        }

        public string Email
        {
            get { return _email; }
            set { SetProperty(ref _email, value, () => Email); }
        }

        public string HomePage
        {
            get { return _homePage; }
            set { SetProperty(ref _homePage, value, () => HomePage); }
        }

        public string HomePhone
        {
            get { return _homePhone; }
            set { SetProperty(ref _homePhone, value, () => HomePhone); }
        }

        public string Fax
        {
            get { return _fax; }
            set { SetProperty(ref _fax, value, () => Fax); }
        }

        public string Phone
        {
            get { return _phone; }
            set { SetProperty(ref _phone, value, () => Phone); }
        }

        public string Phone2
        {
            get { return _phone2; }
            set { SetProperty(ref _phone2, value, () => Phone2); }
        }

        public string MobilePhone
        {
            get { return _mobilePhone; }
            set { SetProperty(ref _mobilePhone, value, () => MobilePhone); }
        }

        public string ContactGroup
        {
            get { return _contactGroup; }
            set { SetProperty(ref _contactGroup, value, () => ContactGroup); }
        }

        public int NumberOfStores
        {
            get { return _numberOfStores; }
            set { SetProperty(ref _numberOfStores, value, () => NumberOfStores); }
        }

        public int NumberOfEmployees
        {
            get { return _numberOfEmployees; }
            set { SetProperty(ref _numberOfEmployees, value, () => NumberOfEmployees); }
        }

        public decimal CreditLimit
        {
            get { return _creditLimit; }
            set { SetProperty(ref _creditLimit, value, () => CreditLimit); }
        }

        public PaymentTerms PaymentTerms
        {
            get { return _paymentTerms; }
            set { SetProperty(ref _paymentTerms, value, () => PaymentTerms); }
        }

        public PaymentMethod PaymentMethod
        {
            get { return _paymentMethod; }
            set { SetProperty(ref _paymentMethod, value, () => PaymentMethod); }
        }

        public string BusinessType
        {
            get { return _businessType; }
            set { SetProperty(ref _businessType, value, () => BusinessType); }
        }

        public PostingGroup CustomerPostingGroup
        {
            get { return _customerPostingGroup; }
            set { SetProperty(ref _customerPostingGroup, value, () => CustomerPostingGroup); }
        }

        public PostingGroup GeneralBusinessPostingGroup
        {
            get { return _generalBusinessPostingGroup; }
            set { SetProperty(ref _generalBusinessPostingGroup, value, () => GeneralBusinessPostingGroup); }
        }

        public PostingGroup GSTBusinessPostingGroup
        {
            get { return _gstBusinessPostingGroup; }
            set { SetProperty(ref _gstBusinessPostingGroup, value, () => GSTBusinessPostingGroup); }
        }
        public ShipmentMethod ShipmentMethod
        {
            get { return _shipmentMethod; }
            set { SetProperty(ref _shipmentMethod, value, () => ShipmentMethod); }
        }

        public AddressType AddressType
        {
            get { return _addressType; }
            set { SetProperty(ref _addressType, value, () => AddressType); }
        }

        public string Address1
        {
            get { return _address1; }
            set { SetProperty(ref _address1, value, () => Address1); }
        }

        public string Address2
        {
            get { return _address2; }
            set { SetProperty(ref _address2, value, () => Address2); }
        }

        public string City
        {
            get { return _city; }
            set { SetProperty(ref _city, value, () => City); }
        }

        public string Suburb
        {
            get { return _suburb; }
            set { SetProperty(ref _suburb, value, () => Suburb); }
        }

        public Postcode Postcode
        {
            get { return _postcode; }
            set { SetProperty(ref _postcode, value, () => Postcode); }
        }

        public State State
        {
            get { return _state; }
            set { SetProperty(ref _state, value, () => State); }
        }

        public Country Country
        {
            get { return _country; }
            set { SetProperty(ref _country, value, () => Country); }
        }

        #endregion


        #region Metadata

        ///<summary>
        /// Builds metadata for properties.
        ///</summary>
        ///<param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<CompanyUploaderDataModel> builder)
        {
            builder.Property(p => p.LegacyReference)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.ExternalReference)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.OwnershipType)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.Industry)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.IndustryClass)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.Email)
                .ReadOnly()
                .MaxLength(255);
            builder.Property(p => p.HomePage)
                .ReadOnly()
                .MaxLength(255);
            builder.Property(p => p.ChainName)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.CompanyName)
                .ReadOnly()
                .Required()
                .MaxLength(100);
            builder.Property(p => p.CompanySearchName)
                .ReadOnly()
                .Required()
                .MaxLength(255);
            builder.Property(p => p.BranchName)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.HomePhone)
                .ReadOnly()
                .MaxLength(30);
            builder.Property(p => p.Fax)
                .ReadOnly()
                .MaxLength(30);
            builder.Property(p => p.Phone)
                .ReadOnly()
                .MaxLength(30);
            builder.Property(p => p.Phone2)
                .ReadOnly()
                .MaxLength(30);
            builder.Property(p => p.MobilePhone)
                .ReadOnly()
                .MaxLength(30);
            builder.Property(p => p.ContactGroup)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.NumberOfStores)
                .ReadOnly();
            builder.Property(p => p.NumberOfEmployees)
                .ReadOnly();
            builder.Property(p => p.CreditLimit)
                .ReadOnly();
            builder.Property(p => p.PaymentTerms)
                .ReadOnly();
            builder.Property(p => p.PaymentMethod)
                .ReadOnly();
            builder.Property(p => p.ABN)
                .ReadOnly();
            builder.Property(p => p.BranchNumber)
                .ReadOnly();
            builder.Property(p => p.BusinessType)
                .ReadOnly();
            builder.Property(p => p.CustomerPostingGroup)
                .ReadOnly();
            builder.Property(p => p.GeneralBusinessPostingGroup)
                .ReadOnly();
            builder.Property(p => p.GSTBusinessPostingGroup)
                .ReadOnly();
            builder.Property(p => p.AddressType)
                .ReadOnly();
            builder.Property(p => p.Address1)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.Address2)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.City)
                .MaxLength(100)
                .ReadOnly();
            builder.Property(p => p.Suburb)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.Postcode)
                .ReadOnly();
            builder.Property(p => p.State)
                .ReadOnly();
            builder.Property(p => p.Country)
                .ReadOnly();
        }

        ///<summary>
        ///Builds metadata for editors.
        ///</summary>
        ///<param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<CompanyUploaderDataModel> builder)
        {
            builder.Property(p => p.Email).ReplaceEditor(new HyperLinkEditorDefinition() { AllowUrls = false });
            builder.Property(p => p.HomePage).ReplaceEditor(new HyperLinkEditorDefinition() { AllowEmails = false });
            builder.Property(p => p.CreditLimit).ReplaceEditor(new CurrencyEditorDefinition());

            builder.Property(p => p.Postcode)
                .GetEditor<LookUpEditorDefinition>().DisplayMember = "Code";
        }

        #endregion
    }
}
