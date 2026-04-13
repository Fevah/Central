using LinqToLdap.Mapping;
using System;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.ActiveDirectory;

namespace TIG.TotalLink.Shared.DataModel.ActiveDirectory
{
    [Serializable]
    [DirectorySchema(NamingContext, ObjectCategory = "Person", ObjectClass = "User")]
    public partial class ActiveDirectoryUser : DataObjectBase
    {
        #region Private Fields

        private string _distinguishedName;
        private string _samAccountName;
        private Guid _oid;
        private string _displayName;
        private string _firstName;
        private string _lastName;
        private string _title;
        private string _department;
        private string _company;
        private string _manager;
        private string _office;
        private string _homePhone;
        private string _pager;
        private string _mobile;
        private string _fax;
        private string _ipPhone;
        private byte[] _sid;
        private AdsUserFlag _userAccountControl;
        private DateTime _createdDate;
        private DateTime _modifiedDate;

        #endregion


        #region Private Constants

        /// <summary>
        /// Default naming context, it will be overwritten in web config file.
        /// </summary>
        private const string NamingContext = "CN=Users,DC=totalimagegroup,DC=com,DC=au";

        #endregion


        #region Constructors

        public ActiveDirectoryUser() : base() { }
        public ActiveDirectoryUser(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }

        #endregion


        #region Properties

        /// <summary>
        /// The DistinguishedName for the user.
        /// </summary>
        [DistinguishedName]
        public string DistinguishedName
        {
            get { return _distinguishedName; }
            set { SetPropertyValue("DistinguishedName", ref _distinguishedName, value); }
        }

        /// <summary>
        /// The SAMAccountName for the user.
        /// </summary>
        [DirectoryAttribute("samaccountname")]
        public string SamAccountName
        {
            get { return _samAccountName; }
            set { SetPropertyValue("SamAccountName", ref _samAccountName, value); }
        }

        /// <summary>
        /// The Name of the domain that the user belongs to.
        /// </summary>
        public string DomainName { get; set; }

        /// <summary>
        /// The full login name for the user in domain\user format.
        /// </summary>
        [PersistentAlias("DomainName + '\\' + SamAccountName")]
        public string LoginName
        {
            get { return (string)EvaluateAlias("LoginName"); }
        }

        /// <summary>
        /// The Active Directory Guid for the user.
        /// </summary>
        [DirectoryAttribute("objectguid", StoreGenerated = true)]
        public override Guid Oid
        {
            get { return _oid; }
            set { SetPropertyValue("Oid", ref _oid, value); }
        }

        /// <summary>
        /// The Display Name for the user.
        /// </summary>
        [DirectoryAttribute("displayname", ReadOnly = true)]
        public string DisplayName
        {
            get { return _displayName; }
            set { SetPropertyValue("DisplayName", ref _displayName, value); }
        }

        /// <summary>
        /// FirstName for active directory user first name.
        /// </summary>
        [DirectoryAttribute("givenname")]
        public string FirstName
        {
            get { return _firstName; }
            set { SetPropertyValue("FirstName", ref _firstName, value); }
        }

        /// <summary>
        /// LastName for active directory user last name.
        /// </summary>
        [DirectoryAttribute("sn")]
        public string LastName
        {
            get { return _lastName; }
            set { SetPropertyValue("LastName", ref _lastName, value); }
        }

        /// <summary>
        /// Title for active directory user job title.
        /// </summary>
        [DirectoryAttribute("title")]
        public string Title
        {
            get { return _title; }
            set { SetPropertyValue("Title", ref _title, value); }
        }

        /// <summary>
        /// Department for active directory user department.
        /// </summary>
        [DirectoryAttribute("department")]
        public string Department
        {
            get { return _department; }
            set { SetPropertyValue("Department", ref _department, value); }
        }

