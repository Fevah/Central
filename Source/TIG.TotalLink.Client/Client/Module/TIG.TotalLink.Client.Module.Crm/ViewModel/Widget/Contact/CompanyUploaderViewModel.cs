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
    public class CompanyUploaderViewModel : UploaderViewModelBase<CompanyUploaderDataModel>
    {
        #region Private Fields

        private readonly ICrmFacade _crmFacade;
        private UnitOfWork _unitOfWork;
        private ContactLinkType _companyContactLinkType;
        private ContactLinkType _branchContactLinkType;

        #endregion


        #region Constructors

        public CompanyUploaderViewModel()
        {
        }

        public CompanyUploaderViewModel(ICrmFacade crmFacade)
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
        /// Finds or creates a ContactGroup.
        /// </summary>
        /// <param name="name">The name of the ContactGroup to find or create.</param>
        /// <returns>A ContactGroup.</returns>
        private ContactGroup FindOrCreateContactGroup(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the ContactGroup in the database
            var contactGroup = _unitOfWork.QueryInTransaction<ContactGroup>().FirstOrDefault(d => d.Name == name);
            if (contactGroup != null)
                return contactGroup;

            // Create a new ContactGroup
            contactGroup = new ContactGroup(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name,
            };
            return contactGroup;
        }

        /// <summary>
        /// Finds or creates a ContactGroupLink.
        /// </summary>
        /// <param name="contactGroup">The ContactGroup to link.</param>
        /// <param name="contact">The Contact to link.</param>
        /// <returns>A ContactGroupLink.</returns>
        private ContactGroupLink FindOrCreateContactGroupLink(ContactGroup contactGroup, Shared.DataModel.Crm.Contact contact)
        {
            // Abort if any of the parameters are null
            if (contactGroup == null || contact == null)
                return null;

            // Attempt to find the ContactGroupLink in the database
            var contactGroupLink = _unitOfWork.QueryInTransaction<ContactGroupLink>().FirstOrDefault(d => d.ContactGroup.Oid == contactGroup.Oid && d.Contact.Oid == contact.Oid);
            if (contactGroupLink != null)
                return contactGroupLink;

            // Create a new ContactGroupLink
            contactGroupLink = new ContactGroupLink(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                ContactGroup = contactGroup,
                Contact = contact,
                IsPrimary = true,
            };
            return contactGroupLink;
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
        /// Finds or creates a Chain Contact.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="industryClass">The IndustryClass that the Chain belongs to.</param>
        /// <returns>An existing Contact if one was found; otherwise returns a new one.</returns>
        private Chain FindOrCreateChain(CompanyUploaderDataModel dataModel, IndustryClass industryClass)
        {
            // Abort if the ChainName is empty
            if (string.IsNullOrWhiteSpace(dataModel.ChainName))
                return null;

            // Attempt to find the Chain in the database
            var chain = _unitOfWork.QueryInTransaction<Chain>().FirstOrDefault(c => c.FirstName == dataModel.ChainName);
            if (chain != null)
                return chain;

            // If no Chain was found, create a new one
            chain = new Chain(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                FirstName = dataModel.ChainName,
                IndustryClass = industryClass,
                ContactPostingGroup = _unitOfWork.GetDataObject(dataModel.CustomerPostingGroup),
                GeneralBusinessPostingGroup = _unitOfWork.GetDataObject(dataModel.GeneralBusinessPostingGroup),
                GSTBusinessPostingGroup = _unitOfWork.GetDataObject(dataModel.GSTBusinessPostingGroup),
                ShipmentMethod = _unitOfWork.GetDataObject(dataModel.ShipmentMethod),
                AllowPartialDelivery = false,
                IsVendor = false
            };

            chain.GenerateReferenceNumber();
            return chain;
        }

        /// <summary>
        /// Finds or creates a Company Contact.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="industryClass">The IndustryClass that the Company belongs to.</param>
        /// <returns>A Contact.</returns>
        private Company FindOrCreateCompany(CompanyUploaderDataModel dataModel, IndustryClass industryClass)
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
                IndustryClass = industryClass,
                TotalStores = dataModel.NumberOfStores,
                TotalEmployees = dataModel.NumberOfEmployees,
                ContactPostingGroup = _unitOfWork.GetDataObject(dataModel.CustomerPostingGroup),
                GeneralBusinessPostingGroup = _unitOfWork.GetDataObject(dataModel.GeneralBusinessPostingGroup),
                GSTBusinessPostingGroup = _unitOfWork.GetDataObject(dataModel.GSTBusinessPostingGroup),
                ShipmentMethod = _unitOfWork.GetDataObject(dataModel.ShipmentMethod),
                AllowPartialDelivery = false,
                IsVendor = false
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
        /// <returns>A Contact.</returns>
        private Branch FindOrCreateBranch(CompanyUploaderDataModel dataModel, IndustryClass industryClass, OwnershipType ownershipType, BusinessType businessType)
        {
            // If the BranchName is empty, use the CompanyName instead
            var branchName = (string.IsNullOrWhiteSpace(dataModel.BranchName) ? dataModel.CompanyName : dataModel.BranchName);

            // Abort if the CustomerNo or BranchName is empty
            if (string.IsNullOrWhiteSpace(dataModel.LegacyReference) || string.IsNullOrWhiteSpace(branchName))
                return null;

            // Attempt to find the Branch in the database
            var branch = _unitOfWork.QueryInTransaction<Branch>().FirstOrDefault(c => c.LegacyReference == dataModel.LegacyReference);
            if (branch != null)
                return branch;

            // Create a new Branch
            branch = new Branch(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                LegacyReference = dataModel.LegacyReference,
                ExternalReference = dataModel.ExternalReference,
                FirstName = branchName,
                SearchName = dataModel.CompanySearchName,
                OwnershipType = ownershipType,
                IndustryClass = industryClass,
                Email = dataModel.Email,
                Webpage = dataModel.HomePage,
                Fax = dataModel.Fax,
                BusinessPhone = dataModel.Phone,
                BusinessPhone2 = dataModel.Phone2,
                BusinessMobile = dataModel.MobilePhone,
                CreditLimit = dataModel.CreditLimit,
                PaymentTerms = _unitOfWork.GetDataObject(dataModel.PaymentTerms),
                PaymentMethod = _unitOfWork.GetDataObject(dataModel.PaymentMethod),
                ABN = dataModel.ABN,
                BranchNumber = dataModel.BranchNumber,
                BusinessType = businessType,
                ContactPostingGroup = _unitOfWork.GetDataObject(dataModel.CustomerPostingGroup),
                GeneralBusinessPostingGroup = _unitOfWork.GetDataObject(dataModel.GeneralBusinessPostingGroup),
                GSTBusinessPostingGroup = _unitOfWork.GetDataObject(dataModel.GSTBusinessPostingGroup),
                ShipmentMethod = _unitOfWork.GetDataObject(dataModel.ShipmentMethod),
                AllowPartialDelivery = false,
                IsVendor = false,
                BranchType = _unitOfWork.GetDataObject(dataModel.BranchType)
            };
            branch.GenerateReferenceNumber();
            return branch;
        }

        /// <summary>
        /// Finds or creates an Address.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="contact">The Contact that the Address belongs to.</param>
        /// <returns>An Address.</returns>
        private Address FindOrCreateAddress(CompanyUploaderDataModel dataModel, Shared.DataModel.Crm.Contact contact)
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
            _companyContactLinkType = FindOrCreateContactLinkType("Company");
            _branchContactLinkType = FindOrCreateContactLinkType("Branch");

            // Commit the created types
            _unitOfWork.CommitChanges();
        }

        protected override void UploadRow(CompanyUploaderDataModel dataModel)
        {
            base.UploadRow(dataModel);

            // Lookups
            var industry = FindOrCreateIndustry(dataModel.Industry);
            var industryClass = FindOrCreateIndustryClass(dataModel.IndustryClass, industry);
            var contactGroup = FindOrCreateContactGroup(dataModel.ContactGroup);
            var ownershipType = FindOrCreateOwnershipType(dataModel.OwnershipType);
            var businessType = FindOrCreateBusinessType(dataModel.BusinessType);

            // Chain
            var chain = FindOrCreateChain(dataModel, industryClass);
            FindOrCreateContactGroupLink(contactGroup, chain);

            // Company
            var company = FindOrCreateCompany(dataModel, industryClass);
            FindOrCreateContactGroupLink(contactGroup, company);
            FindOrCreateContactLink(company, chain, _companyContactLinkType);

            // Branch
            var branch = FindOrCreateBranch(dataModel, industryClass, ownershipType, businessType);
            FindOrCreateContactGroupLink(contactGroup, branch);
            FindOrCreateContactLink(branch, company, _branchContactLinkType);

            // Branch Address
            FindOrCreateAddress(dataModel, branch);
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
