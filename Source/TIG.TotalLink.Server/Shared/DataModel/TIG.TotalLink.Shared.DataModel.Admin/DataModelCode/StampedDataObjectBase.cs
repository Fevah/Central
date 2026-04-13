using System;
using DevExpress.Xpo;

namespace TIG.TotalLink.Shared.DataModel.Admin
{
    public partial class StampedDataObjectBase
    {
        #region Private Fields

        private bool _isModifiedDateSet;
        private bool _isModifiedBySet;

        #endregion


        #region Constructors

        public StampedDataObjectBase()
            : base(Session.DefaultSession)
        {
        }

        public StampedDataObjectBase(Session session)
            : base(session)
        {
        }

        #endregion


        #region Partial Methods

        /// <summary>
        /// AfterConstruction method for client/server specific code.
        /// </summary>
        partial void AfterConstructionLocal();

        /// <summary>
        /// OnSaving method for client/server specific code.
        /// </summary>
        partial void OnSavingLocal();

        #endregion


        #region Overrides

        public override void AfterConstruction()
        {
            base.AfterConstruction();

            // Populate CreatedDate and ModifiedDate with the current time
            var now = DateTime.UtcNow;
            CreatedDate = now;
            ModifiedDate = now;

            // Perform client/server specific initialization
            AfterConstructionLocal();

            // Flag that the ModifiedDate and ModifiedUser have already been set
            _isModifiedDateSet = true;
            _isModifiedBySet = true;
        }

        protected override void OnSaving()
        {
            base.OnSaving();

            // If ModifiedDate hasn't been manually set, populate it with the current time
            if (!_isModifiedDateSet)
            {
                var now = DateTime.UtcNow;
                ModifiedDate = now;
            }

            // Perform client/server specific initialization
            OnSavingLocal();

            // Flag that the ModifiedDate and Modified user will need to be automatically set on next save if they are not set manually
            _isModifiedDateSet = false;
            _isModifiedBySet = false;
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();

            // Flag that the ModifiedDate and Modified user will need to be automatically set on next save if they are not set manually
            _isModifiedDateSet = false;
            _isModifiedBySet = false;
        }

        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);

            // Abort if the object is still loading
            if (IsLoading)
                return;

            // Flag that ModifiedDate has been manually set, so it does not need to be automatically set on next save
            if (propertyName == "ModifiedDate")
                _isModifiedDateSet = true;

            // Flag that ModifiedUser has been manually set, so it does not need to be automatically set on next save
            if (propertyName == "ModifiedUser")
                _isModifiedBySet = true;
        }

        #endregion
    }

}
