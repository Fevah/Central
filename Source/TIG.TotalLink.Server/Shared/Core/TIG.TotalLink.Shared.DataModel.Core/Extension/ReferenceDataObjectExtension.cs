using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Core.Interface;

namespace TIG.TotalLink.Shared.DataModel.Core.Extension
{
    public static class ReferenceDataObjectExtension
    {
        #region Public Properties

        /// <summary>
        /// A numeric format string for displaying reference numbers.
        /// </summary>
        public static string ReferenceDisplayFormat { get; set; }

        /// <summary>
        /// A regex expression for cleaning reference numbers.
        /// </summary>
        public static string ReferenceDisplayClean { get; set; }
        
        #endregion


        #region Public Methods

        /// <summary>
        /// Returns the formatted Reference Number from an object which implements IReferenceDataObject.
        /// </summary>
        /// <param name="referenceDataObject">The IReferenceDataObject to collect the Reference Number from.</param>
        public static string GetFormattedReferenceNumber(this IReferenceDataObject referenceDataObject)
        {
            return ReferenceNumberHelper.FormatDisplayCleaned(ReferenceNumberHelper.FormatDisplay(referenceDataObject.Reference, ReferenceDisplayFormat), ReferenceDisplayClean);
        }

        #endregion
    }
}
