using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using DevExpress.Data.Filtering;
using Microsoft.Data.OData;
using Simple.OData.Client;
using TIG.IntegrationServer.Common;
using TIG.IntegrationServer.Common.Converter;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.Entity;
using TIG.IntegrationServer.Plugin.Core.ServiceContext.Interface;

namespace TIG.IntegrationServer.Plugin.Core.ServiceContext
{
    public class ODataServiceContext : IServiceContext
    {
        #region Private Properties

        private readonly string _authenticationToken;
        private readonly ODataClient _client;
        private ILog Log;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor OData service context
        /// </summary>
        /// <param name="uri">Service Uri</param>
        /// <param name="authenticationToken">Service authentication token for login service</param>
        /// <param name="credentials">Service credentials for login service</param>
        /// <param name="metadataUri">Metadata Uri, some service such Nav is independency with main service Uri</param>
        public ODataServiceContext(string uri, string authenticationToken, ICredentials credentials,
            string metadataUri = null)
        {
            _authenticationToken = authenticationToken;

            // Build Odata client settings.
            var settings = new ODataClientSettings(uri)
            {
                BeforeRequest = rm =>
                {
                    if (!string.IsNullOrWhiteSpace(_authenticationToken))
                        rm.Headers.Add("ticket", _authenticationToken);
                },
                IgnoreUnmappedProperties = true,
                IgnoreResourceNotFoundException = true
            };

            // Get metadata, if meatadata Uri was specifed.
            if (!string.IsNullOrEmpty(metadataUri))
            {
                var client = new WebClient();
                if (credentials != null)
                {
                    client.Credentials = credentials;
                }

                var stream = client.OpenRead(metadataUri);
                if (stream != null)
                {
                    var sr = new StreamReader(stream);
                    settings.MetadataDocument = sr.ReadToEnd();
                }
            }

            if (credentials != null)
            {
                settings.Credentials = credentials;
            }

            _client = new ODataClient(settings);

            Log = AutofacLocator.Resolve<ILog>();

            if (Log == null)
            {
                throw new ArgumentNullException("Log", "Please make sure loger componet was loaded.");
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// GetOne method by filter.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entity</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        public EntityBase GetOne(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter, params EntityJoinInfo[] joinEntityInfos)
        {
            var condition = ConverterToODataFilter(filter);

            var expand = joinEntityInfos.Select(p => p.ToString()).ToArray();

            try
            {
                var boundClient = _client.For(entityName).Filter(condition);
                if (expand.Any())
                {
                    boundClient = boundClient.Expand(expand);
                }

                var entity = boundClient.FindEntryAsync().Result;
                return entity == null ? null : EntityBase.Map(entity);
            }
            catch (AggregateException aex)
            {
                aex.Handle(ex =>
                {
                    var exception = ex as WebRequestException;
                    if (exception != null)
                    {
                        Log.Error(string.Format("Request: ({0}), Message: {1}",
                            entityName,
                            exception.Response ?? exception.ToString()));
                        return true;
                    }

                    var oDataException = ex as ODataException;
                    if (oDataException != null)
                    {
                        Log.Error(string.Format("Request: ({0}), Message: {1}",
                            entityName,
                            oDataException.Message));
                        return true;
                    }

                    return false;
                });

                return null;
            }
        }

        /// <summary>
        /// GetAll method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entities</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        public IEnumerable<EntityBase> GetAll(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter, params EntityJoinInfo[] joinEntityInfos)
        {
            var condition = ConverterToODataFilter(filter);

            var expand = joinEntityInfos.Select(p => p.ToString()).ToArray();

            try
            {
                var boundClient = _client.For(entityName).Filter(condition);
                if (expand.Any())
                {
                    boundClient = boundClient.Expand(expand);
                }

                var entities = boundClient.FindEntriesAsync().Result;
                return entities == null ? null : entities.Select(EntityBase.Map);
            }
            catch (AggregateException aex)
            {
                aex.Handle(ex =>
                {
                    var exception = ex as WebRequestException;
                    if (exception != null)
                    {
                        Log.Error(string.Format("Request: ({0}), Message: {1}",
                            entityName,
                            exception.Response ?? exception.ToString()));
                        return true;
                    }

                    var oDataException = ex as ODataException;
                    if (oDataException != null)
                    {
                        Log.Error(string.Format("Request: ({0}), Message: {1}",
                            entityName,
                            oDataException.Message));
                        return true;
                    }

                    return false;
                });

                return null;
            }
        }

        /// <summary>
        /// Update method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <param name="filter">Filter for locate required entity</param>
        public bool Update(string entityName, List<EntityFieldInfo> fieldInfos, EntityBase entity, CriteriaOperator filter)
        {
            var condition = ConverterToODataFilter(filter);

            try
            {
                var result = _client.For(entityName).Filter(condition).Set(entity).UpdateEntryAsync().Result;
                return true;
            }
            catch (AggregateException aex)
            {
                aex.Handle(ex =>
                {
                    var exception = ex as WebRequestException;
                    if (exception != null)
                    {
                        Log.Error(string.Format("Request: ({0}), Message: {1}",
                            entityName,
                            exception.Response ?? exception.ToString()));
                        return true;
                    }

                    var oDataException = ex as ODataException;
                    if (oDataException != null)
                    {
                        Log.Error(string.Format("Request: ({0}), Message: {1}",
                            entityName,
                            oDataException.Message));
                        return true;
                    }

                    return false;
                });

                return false;
            }
        }

        /// <summary>
        /// Create entity by entity information.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <returns>Retrieved entity from persistence</returns>
        public EntityBase Create(string entityName, List<EntityFieldInfo> fieldInfos, EntityBase entity)
        {
            try
            {
                var uselessKeys = entity.Where(p => p.Value == null).Select(p => p.Key).ToList();
                foreach (var key in uselessKeys)
                {
                    entity.Remove(key);
                }

                var result = _client.InsertEntryAsync(entityName, entity).Result;
                return result == null ? null : EntityBase.Map(result);
            }
            catch (AggregateException aex)
            {
                // Handle trancated case.
                if (aex.InnerException.Message == "Internal Server Error")
                {
                    Log.Error(string.Format("Request: ({0}), Message: Please check you mapping fields, and try it again later.",
                        entityName));

                    throw new Exception("Update failed, Task will be try again late.");
                }

                aex.Handle(ex =>
                {
                    var exception = ex as WebRequestException;
                    if (exception != null)
                    {
                        Log.Error(string.Format("Request: ({0}), Message: {1}",
                            entityName,
                            exception.Response ?? exception.ToString()));
                        return true;
                    }

                    var oDataException = ex as ODataException;
                    if (oDataException != null)
                    {
                        Log.Error(string.Format("Request: ({0}), Message: {1}",
                            entityName,
                            oDataException.Message));
                        return true;
                    }

                    return false;
                });
                return null;
            }
        }

        /// <summary>
        /// Delete entitys
        /// </summary>
        /// <param name="entityName">Entity name for delete</param>
        /// <param name="filter">Filter for delete entites</param>
        /// <returns></returns>
        public bool Delete(string entityName, CriteriaOperator filter)
        {
            var condition = ConverterToODataFilter(filter);

            try
            {
                _client.For(entityName).Filter(condition).DeleteEntryAsync();
                return true;
            }
            catch (AggregateException aex)
            {
                aex.Handle(ex =>
                {
                    var exception = ex as WebRequestException;
                    if (exception != null)
                    {
                        Log.Error(string.Format("Request: ({0}), Message: {1}",
                            entityName,
                            exception.Response ?? exception.ToString()));
                        return true;
                    }

                    var oDataException = ex as ODataException;
                    if (oDataException != null)
                    {
                        Log.Error(string.Format("Request: ({0}), Message: {1}",
                            entityName,
                            oDataException.Message));
                        return true;
                    }

                    return false;
                });

                return false;
            }
        }

        public string ConverterToODataFilter(CriteriaOperator filter)
        {
            const string replacementRegexPattern = @"[\w]{0,3}\.{(?<key>\w+)\,(?<type>\w+)}\s*(?<math>[\=(\w+)]+)\s*'*(?<value>[\w{}-]+)'*";
            var query = Regex.Replace(filter.ToString(), replacementRegexPattern, match =>
            {
                var key = match.Groups["key"].Value;
                var type = match.Groups["type"].Value.ToLower();
                var math = match.Groups["math"].Value;
                var value = match.Groups["value"].Value;

                string resourceFormat;

                switch (type)
                {
                    case "string":
                        resourceFormat = "{0} {1} '{2}'";
                        break;
                    case "guid":
                        resourceFormat = "{0} {1} guid'{2}'";
                        break;
                    default:
                        resourceFormat = "{0} {1} {2}";
                        break;
                }

                return string.Format(resourceFormat, key, math, value);
            });

            var convert = new CriteriaToODataFilterConverter();
            return convert.Convert(query);
        }

        #endregion
    }
}