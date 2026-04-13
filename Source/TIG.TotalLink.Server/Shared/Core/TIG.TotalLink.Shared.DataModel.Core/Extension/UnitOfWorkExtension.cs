using System.Threading.Tasks;
using DevExpress.Xpo;

namespace TIG.TotalLink.Shared.DataModel.Core.Extension
{
    public static class UnitOfWorkExtension
    {
        #region Public Methods

        /// <summary>
        /// Awaitable version of CommitChangesAsync.
        /// </summary>
        /// <param name="uow">The UnitOFWork to commit.</param>
        public static Task CommitChangesAsync(this UnitOfWork uow)
        {
            // Create a TaskCompletionSource to control the async commit
            var tcs = new TaskCompletionSource<object>();

            // Asynchronously commit the UnitOfWork
            uow.CommitChangesAsync(exception =>
            {
                // Handle the result of the commit and set the task result or exception as necessary
                if (exception == null)
                    tcs.SetResult(null);
                else
                    tcs.SetException(exception);
            });

            // Return the task
            return tcs.Task;
        }

        #endregion
    }
}
