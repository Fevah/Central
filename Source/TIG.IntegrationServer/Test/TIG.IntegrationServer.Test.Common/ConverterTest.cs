using NUnit.Framework;
using TIG.IntegrationServer.Common.Converter;

namespace TIG.IntegrationServer.Test.Common
{
    [TestFixture]
    public class ConverterTest
    {
        [Test]
        public void CriteriaToODataFilterConverterTest()
        {
            var criteriaFilter = @"[Price] > 50.0m And [Price] Between(80.0m, 150.0m) And StartsWith([ProductName], 'A') And EndsWith([ProductName], 'e') And [ProductName] Like '%p%' And [ProductName] In ('Apple') And Not IsNullOrEmpty([ProductName]) And Contains([ProductName], 'l') And [Quantity] >= 20 And [Quantity] <= 500 And [Price] < 300.0m";
            var expectOdataFilter = @"Price gt 50.0m And (Price>=80.0m and Price<=150.0m) And StartsWith(ProductName, 'A') And EndsWith(ProductName, 'e') And substringof(p, ProductName) And ProductName eq 'Apple' And Not ProductName eq '' And substringof('l', ProductName) And Quantity ge 20 And Quantity le 500 And Price lt 300.0m";
            var converter = new CriteriaToODataFilterConverter();
            var result = converter.Convert(criteriaFilter);
            Assert.AreEqual(result, expectOdataFilter);
        }
    }
}
