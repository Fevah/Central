namespace TIG.TotalLink.Shared.Contract.Core.Exception
{
    /// <summary>
    /// Throw this exception from methods that are called from service methods that can return a ServiceFault.
    /// </summary>
    public class ServiceMethodException : System.Exception
    {
        #region Constructors

        public ServiceMethodException(string message)
            : base(message)
        {
        }

        public ServiceMethodException(string message, System.Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }
}
