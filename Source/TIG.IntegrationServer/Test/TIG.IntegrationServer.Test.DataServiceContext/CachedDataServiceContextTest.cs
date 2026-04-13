using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Xml;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;
using NUnit.Framework;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.Entity;
using TIG.IntegrationServer.Plugin.Core.ServiceContext;

namespace TIG.IntegrationServer.Test.DataServiceContext
{
    [TestFixture]
    public class CachedDataServiceContextTest
    {
        [Test]
        public string GetAuthenticationToken()
        {
            const string address = "http://localhost:42101/AuthenticationMethodService.svc/";
            const string userName = "admin";
            const string password = "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=";

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

            var client = new AuthenticationServiceContext(binding, endpoint);

            return client.Login(userName, password);
        }

        [Test]
        public void GetPostcodes()
        {
            const string address = "http://localhost:42001/MainDataService.svc/";
            var token = GetAuthenticationToken();

            var context = new CachedDataServiceContext(address, token);
            var filter = new BinaryOperator(new QueryOperand("Region", "N0", DBColumnType.String), new ParameterValue { Value = "Sydney" }, BinaryOperatorType.Equal);
            var fields = new List<EntityFieldInfo>
            {
               new EntityFieldInfo {Name="Oid", Type = typeof(Guid)},
               new EntityFieldInfo {Name="Code", Type = typeof(string)},
               new EntityFieldInfo {Name="Name", Type = typeof(string)},
               new EntityFieldInfo {Name = "State"}
            };

            var joinEntityInfo = new EntityJoinInfo
            {
                EntityName = "State",
                JoinKey = "Oid",
                FieldInfos = new List<EntityFieldInfo>
                {
                    new EntityFieldInfo{Name = "Code",Type = typeof(string)},
                    new EntityFieldInfo{Name = "Name",Type = typeof(string)},
                }
            };

            var result = context.GetAll("Postcode", fields, filter, joinEntityInfo);
        }

        [Test]
        public void GetPostcode()
        {
            const string address = "http://localhost:42001/MainDataService.svc/";
            var token = GetAuthenticationToken();

            var context = new CachedDataServiceContext(address, token);
            var filter = new BinaryOperator(new QueryOperand("Oid", "NO", DBColumnType.Guid), new ParameterValue { Value = Guid.Parse("678B3B84-057E-4E04-9FF0-0015491D9FB6") }, BinaryOperatorType.Equal);
            var fields = new List<EntityFieldInfo>
            {
               new EntityFieldInfo {Name="Oid", Type = typeof(Guid)},
               new EntityFieldInfo {Name="Code", Type = typeof(string)},
               new EntityFieldInfo {Name="Name", Type = typeof(string)}
            };

            var result = context.GetOne("Postcode", fields, filter);

            Assert.IsNotNull(result);
            Assert.AreEqual(result["Name"], "Kyle Bay");
            Assert.AreEqual(result["Code"], "2221");
        }

        [Test]
        public void DeletePostcode()
        {
            const string address = "http://localhost:42001/MainDataService.svc/";
            var token = GetAuthenticationToken();

            var context = new CachedDataServiceContext(address, token);

            var deleteFilter = new BinaryOperator(new QueryOperand("Oid", null, DBColumnType.Guid), new ParameterValue { Value = Guid.Parse("408B7182-03F3-4C29-A117-4168DDBF3E15") }, BinaryOperatorType.Equal);

            var success = context.Delete("Postcode", deleteFilter);
            Assert.IsTrue(success);

            var filter = new BinaryOperator(new QueryOperand("Oid", "NO", DBColumnType.Guid), new ParameterValue { Value = Guid.Parse("408B7182-03F3-4C29-A117-4168DDBF3E15") }, BinaryOperatorType.Equal);
            var fields = new List<EntityFieldInfo>
            {
               new EntityFieldInfo {Name="Oid", Type = typeof(Guid)},
               new EntityFieldInfo {Name="Code", Type = typeof(string)},
               new EntityFieldInfo {Name="Name", Type = typeof(string)}
            };

            var result = context.GetOne("Postcode", fields, filter);

            Assert.IsNull(result);
        }

        [Test]
        public void CreatePostcode()
        {
            const string address = "http://localhost:42001/MainDataService.svc/";
            var token = GetAuthenticationToken();

            var context = new CachedDataServiceContext(address, token);
            var fields = new List<EntityFieldInfo>
            {
               new EntityFieldInfo {Name="Oid", Type = typeof(Guid)},
               new EntityFieldInfo {Name="Code", Type = typeof(string)},
               new EntityFieldInfo {Name="Name", Type = typeof(string)}
            };

            var id = Guid.NewGuid();

            var newEntity = new EntityBase();
            newEntity["Oid"] = id;
            newEntity["Code"] = "010";
            newEntity["Name"] = "Bei jing";

            context.Create("Postcode", fields, newEntity);

            var filter = new BinaryOperator(new QueryOperand("Oid", "NO", DBColumnType.Guid), new ParameterValue { Value = id }, BinaryOperatorType.Equal);

            var result = context.GetOne("Postcode", fields, filter);

            Assert.IsNotNull(result);
            Assert.AreEqual(result["Oid"], id);
            Assert.AreEqual(result["Name"], "Bei jing");
            Assert.AreEqual(result["Code"], "010");
        }

        [Test]
        public void UpdatePostcode()
        {
            const string address = "http://localhost:42001/MainDataService.svc/";
            var token = GetAuthenticationToken();

            var context = new CachedDataServiceContext(address, token);
            var filter = new BinaryOperator(new QueryOperand("Oid", "NO", DBColumnType.Guid), new ParameterValue { Value = Guid.Parse("678B3B84-057E-4E04-9FF0-0015491D9FB6") }, BinaryOperatorType.Equal);
            var fields = new List<EntityFieldInfo>
            {
               new EntityFieldInfo {Name="Oid", Type = typeof(Guid)},
               new EntityFieldInfo {Name="Code", Type = typeof(string)},
               new EntityFieldInfo {Name="Name", Type = typeof(string)}
            };

            var result = context.GetOne("Postcode", fields, filter);

            Assert.IsNotNull(result);
            Assert.AreEqual(result["Name"], "Wuhan");
            Assert.AreEqual(result["Code"], "027");

            result["Name"] = "Kyle Bay";
            result["Code"] = "2221";

            var updateFields = new List<EntityFieldInfo>
            {
               new EntityFieldInfo {Name="Code", Type = typeof(string)},
               new EntityFieldInfo {Name="Name", Type = typeof(string)}
            };

            var updateFilter = new BinaryOperator(new QueryOperand("Oid", null, DBColumnType.Guid), new ParameterValue { Value = Guid.Parse("678B3B84-057E-4E04-9FF0-0015491D9FB6") }, BinaryOperatorType.Equal);

            var updateResult = context.Update("Postcode", updateFields, result, updateFilter);

            Assert.IsTrue(updateResult);

            result = context.GetOne("Postcode", fields, filter);

            Assert.IsNotNull(result);
            Assert.AreEqual(result["Name"], "Kyle Bay");
            Assert.AreEqual(result["Code"], "2221");
        }
    }
}