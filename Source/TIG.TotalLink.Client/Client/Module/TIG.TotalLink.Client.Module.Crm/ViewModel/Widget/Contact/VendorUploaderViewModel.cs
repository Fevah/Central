using System;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Module.Admin.Extension;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Crm.Uploader;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact
{
    public class VendorUploaderViewModel : UploaderViewModelBase<VendorUploaderDataModel>
    {
        #region Private Fields

        private readonly ICrmFacade _crmFacade;
        private UnitOfWork _unitOfWork;
        private ContactLinkType _branchContactLinkType;
        private ContactLinkType _employeeContactLinkType;

        #endregion


        #region Constructors

        public VendorUploaderViewModel()
        {
        }

        public VendorUploaderViewModel(ICrmFacade crmFacade)
            : this()
        {
            _crmFacade = crmFacade;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Finds or creates a ContactLinkType.
        /// </summary>
        /// <param name="name">The name of the ContactLinkType to find or create.</param>
        /// <returns>An existing ContactLinkType if one was found; otherwise returns a new one.</returns>
        private ContactLinkType FindOrCreateContactLinkType(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the ContactLinkType in the database
            var contactLinkType = _unitOfWork.QueryInTransaction<ContactLinkType>().FirstOrDefault(c => c.Name == name);
            if (contactLinkType != null)
                return contactLinkType;

            // If no ContactLinkType was found, create a new one
            contactLinkType = new ContactLinkType(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return contactLinkType;
        }

        /// <summary>
        /// Finds or creates a ContactLink.
        /// </summary>
        /// <param name="source">The source (child) Contact.</param>
        /// <param name="target">The target (parent) Contact.</param>
        /// <param name="linkType">The ContactLinkType.</param>
        /// <returns>A ContactLink.</returns>
        private ContactLink FindOrCreateContactLink(Shared.DataModel.Crm.Contact source, Shared.DataModel.Crm.Contact target, ContactLinkType linkType)
        {
            // Abort if any of the parameters are null
            if (source == null || target == null || linkType == null)
                return null;

            // Attempt to find the ContactLink in the database
            var contactLink = _unitOfWork.QueryInTransaction<ContactLink>().FirstOrDefault(d => d.Source.Oid == source.Oid && d.Target.Oid == target.Oid && d.ContactLinkType.Oid == linkType.Oid);
            if (contactLink != null)
                return contactLink;

            // Create a new ContactLink
            contactLink = new ContactLink(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Source = source,
                Target = target,
                ContactLinkType = linkType,
            };
            return contactLink;
        }


        /// <summary>
        /// Finds or creates an Industry.
        /// </summary>
        /// <param name="name">The name of the Industry to find or create.</param>
        /// <returns>An existing Industry if one was found; otherwise returns a new one.</returns>
        private Industry FindOrCreateIndustry(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the Industry in the database
            var industry = _unitOfWork.QueryInTransaction<Industry>().FirstOrDefault(i => i.Name == name);
            if (industry != null)
                return industry;

            // If no Industry was found, create a new one
            industry = new Industry(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return industry;
        }

        /// <summary>
        /// Finds or creates an IndustryClass.
        /// </summary>
        /// <param name="name">The name of the IndustryClass to find or create.</param>
        /// <param name="industry">The Industry that the IndustryClass belongs to.</param>
        /// <returns>An existing IndustryClass if one was found; otherwise returns a new one.</returns>
        private IndustryClass FindOrCreateIndustryClass(string name, Industry industry)
        {
            // Abort if any of the parameters are empty
            if (string.IsNullOrWhiteSpace(name) || industry == null)
                return null;

            // Attempt to find the IndustryClass in the database
            var industryClass = _unitOfWork.QueryInTransaction<IndustryClass>().FirstOrDefault(i => i.Industry.Oid == industry.Oid && i.Name == name);
            if (industryClass != null)
                return industryClass;

            // If no IndustryClass was found, create a new one
            industryClass = new IndustryClass(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name,
                Industry = industry
            };
            return industryClass;
        }

        /// <summary>
        /// Finds or creates a OwnershipType.
        /// </summary>
        /// <param name="name">The name of the OwnershipType to find or create.</param>
        /// <returns>An OwnershipType.</returns>
        private OwnershipType FindOrCreateOwnershipType(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the OwnershipType in the database
            var ownershipType = _unitOfWork.QueryInTransaction<OwnershipType>().FirstOrDefault(d => d.Name == name);
            if (ownershipType != null)
                return ownershipType;

            // Create a new OwnershipType
            ownershipType = new OwnershipType(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return ownershipType;
        }

        /// <summary>
        /// Finds or creates a BusinessType.
        /// </summary>
        /// <param name="name">The name of the BusinessType to find or create.</param>
        /// <returns>An BusinessType.</returns>
        private BusinessType FindOrCreateBusinessType(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the BusinessType in the database
            var businessType = _unitOfWork.QueryInTransaction<BusinessType>().FirstOrDefault(d => d.Name == name);
            if (businessType != null)
                return businessType;

            // Create a new BusinessType
            businessType = new BusinessType(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return businessType;
        }

        /// <summary>
        /// Finds or creates a Company Contact.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <returns>An existing Contact if one was found; otherwise returns a new one.</returns>
        private Company FindOrCreateCompany(VendorUploaderDataModel dataModel)
        {
            // Abort if the CompanyName is empty
            if (string.IsNullOrWhiteSpace(dataModel.CompanyName))
                return null;

            // Attempt to find the Company in the database
            var company = _unitOfWork.QueryInTransaction<Company>().FirstOrDefault(c => c.FirstName == dataModel.CompanyName);
            if (company != null)
                return company;

            // Create a new Company
            company = new Company(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                FirstName = dataModel.CompanyName,
                IsVendor = true
            };
            company.GenerateReferenceNumber();
            return company;
        }

        /// <summary>
        /// Finds or creates a Branch Contact.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="industryClass">The IndustryClass that the Branch belongs to.</param>
        /// <param name="ownershipType">The OwnershipType that the Branch belongs to.</param>
        /// <param name="businessType">The BusinessType that the Branch belongs to.</param>
        /// <returns>An existing Contact if one was found; otherwise returns a new one.</returns>
        private Branch FindOrCreateBranch(VendorUploaderDataModel dataModel, IndustryClass industryClass, OwnershipType ownershipType, BusinessType businessType)
        {
            // Abort if the VendorNo or CompanyName is empty
            if (string.IsNullOrWhiteSpace(dataModel.LegacyReference) || string.IsNullOrWhiteSpace(dataModel.CompanyName))
                return null;

            // Attempt to find the Branch in the database
            var branch = _unitOfWork.QueryInTransaction<Branch>().FirstOrDefault(b => b.LegacyReference == dataModel.LegacyReference);
            if (branch != null)
                return branch;

            // Create a new Branch
            branch = new Branch(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                LegacyReference = dataModel.LegacyReference,
                BranchType = _unitOfWork.GetDataObject(dataModel.BranchType),
                OwnershipType = ownershipType,
                IndustryClass = industryClass,
                Email = dataModel.Email,
                Webpage = dataModel.HomePage,
                FirstName = dataModel.CompanyName,
                SearchName = dataModel.SearchName,
                Fax = dataModel.ContactFax,
                BusinessPhone = dataModel.ContactPhone,
                Currency = _unitOfWork.GetDataObject(dataModel.Currency),
                PaymentTerms = _unitOfWork.GetDataObject(dataModel.PaymentTerms),
                PaymentMethod = _unitOfWork.GetDataObject(dataModel.PaymentMethod),
                CashFlowPaymentTerms = _unitOfWork.GetDataObject(dataModel.CashFlowPaymentTerms),
                ABN = dataModel.ABN,
                BusinessType = businessType,
                ContactPostingGroup = _unitOfWork.GetDataObject(dataModel.VendorPostingGroup),
                GeneralBusinessPostingGroup = _unitOfWork.GetDataObject(dataModel.GeneralBusinessPostingGroup),
                GSTBusinessPostingGroup = _unitOfWork.GetDataObject(dataModel.GSTBusinessPostingGroup),
                ShipmentMethod = _unitOfWork.GetDataObject(dataModel.ShipmentMethod),
                LegacySource = dataModel.LegacySource,
                AllowPartialDelivery = false,
                IsVendor = true
            };
            branch.GenerateReferenceNumber();
            return branch;
        }

        /// <summary>
        /// Finds or creates a StaffRole.
        /// </summary>
        /// <param name="name">The name of the StaffRole to find or create.</param>
        /// <returns>A StaffRole.</returns>
        private StaffRole FindOrCreateStaffRole(string name)
        {
            // Abort if the Name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the StaffRole in the database
            var staffRole = _unitOfWork.QueryInTransaction<StaffRole>().FirstOrDefault(a => a.Name == name);
            if (staffRole != null)
                return staffRole;

            // Create a new StaffRole
            staffRole = new StaffRole(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return staffRole;
        }

        /// <summary>
        /// Finds or creates an Person Contact.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="staffRole">The StaffRole for the Contact.</param>
        /// <returns>An existing Contact if one was found; otherwise returns a new one.</returns>
        private Person FindOrCreateEmployee(VendorUploaderDataModel dataModel, StaffRole staffRole)
        {
            // Abort if the ContactFirstName is empty
            if (string.IsNullOrWhiteSpace(dataModel.ContactFirstName))
                return null;

            // Attempt to find the Person in the database
            var person = _unitOfWork.QueryInTransaction<Person>().FirstOrDefault(p => p.FirstName == dataModel.ContactFirstName && p.LastName == dataModel.ContactLastName);
            if (person != null)
                return person;

            // Create a new Person
            person = new Person(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                FirstName = dataModel.ContactFirstName,
                LastName = dataModel.ContactLastName,
                Fax = dataModel.ContactFax,
                BusinessPhone = dataModel.ContactPhone,
                Gender = dataModel.ContactGender,
                Title = _unitOfWork.GetDataObject(dataModel.ContactTitle),
                AllowPartialDelivery = false,
                IsVendor = false,
                StaffRole = staffRole
            };
            person.GenerateReferenceNumber();
            return person;
        }

        /// <summary>
        /// Finds or creates an Address.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="contact">The Contact that the Address belongs to.</param>
        /// <returns>An Address.</returns>
        private Address FindOrCreateAddress(VendorUploaderDataModel dataModel, Shared.DataModel.Crm.Contact contact)
        {
            // Abort if the Address1 and Address2 is empty
            if (string.IsNullOrWhiteSpace(dataModel.Address1) && string.IsNullOrWhiteSpace(dataModel.Address2))
                return null;

            // Attempt to find the Address in the database
            var address = _unitOfWork.QueryInTransaction<Address>().FirstOrDefault(a => a.Contact.Oid == contact.Oid && a.AddressType.Oid == dataModel.AddressType.Oid);
            if (address != null)
                return address;

            // Create a new Address
            address = new Address(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                AddressType = _unitOfWork.GetDataObject(dataModel.AddressType),
                IsDefault = (dataModel.AddressType.Name == "Billing"),
                LegacyLine1 = dataModel.Address1,
                LegacyLine2 = dataModel.Address2,
                City = dataModel.City,
                Postcode = _unitOfWork.GetDataObject(dataModel.Postcode),
                Contact = contact
            };

            return address;
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
            });
        }

        protected override void InitializeUpload()
        {
            base.InitializeUpload();

            // Create a UnitOfWork and start notification tracking
            _unitOfWork = _crmFacade.CreateUnitOfWork();
            _unitOfWork.StartUiTracking(this, true, false, true, false);

            // Create required ContactLinkTypes
            _branchContactLinkType = FindOrCreateContactLinkType("Branch");
            _employeeContactLinkType = FindOrCreateContactLinkType("Employee");

            // Commit the created types
            _unitOfWork.CommitChanges();
        }

        protected override void UploadRow(VendorUploaderDataModel dataModel)
        {
            base.UploadRow(dataModel);

            // Lookups
            var industry = FindOrCreateIndustry(dataModel.Industry);
            var industryClass = FindOrCreateIndustryClass(dataModel.IndustryClass, industry);
            var ownershipType = FindOrCreateOwnershipType(dataModel.OwnershipType);
            var businessType = FindOrCreateBusinessType(dataModel.BusinessType);

            // Vendor Company
            var company = FindOrCreateCompany(dataModel);

            // Vendor Branch
            var branch = FindOrCreateBranch(dataModel, industryClass, ownershipType, businessType);
            FindOrCreateContactLink(branch, company, _branchContactLinkType);

            // Vendor Address
            FindOrCreateAddress(dataModel, branch);

            // Vendor Contact
            var role = FindOrCreateStaffRole(dataModel.ContactRole);
            var employee = FindOrCreateEmployee(dataModel, role);
            FindOrCreateContactLink(employee, branch, _employeeContactLinkType);
        }

        protected override void WriteBatch()
        {
            base.WriteBatch();

            // Commit the UnitOfWork
            _unitOfWork.CommitChanges();
        }

        protected override void FinalizeUpload()
        {
            base.FinalizeUpload();

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
