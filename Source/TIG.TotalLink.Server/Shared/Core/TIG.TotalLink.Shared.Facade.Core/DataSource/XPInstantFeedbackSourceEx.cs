using System;
using System.Linq;
using DevExpress.Xpo;

namespace TIG.TotalLink.Shared.Facade.Core.DataSource
{
    public class XPInstantFeedbackSourceEx : XPInstantFeedbackSource
    {
        #region Public Events

        public event EventHandler RefreshStarting;

        #endregion


        #region Private Fields

        private readonly IDataLayer _dataLayer;
        private UnitOfWork _session;

        #endregion


        #region Constructors

        public XPInstantFeedbackSourceEx(IDataLayer dataLayer, Type objectType)
            : base(objectType, GetDisplayableProperties(dataLayer, objectType), null)
        {
            _dataLayer = dataLayer;
            
            ResolveSession += XPInstantFeedbackSource_ResolveSession;
            DismissSession += XPInstantFeedbackSource_DismissSession;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The session that this datasource uses to collect data.
        /// </summary>
        public UnitOfWork Session
        {
            get { return _session; }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Returns a string containing all the displayable members on the specified object type.
        /// We have to calculate these manually because dynamic PersistentAliases are excluded in the default implementation.
        /// </summary>
        /// <param name="dataLayer">The DataLayer that owns the objectType.</param>
        /// <param name="objectType">The type of object to collect proeprties for.</param>
        /// <returns>A semi-colon separated string of properties for the associated grid to display.</returns>
        private static string GetDisplayableProperties(IDataLayer dataLayer, Type objectType)
        {
            // Get the ClassInfo for the type
            var classInfo = dataLayer.Dictionary.GetClassInfo(objectType);

            // Collect a list of displayable properties using the following rules...
            // Include all persistent properties except for associations (excludes lookup columns)
            // Include all collection properties
            // Include all alias properties (includes dynamic aliases)
            var displayableProperties = classInfo.Members.Where(m => (m.IsPersistent && !m.IsAssociation) || m.IsCollection || m.IsAliased).ToList();

            // Return the displayable properties as a semi-colon separated string
            return string.Join(";", displayableProperties);
        }

        /// <summary>
        /// Raises the RefreshStarting event.
        /// </summary>
        private void RaiseRefreshStarting()
        {
            OnRefreshStarting(new EventArgs());
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Called just before the data source is refreshed.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnRefreshStarting(EventArgs e)
        {
            if (RefreshStarting != null)
                RefreshStarting(this, e);
        }

        #endregion


        #region Events

        /// <summary>
        /// Handles the DataSource.ResolveSession event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void XPInstantFeedbackSource_ResolveSession(object sender, ResolveSessionEventArgs e)
        {
            if (_dataLayer == null)
                return;

            _session = new UnitOfWork(_dataLayer);
            e.Session = _session;
        }

        /// <summary>
        /// Handles the DataSource.DismissSession event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void XPInstantFeedbackSource_DismissSession(object sender, ResolveSessionEventArgs e)
        {
            var session = e.Session as IDisposable;
            if (session == null)
                return;

            session.Dispose();
        }
        
        #endregion


        #region Overrides

        public new void Refresh()
        {
            RaiseRefreshStarting();
            base.Refresh();
        }

        #endregion
    }
}
