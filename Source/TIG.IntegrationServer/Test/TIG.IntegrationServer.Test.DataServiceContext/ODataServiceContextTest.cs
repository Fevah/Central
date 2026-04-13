using System;
using System.Collections.Generic;
using System.Xml.Linq;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;
using NUnit.Framework;
using RestSharp;
using TIG.IntegrationServer.Plugin.Agent.TotalLinkAgent.Entity;
using TIG.IntegrationServer.Plugin.Core.Entity;
using TIG.IntegrationServer.Plugin.Core.ServiceContext;

namespace TIG.IntegrationServer.Test.DataServiceContext
{
    [TestFixture]
    public class ODataServiceContextTest
    {
        private const string EntityName = "Customer";

        private ODataServiceContext GetDataServiceContext()
        {
            // http://localhost:4301/Admin.svc/Login?userName='admin'&password='jGl25bVBBBW96Qi9Te4V37Fnqchz%2FEu4qB9vKrRIqRg%3D'

            const string baseUri = "http://localhost:4301/Admin.svc/Login";
            const string userName = "'admin'";
            const string password = "'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg='";

            var client = new RestClient(baseUri);
            var request = new RestRequest(Method.GET)
            {
                RequestFormat = DataFormat.Json
            };
            request.AddQueryParameter("userName", userName);
            request.AddQueryParameter("password", password);

            var response = client.Execute(request);
            var ticket = string.Empty;
            if (!string.IsNullOrWhiteSpace(response.Content))
            {
                var xDocument = XDocument.Parse(response.Content);
                if (xDocument.Root != null)
                    ticket = xDocument.Root.Value;
            }

            const string uri = "http://localhost:4303/Crm.svc";
            var dataServiceContext = new ODataServiceContext(uri, ticket, null);

            return dataServiceContext;
        }

        [Test]
        public void Create()
        {
            const string propertyNameKey = "Email";
            const string propertyNameExpectValue = "Jack";
            const string propertyAddressKey = "Webpage";
            const string propertyAddressExpectValue = "LA Street 1";

            var dataServiceContext = GetDataServiceContext();
            var customer = new TotalLinkEntity();
            customer[propertyNameKey] = propertyNameExpectValue;
            customer[propertyAddressKey] = propertyAddressExpectValue;
            var retrivedCustomer = dataServiceContext.Create(EntityName, new List<EntityFieldInfo>(), customer);
            Assert.AreEqual(retrivedCustomer[propertyNameKey], propertyNameExpectValue);
            Assert.AreEqual(retrivedCustomer[propertyAddressKey], propertyAddressExpectValue);
        }

        [Test]
        public void Update()
        {
            const string propertyIdKey = "Oid";
            const string propertyNameKey = "Email";
            const string propertyNameValue = "Jack";
            const string propertyAddressKey = "Webpage";
            const string propertyAddressValue = "LA Street 1";

            const string propertyNameExpectValue = "Jack Ma";
            const string propertyAddressExpectValue = "LA Street 2";

            var dataServiceContext = GetDataServiceContext();
            var customer = new TotalLinkEntity();
            customer[propertyNameKey] = propertyNameValue;
            customer[propertyAddressKey] = propertyAddressValue;

            var retrivedCustomer = dataServiceContext.Create("Customer", new List<EntityFieldInfo>(), customer);
            Assert.AreEqual(retrivedCustomer[propertyNameKey], propertyNameValue);
            Assert.AreEqual(retrivedCustomer[propertyAddressKey], propertyAddressValue);

            var retriveId = retrivedCustomer[propertyIdKey];

            customer[propertyIdKey] = retriveId;
            customer[propertyNameKey] = propertyNameExpectValue;
            customer[propertyAddressKey] = propertyAddressExpectValue;

            var filter = new BinaryOperator(
                new QueryOperand(propertyIdKey, null, DBColumn.GetColumnType(typeof(Guid))),
                new ParameterValue { Value = retriveId }, BinaryOperatorType.Equal);

            var isSuccessToUpdated = dataServiceContext.Update(EntityName, new List<EntityFieldInfo>(), customer, filter);

            Assert.IsTrue(isSuccessToUpdated);

            var expectCustomer = dataServiceContext.GetOne(EntityName, new List<EntityFieldInfo>(), filter);

            Assert.AreEqual(expectCustomer[propertyNameKey], propertyNameExpectValue);
            Assert.AreEqual(expectCustomer[propertyAddressKey], propertyAddressExpectValue);
        }

