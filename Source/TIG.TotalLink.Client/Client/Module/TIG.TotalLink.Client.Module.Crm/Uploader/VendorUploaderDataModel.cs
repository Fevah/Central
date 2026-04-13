using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Uploader.Core;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Enum.CRM;
using TIG.TotalLink.Shared.DataModel.Crm;

namespace TIG.TotalLink.Client.Module.Crm.Uploader
{
    public class VendorUploaderDataModel : UploaderDataModelBase
    {
        #region Private Fields

        private string _legacyReference;
        private string _companyName;
        private string _searchName;
        private string _ownershipType;
        private PostingGroup _vendorPostingGroup;
        private PostingGroup _generalBusinessPostingGroup;
        private PostingGroup _gstBusinessPostingGroup;
        private Currency _currency;
        private PaymentTerms _paymentTerms;
        private PaymentTerms _cashFlowPaymentTerms;
        private ShipmentMethod _shipmentMethod;
        private PaymentMethod _paymentMethod;
        private string _email;
        private string _homePage;
        private string _abn;
        private string _industry;
        private string _industryClass;
        private string _businessType;
        private string _legacySource;
        private AddressType _addressType;
        private string _address1;
        private string _address2;
        private string _city;
        private string _suburb;
        private Postcode _postcode;
        private State _state;
        private Country _country;
        private Title _contactTitle;
        private Gender _contactGender;
        private string _contactFirstName;
        private string _contactLastName;
        private string _contactRole;
        private string _contactPhone;
        private string _contactFax;
        private BranchType _branchType;

        #endregion


        #region Public Properties

        public string LegacyReference
        {
            get { return _legacyReference; }
            set { SetProperty(ref _legacyReference, value, () => LegacyReference); }
        }

        public string CompanyName
        {
            get { return _companyName; }
            set { SetProperty(ref _companyName, value, () => CompanyName); }
        }

        public string SearchName
        {
            get { return _searchName; }
            set { SetProperty(ref _searchName, value, () => SearchName); }
        }

        public BranchType BranchType
        {
            get { return _branchType; }
            set { SetProperty(ref _branchType, value, () => BranchType); }
        }

        public string OwnershipType
        {
            get { return _ownershipType; }
            set { SetProperty(ref _ownershipType, value, () => OwnershipType); }
        }

        public PostingGroup VendorPostingGroup
        {
            get { return _vendorPostingGroup; }
            set { SetProperty(ref _vendorPostingGroup, value, () => VendorPostingGroup); }
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

        public Currency Currency
        {
            get { return _currency; }
            set { SetProperty(ref _currency, value, () => Currency); }
        }

        public PaymentTerms PaymentTerms
        {
            get { return _paymentTerms; }
            set { SetProperty(ref _paymentTerms, value, () => PaymentTerms); }
        }

        public PaymentTerms CashFlowPaymentTerms
        {
            get { return _cashFlowPaymentTerms; }
            set { SetProperty(ref _cashFlowPaymentTerms, value, () => CashFlowPaymentTerms); }
        }

        public ShipmentMethod ShipmentMethod
        {
            get { return _shipmentMethod; }
            set { SetProperty(ref _shipmentMethod, value, () => ShipmentMethod); }
        }

        public PaymentMethod PaymentMethod
        {
            get { return _paymentMethod; }
            set { SetProperty(ref _paymentMethod, value, () => PaymentMethod); }
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

        public string ABN
        {
            get { return _abn; }
            set { SetProperty(ref _abn, value, () => ABN); }
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

        public string BusinessType
        {
            get { return _businessType; }
            set { SetProperty(ref _businessType, value, () => BusinessType); }
        }

        public string LegacySource
        {
            get { return _legacySource; }
            set { SetProperty(ref _legacySource, value, () => LegacySource); }
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

        public Title ContactTitle
        {
            get { return _contactTitle; }
            set { SetProperty(ref _contactTitle, value, () => ContactTitle); }
        }

        public Gender ContactGender
        {
            get { return _contactGender; }
            set { SetProperty(ref _contactGender, value, () => ContactGender); }
        }

        public string ContactFirstName
        {
            get { return _contactFirstName; }
            set { SetProperty(ref _contactFirstName, value, () => ContactFirstName); }
        }

        public string ContactLastName
        {
            get { return _contactLastName; }
            set { SetProperty(ref _contactLastName, value, () => ContactLastName); }
        }

        public string ContactRole
        {
            get { return _contactRole; }
            set { SetProperty(ref _contactRole, value, () => ContactRole); }
        }

        public string ContactPhone
        {
            get { return _contactPhone; }
            set { SetProperty(ref _contactPhone, value, () => ContactPhone); }
        }

        public string ContactFax
        {
            get { return _contactFax; }
            set { SetProperty(ref _contactFax, value, () => ContactFax); }
        }

        #endregion


        #region Metadata

        ///<summary>
        /// Builds metadata for properties.
        ///</summary>
        ///<param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<VendorUploaderDataModel> builder)
        {
            builder.Property(p => p.LegacyReference)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.CompanyName)
                .ReadOnly()
                .Required()
                .MaxLength(100);
            builder.Property(p => p.SearchName)
                .ReadOnly()
                .MaxLength(255);
            builder.Property(p => p.OwnershipType)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.VendorPostingGroup)
                .ReadOnly();
            builder.Property(p => p.GeneralBusinessPostingGroup)
                .ReadOnly();
            builder.Property(p => p.GSTBusinessPostingGroup)
                .ReadOnly();
            builder.Property(p => p.Currency)
                .ReadOnly();
            builder.Property(p => p.PaymentTerms)
                 .ReadOnly();
            builder.Property(p => p.CashFlowPaymentTerms)
                .ReadOnly();
            builder.Property(p => p.ShipmentMethod)
                .ReadOnly();
            builder.Property(p => p.PaymentMethod)
                .ReadOnly();
            builder.Property(p => p.Email)
                .ReadOnly()
                .MaxLength(255);
            builder.Property(p => p.HomePage)
                .ReadOnly()
                .MaxLength(255);
            builder.Property(p => p.ABN)
                .ReadOnly()
                .MaxLength(11);
            builder.Property(p => p.Industry)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.IndustryClass)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.BusinessType)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.LegacySource)
                .ReadOnly()
                .MaxLength(30);
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
            builder.Property(p => p.ContactTitle)
                .ReadOnly();
            builder.Property(p => p.ContactGender)
                .ReadOnly();
            builder.Property(p => p.ContactFirstName)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.ContactLastName)
                .ReadOnly()
                .MaxLength(100);
            //builder.Property(p => p.ContactRole)
            //    .ReadOnly()
            //    .MaxLength(100);
            builder.Property(p => p.ContactPhone)
                .ReadOnly()
                .MaxLength(30);
            builder.Property(p => p.ContactFax)
                .ReadOnly()
                .MaxLength(30);
        }

        ///<summary>
        ///Builds metadata for editors.
        ///</summary>
        ///<param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<VendorUploaderDataModel> builder)
        {
            builder.Property(p => p.Email).ReplaceEditor(new HyperLinkEditorDefinition() { AllowUrls = false });
            builder.Property(p => p.HomePage).ReplaceEditor(new HyperLinkEditorDefinition() { AllowEmails = false });

            builder.Property(p => p.Postcode)
                .GetEditor<LookUpEditorDefinition>().DisplayMember = "Code";
        }

        #endregion
    }
}
