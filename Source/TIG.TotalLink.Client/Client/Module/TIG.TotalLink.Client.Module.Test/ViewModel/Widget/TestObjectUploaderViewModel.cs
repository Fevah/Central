using System;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Test.Uploader;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Test;
using TIG.TotalLink.Shared.Facade.Test;

namespace TIG.TotalLink.Client.Module.Test.ViewModel.Widget
{
    public class TestObjectUploaderViewModel : UploaderViewModelBase<TestObjectUploaderDataModel>
    {
        #region Private Fields

        private readonly ITestFacade _testFacade;
        private UnitOfWork _unitOfWork;

        #endregion


        #region Constructors

        public TestObjectUploaderViewModel()
        {
        }

        public TestObjectUploaderViewModel(ITestFacade testFacade)
            : this()
        {
            _testFacade = testFacade;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Finds or creates a TestObject.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <returns>A TestObject.</returns>
        private TestObject FindOrCreateTestObject(TestObjectUploaderDataModel dataModel)
        {
            // Abort if the Text is empty
            if (string.IsNullOrWhiteSpace(dataModel.Text))
                return null;

            // Attempt to find the TestObject in the database
            var testObject = _unitOfWork.QueryInTransaction<TestObject>().FirstOrDefault(t => t.Text == dataModel.Text);
            if (testObject != null)
                return testObject;

            // Create a new TestObject
            testObject = new TestObject(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Text = dataModel.Text,
                SpinInt = dataModel.SpinInt,
                Checkbox = dataModel.Checkbox,
                DateTime = dataModel.DateTime,
                HyperLink = dataModel.HyperLink,
                LookUp = _unitOfWork.GetDataObject(dataModel.LookUp),
                Currency = dataModel.Currency,
                Label = dataModel.Label,
                Progress = dataModel.Progress
            };
            return testObject;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the TestFacade
                ConnectToFacade(_testFacade);
            });
        }

        protected override void InitializeUpload()
        {
            base.InitializeUpload();

            // Create a UnitOfWork and start notification tracking
            _unitOfWork = _testFacade.CreateUnitOfWork();
            _unitOfWork.StartUiTracking(this, true, false, true, false);
        }

        protected override void UploadRow(TestObjectUploaderDataModel dataModel)
        {
            base.UploadRow(dataModel);

            // TestObject
            var testObject = FindOrCreateTestObject(dataModel);
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