        /// <summary>
        /// Company for active directory user company.
        /// </summary>
        [DirectoryAttribute("company")]
        public string Company
        {
            get { return _company; }
            set { SetPropertyValue("Company", ref _company, value); }
        }

        /// <summary>
        /// Manager for active directory user manger.
        /// </summary>
        [DirectoryAttribute("manager")]
        public string Manager
        {
            get { return _manager; }
            set { SetPropertyValue("Manager", ref _manager, value); }
        }

        /// <summary>
        /// Office for active directory user office.
        /// </summary>
        [DirectoryAttribute("physicalDeliveryOfficeName")]
        public string Office
        {
            get { return _office; }
            set { SetPropertyValue("Office", ref _office, value); }
        }

        /// <summary>
        /// HomePhone for active directory user home phone.
        /// </summary>
        [DirectoryAttribute("homePhone")]
        public string HomePhone
        {
            get { return _homePhone; }
            set { SetPropertyValue("HomePhone", ref _homePhone, value); }
        }

        /// <summary>
        /// Pager for active directory user pager.
        /// </summary>
        [DirectoryAttribute("pager")]
        public string Pager
        {
            get { return _pager; }
            set { SetPropertyValue("Pager", ref _pager, value); }
        }

        /// <summary>
        /// Mobile for active directory user mobile.
        /// </summary>
        [DirectoryAttribute("mobile")]
        public string Mobile
        {
            get { return _mobile; }
            set { SetPropertyValue("Mobile", ref _mobile, value); }
        }

        /// <summary>
        /// Fax for active directory user fax.
        /// </summary>
        [DirectoryAttribute("facsimileTelephoneNumber")]
        public string Fax
        {
            get { return _fax; }
            set { SetPropertyValue("Fax", ref _fax, value); }
        }

        /// <summary>
        /// IpPhone for active directory user IP phone.
        /// </summary>
        [DirectoryAttribute("ipPhone")]
        public string IpPhone
        {
            get { return _ipPhone; }
            set { SetPropertyValue("IpPhone", ref _ipPhone, value); }
        }

        /// <summary>
        /// DateTime that the user was created.
        /// </summary>
        [DirectoryAttribute("whencreated", StoreGenerated = true)]
        public DateTime CreatedDate
        {
            get { return _createdDate; }
            set { SetPropertyValue("CreatedDate", ref _createdDate, value); }
        }

        /// <summary>
        /// DateTime that the user was last modified.
        /// </summary>
        [DirectoryAttribute("whenchanged", StoreGenerated = true)]
        public DateTime ModifiedDate
        {
            get { return _modifiedDate; }
            set { SetPropertyValue("ModifiedDate", ref _modifiedDate, value); }
        }

        /// <summary>
        /// Sid for active directory user security identifier.
        /// </summary>
        [DirectoryAttribute("objectsid", StoreGenerated = true)]
        public byte[] Sid
        {
            get { return _sid; }
            set { SetPropertyValue("Sid", ref _sid, value); }
        }

        /// <summary>
        /// Flags that control the behavior of the user account.
        /// </summary>
        [DirectoryAttribute("userAccountControl")]
        public AdsUserFlag UserAccountControl
        {
            get { return _userAccountControl; }
            set { SetPropertyValue("UserAccountControl", ref _userAccountControl, value); }
        }

        /// <summary>
        /// Indicates if the user is active.
        /// (Displays the state of the AdsUserFlag.ADS_UF_ACCOUNTDISABLE flag in the UserAccountControl property.)
        /// </summary>
        [PersistentAlias("UserAccountControl & 2 == 0")]  // 2 = AdsUserFlag.ADS_UF_ACCOUNTDISABLE
        public bool IsActive
        {
            get { return (bool)EvaluateAlias("IsActive"); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Use this method to generate a DistinguishedName for a new user before saving.
        /// </summary>
        public void SetDistinguishedName()
        {
            DistinguishedName = string.Format("CN={0},{1}", DisplayName, NamingContext);
        }

        #endregion
    }
}