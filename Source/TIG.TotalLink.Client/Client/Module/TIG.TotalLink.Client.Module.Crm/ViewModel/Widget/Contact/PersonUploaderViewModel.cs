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
    public class PersonUploaderViewModel : UploaderViewModelBase<PersonUploaderDataModel>
    {
        #region Private Fields

        private readonly ICrmFacade _crmFacade;
        private UnitOfWork _unitOfWork;
        private ContactLinkType _employeeContactLinkType;

        #endregion


        #region Constructors

        public PersonUploaderViewModel()
        {
        }

        public PersonUploaderViewModel(ICrmFacade crmFacade)
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
        /// Finds or creates a Person Contact.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="staffRole">Staff Role of Person</param>
        /// <returns>A Person.</returns>
        private Person FindOrCreatePerson(PersonUploaderDataModel dataModel, StaffRole staffRole)
        {
            // Abort if the FirstName is empty
            if (string.IsNullOrWhiteSpace(dataModel.FirstName))
                return null;

            // Attempt to find the Person in the database
            var person = _unitOfWork.QueryInTransaction<Person>().FirstOrDefault(c => c.FirstName.Equals(dataModel.FirstName) && c.LastName.Equals(dataModel.LastName));
            if (person != null)
                return person;

            // Create a new Person
            person = new Person(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Title = dataModel.Title,
                FirstName = dataModel.FirstName,
                LastName = dataModel.LastName,
                Gender = dataModel.Gender,
                ExternalReference = dataModel.ExternalReference,
                StaffRole = staffRole,
                SubsidyValue = dataModel.SubsidyValue,
                SubsidyName = dataModel.SubsidyName,
                Email = dataModel.Email,
                BusinessPhone = dataModel.BusinessPhone,
                HomePhone = dataModel.HomePhone,
                Fax = dataModel.Fax,
                Mobile = dataModel.Mobile
            };
            person.GenerateReferenceNumber();
            return person;
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
            _employeeContactLinkType = FindOrCreateContactLinkType("Employee");

            // Commit the created types
            _unitOfWork.CommitChanges();
        }

        protected override void UploadRow(PersonUploaderDataModel dataModel)
        {
            base.UploadRow(dataModel);

            var role = FindOrCreateStaffRole(dataModel.StaffRole);
            var person = FindOrCreatePerson(dataModel, role);
            var branch = _unitOfWork.GetDataObject(dataModel.Branch);
            FindOrCreateContactLink(person, branch, _employeeContactLinkType);
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
