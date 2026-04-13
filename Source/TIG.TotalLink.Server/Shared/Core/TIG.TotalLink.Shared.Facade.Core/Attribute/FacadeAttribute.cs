using System;

namespace TIG.TotalLink.Shared.Facade.Core.Attribute
{
    /// <summary>
    /// Defines a class as a facade and specifies connection info.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class FacadeAttribute : System.Attribute
    {
        #region Constructors

        public FacadeAttribute(int dataPortOffset, string dataServiceName, int methodPortOffset, string methodServiceName, bool hasLocalDataService = false)
        {
            DataPortOffset = dataPortOffset;
            DataServiceName = dataServiceName;
            MethodPortOffset = methodPortOffset;
            MethodServiceName = methodServiceName;
            HasLocalDataService = hasLocalDataService;
        }

        public FacadeAttribute(int dataPortOffset, string dataServiceName, bool hasLocalDataService = false)
        {
            DataServiceName = dataServiceName;
            DataPortOffset = dataPortOffset;
            HasLocalDataService = hasLocalDataService;
        }

        public FacadeAttribute(string methodServiceName, int methodPortOffset, bool hasLocalDataService = false)
        {
            MethodPortOffset = methodPortOffset;
            MethodServiceName = methodServiceName;
            HasLocalDataService = hasLocalDataService;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The port offset that the data service is listening on.
        /// </summary>
        public int DataPortOffset { get; private set; }

        /// <summary>
        /// The name of the service that this facade uses for data access.
        /// </summary>
        public string DataServiceName { get; private set; }

        /// <summary>
        /// The port offset that the method service is listening on.
        /// </summary>
        public int MethodPortOffset { get; private set; }

        /// <summary>
        /// The name of the service that this facade uses to perform operations.
        /// </summary>
        public string MethodServiceName { get; private set; }

        /// <summary>
        /// Indicates if this facade includes a method service.
        /// </summary>
        public bool HasMethodService
        {
            get { return !string.IsNullOrWhiteSpace(MethodServiceName); }
        }

        /// <summary>
        /// Indicates if this facade includes a local data service.
        /// </summary>
        public bool HasLocalDataService { get; private set; }

        /// <summary>
        /// Indicates if this facade includes a method service.
        /// </summary>
        public bool HasDataService
        {
            get { return !string.IsNullOrWhiteSpace(DataServiceName); }
        }


        #endregion
    }
}