        [Test]
        public void GetOne()
        {
            const string propertyIdKey = "Oid";
            const string propertyNameKey = "Email";
            const string propertyNameExpectValue = "Jack";
            const string propertyAddressKey = "Webpage";
            const string propertyAddressExpectValue = "LA Street 1";

            var dataServiceContext = GetDataServiceContext();
            var customer = new TotalLinkEntity();
            customer[propertyNameKey] = propertyNameExpectValue;
            customer[propertyAddressKey] = propertyAddressExpectValue;
            var retrivedCustomer = dataServiceContext.Create(EntityName, new List<EntityFieldInfo>(), customer);

            var retriveId = retrivedCustomer[propertyIdKey];
            var filter = new BinaryOperator(
                            new QueryOperand(propertyIdKey, null, DBColumn.GetColumnType(typeof(Guid))),
                            new ParameterValue { Value = retriveId }, BinaryOperatorType.Equal);

            var expectCustomer = dataServiceContext.GetOne(EntityName, new List<EntityFieldInfo>(), filter);

            Assert.AreEqual(expectCustomer[propertyIdKey], retrivedCustomer[propertyIdKey]);
            Assert.AreEqual(expectCustomer[propertyNameKey], retrivedCustomer[propertyNameKey]);
            Assert.AreEqual(expectCustomer[propertyAddressKey], retrivedCustomer[propertyAddressKey]);
        }


        [Test]
        public void Delete()
        {
            const string propertyIdKey = "Oid";
            const string propertyNameKey = "Email";
            const string propertyNameExpectValue = "Jack";
            const string propertyAddressKey = "Webpage";
            const string propertyAddressExpectValue = "LA Street 1";

            var dataServiceContext = GetDataServiceContext();
            var customer = new TotalLinkEntity();
            customer[propertyNameKey] = propertyNameExpectValue;
            customer[propertyAddressKey] = propertyAddressExpectValue;
            var retrivedCustomer = dataServiceContext.Create(EntityName, new List<EntityFieldInfo>(), customer);

            var retriveId = retrivedCustomer[propertyIdKey];
            var filter = new BinaryOperator(
                            new QueryOperand(propertyIdKey, null, DBColumn.GetColumnType(typeof(Guid))),
                            new ParameterValue { Value = retriveId }, BinaryOperatorType.Equal);

            var isSuccess = dataServiceContext.Delete(EntityName, filter);

            Assert.IsTrue(isSuccess);

            var expectCustomer = dataServiceContext.GetOne(EntityName, new List<EntityFieldInfo>(), filter);

            Assert.IsNull(expectCustomer);
        }

        [Test]
        public void TestODataFilterBuilder()
        {
            var dataServiceContext = GetDataServiceContext();
            var filter = new BinaryOperator(
                new QueryOperand("Oid", null, DBColumn.GetColumnType(typeof(Guid))),
                new ParameterValue { Value = Guid.NewGuid() }, BinaryOperatorType.Equal);

            CriteriaOperator condtion = new UnaryOperator(UnaryOperatorType.IsNull, new QueryOperand("GCRecord", "N0", DBColumnType.Int32));

            var condidtion = CriteriaOperator.And(filter, condtion);

            var query = dataServiceContext.ConverterToODataFilter(condidtion);
        }
    }
}
