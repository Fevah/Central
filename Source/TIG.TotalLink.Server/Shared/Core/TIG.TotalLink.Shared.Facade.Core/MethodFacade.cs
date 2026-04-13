using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.Facade.Core.ServiceClientBehavior;
using TIG.TotalLink.Shared.Facade.Core.ServiceClientMessageInspector;

namespace TIG.TotalLink.Shared.Facade.Core
{
    public class MethodFacade<TContract>
        where TContract : IMethodServiceBase
    {
        #region Private Fields

        private readonly ChannelFactory<TContract> _channelFactory;

        #endregion


        #region Constructors

        public MethodFacade(string address, string authenticationToken)
        {
            // Create an endpoint and binding for the service
            var endpoint = new EndpointAddress(address);
            var binding = new BasicHttpBinding
            {
                MaxBufferPoolSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                TransferMode = TransferMode.Streamed,
                OpenTimeout = new TimeSpan(0, 5, 0),
                CloseTimeout = new TimeSpan(0, 5, 0),
                SendTimeout = new TimeSpan(0, 5, 0),
                ReceiveTimeout = new TimeSpan(0, 5, 0),
                ReaderQuotas = new XmlDictionaryReaderQuotas()
                {
                    MaxDepth = int.MaxValue,
                    MaxArrayLength = int.MaxValue,
                    MaxStringContentLength = int.MaxValue
                }
            };

            // Create a client channel factory
            _channelFactory = new ChannelFactory<TContract>(binding, endpoint);

            // If an authentication token was specified, add an authentication inspector to the channel
            if (!string.IsNullOrEmpty(authenticationToken))
            {
                var inspector = new AuthenticationClientMessageInspector(authenticationToken);
                _channelFactory.Endpoint.EndpointBehaviors.Add(new AuthenticationEndpointClientBehavior(inspector));
            }

            // Call the Ping method to test the connection
            Execute(c => c.Ping());
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Executes a sevice method which returns no result.
        /// </summary>
        /// <param name="action">The action to execute on the service.</param>
        public void Execute(Action<TContract> action)
        {
            var channel = default(TContract);
            try
            {
                // Open a channel
                channel = OpenChannel();
               
                // Execute the action
                action(channel);

                // Close the channel
                CloseChannel(channel);
            }
            catch (Exception)
            {
                // If any errors occurred, abort the channel and rethrow the exception
                AbortChannel(channel);
                throw;
            }
        }

        /// <summary>
        /// Executes a sevice method asynchronously which returns no result.
        /// </summary>
        /// <param name="action">The action to execute on the service.</param>
        public async Task ExecuteAsync(Action<TContract> action)
        {
            var channel = default(TContract);
            try
            {
                // Open a channel
                channel = await OpenChannelAsync().ConfigureAwait(false);

                // Execute the action
                await Task.Run(() => action(channel)).ConfigureAwait(false);

                // Close the channel
                await CloseChannelAsync(channel).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // If any errors occurred, abort the channel and rethrow the exception
                AbortChannel(channel);
                throw;
            }
        }

        /// <summary>
        /// Executes a sevice method which returns a single result.
        /// </summary>
        /// <typeparam name="TResult">The type of result returned by the function.</typeparam>
        /// <param name="func">The function to execute on the service.</param>
        /// <returns>The result of the function.</returns>
        public TResult Execute<TResult>(Func<TContract, TResult> func)
        {
            TResult result;

            var channel = default(TContract);
            try
            {
                // Open a channel
                channel = OpenChannel();

                // Execute the action
                result = func(channel);

                // Close the channel
                CloseChannel(channel);
            }
            catch (Exception)
            {
                // If any errors occurred, abort the channel and rethrow the exception
                AbortChannel(channel);
                throw;
            }

            // Return the result
            return result;
        }

        /// <summary>
        /// Executes a sevice method asynchronously which returns a single result.
        /// </summary>
        /// <typeparam name="TResult">The type of result returned by the function.</typeparam>
        /// <param name="func">The function to execute on the service.</param>
        /// <returns>The result of the function.</returns>
        public async Task<TResult> ExecuteAsync<TResult>(Func<TContract, TResult> func)
        {
            TResult result;

            var channel = default(TContract);
            try
            {
                // Open a channel
                channel = await OpenChannelAsync().ConfigureAwait(false);

                // Execute the action
                result = await Task.Run(() => func(channel)).ConfigureAwait(false);

                // Close the channel
                await CloseChannelAsync(channel).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // If any errors occurred, abort the channel and rethrow the exception
                AbortChannel(channel);
                throw;
            }

            // Return the result
            return result;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Creates a new client channel.
        /// </summary>
        /// <returns>The new client channel.</returns>
        private TContract OpenChannel()
        {
            // Open the channel
            var channel = _channelFactory.CreateChannel();
            ((IClientChannel)channel).Open();

            // Return the channel
            return channel;
        }

        /// <summary>
        /// Creates a new client channel asynchronously.
        /// </summary>
        /// <returns>The new client channel.</returns>
        private Task<TContract> OpenChannelAsync()
        {
            // This will be our sentry that will know when our async operation is completed
            var tcs = new TaskCompletionSource<TContract>();

            try
            {
                var channel = _channelFactory.CreateChannel();

                // Begin opening the channel
                ((IClientChannel)channel).BeginOpen(iar =>
                {
                    try
                    {
                        // End opening the channel
                        ((IClientChannel)channel).EndOpen(iar);
                        tcs.TrySetResult(channel);
                    }
                    catch (OperationCanceledException ex)
                    {
                        // If the inner operation was canceled, this task is cancelled too
                        tcs.TrySetCanceled();
                    }
                    catch (Exception ex)
                    {
                        // General exception has been set
                        tcs.TrySetException(ex);
                    }
                }, null);
            }
            catch
            {
                // Complete the task
                tcs.TrySetResult(default(TContract));

                // Propagate exceptions to the outside
                throw;
            }

            return tcs.Task;
        }

        /// <summary>
        /// Closes a client channel.
        /// </summary>
        /// <param name="channel">The channel to close.</param>
        private void CloseChannel(TContract channel)
        {
            // Abort if the channel is null
            if (channel == null)
                return;

            try
            {
                // Close the channel
                ((IClientChannel)channel).Close();
            }
            catch
            {
                // Ignore any exceptions
            }
        }

        /// <summary>
        /// Closes a client channel asynchronously.
        /// </summary>
        /// <param name="channel">The channel to close.</param>
        private Task CloseChannelAsync(TContract channel)
        {
            // This will be our sentry that will know when our async operation is completed
            var tcs = new TaskCompletionSource<object>();

            try
            {
                // Begin closing the channel
                ((IClientChannel)channel).BeginClose(iar =>
                {
                    try
                    {
                        // End closing the channel
                        ((IClientChannel)channel).EndClose(iar);
                        tcs.TrySetResult(null);
                    }
                    catch (OperationCanceledException ex)
                    {
                        // If the inner operation was canceled, this task is cancelled too
                        tcs.TrySetCanceled();
                    }
                    catch (Exception ex)
                    {
                        // General exception has been set
                        tcs.TrySetException(ex);
                    }
                }, null);
            }
            catch
            {
                // Complete the task
                tcs.TrySetResult(default(TContract));

                // Ignore any exceptions
            }

            return tcs.Task;
        }

        /// <summary>
        /// Aborts a client channel.
        /// </summary>
        /// <param name="channel">The channel to abort.</param>
        private void AbortChannel(TContract channel)
        {
            // ABort if the channel is null
            if (channel == null)
                return;

            try
            {
                // Abort the channel
                ((IClientChannel)channel).Abort();
            }
            catch
            {
                // Ignore any exceptions
            }
        }

        #endregion
    }
}
