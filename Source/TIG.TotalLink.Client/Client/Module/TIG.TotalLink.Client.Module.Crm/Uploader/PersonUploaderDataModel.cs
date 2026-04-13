using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Uploader.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.CRM;
using TIG.TotalLink.Shared.DataModel.Crm;

namespace TIG.TotalLink.Client.Module.Crm.Uploader
{
    public class PersonUploaderDataModel : UploaderDataModelBase
    {
        #region Private Fields

        private string _externalReference;
        private string _staffRole;
        private decimal _subsidyValue;
        private string _subsidyName;
        private Title _title;
        private string _firstName;
        private string _lastName;
        private Gender _gender;
        private string _email;
        private Branch _branch;
        private string _homePhone;
        private string _fax;
        private string _businessPhone;
        private string _mobile;
        private string _company;

        #endregion


        #region Public Properties

        public string Company
        {
            get { return _company; }
            set { SetProperty(ref _company, value, () => Company); }
        }

        public Title Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value, () => Title); }
        }

        public string FirstName
        {
            get { return _firstName; }
            set { SetProperty(ref _firstName, value, () => FirstName); }
        }

        public string LastName
        {
            get { return _lastName; }
            set { SetProperty(ref _lastName, value, () => LastName); }
        }

        public string StaffRole
        {
            get { return _staffRole; }
            set { SetProperty(ref _staffRole, value, () => StaffRole); }
        }

        public string ExternalReference
        {
            get { return _externalReference; }
            set { SetProperty(ref _externalReference, value, () => ExternalReference); }
        }
        
        public Gender Gender
        {
            get { return _gender; }
            set { SetProperty(ref _gender, value, () => Gender); }
        }

        public Branch Branch
        {
            get { return _branch; }
            set { SetProperty(ref _branch, value, () => Branch); }
        }

        public string Email
        {
            get { return _email; }
            set { SetProperty(ref _email, value, () => Email); }
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

        public string BusinessPhone
        {
            get { return _businessPhone; }
            set { SetProperty(ref _businessPhone, value, () => BusinessPhone); }
        }

        public string Mobile
        {
            get { return _mobile; }
            set { SetProperty(ref _mobile, value, () => Mobile); }
        }

        public string SubsidyName
        {
            get { return _subsidyName; }
            set { SetProperty(ref _subsidyName, value, () => SubsidyName); }
        }

        public decimal SubsidyValue
        {
            get { return _subsidyValue; }
            set { SetProperty(ref _subsidyValue, value, () => SubsidyValue); }
        }

        #endregion


        #region Metadata

        ///<summary>
        /// Builds metadata for properties.
        ///</summary>
        ///<param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<PersonUploaderDataModel> builder)
        {
            builder.Property(p => p.Company)
                .ReadOnly();
            builder.Property(p => p.Title)
                .ReadOnly();
            builder.Property(p => p.FirstName)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.LastName)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.Gender)
                .ReadOnly();
            builder.Property(p => p.Email)
                .ReadOnly()
                .MaxLength(255);
            builder.Property(p => p.Branch)
                .ReadOnly();
            builder.Property(p => p.BusinessPhone)
                .ReadOnly()
                .MaxLength(30);
            builder.Property(p => p.HomePhone)
                .ReadOnly()
                .MaxLength(30);
            builder.Property(p => p.Fax)
                .ReadOnly()
                .MaxLength(30);
            builder.Property(p => p.Mobile)
                .ReadOnly()
                .MaxLength(30);
            builder.Property(p => p.SubsidyName)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.ExternalReference)
                .MaxLength(100)
                .ReadOnly();
            builder.Property(p => p.SubsidyValue)
                .ReadOnly();
            builder.Property(p => p.StaffRole)
                .ReadOnly();
        }

        ///<summary>
        ///Builds metadata for editors.
        ///</summary>
        ///<param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<PersonUploaderDataModel> builder)
        {
            builder.Property(p => p.Email)
                .ReplaceEditor(new HyperLinkEditorDefinition() { AllowUrls = false });

            builder.Property(p => p.Gender)
                .ReplaceEditor(new ComboEditorDefinition(typeof(Gender)));

            builder.Property(p => p.SubsidyValue)
                .ReplaceEditor(new CurrencyEditorDefinition());
        }

        #endregion
    }
}
