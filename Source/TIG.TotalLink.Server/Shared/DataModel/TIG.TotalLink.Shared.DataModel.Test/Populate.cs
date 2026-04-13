using System;
using System.Linq;
using System.Runtime.CompilerServices;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Test;

namespace TIG.TotalLink.Shared.DataModel.Test
{
    public class Populate : IPopulateDataStore
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PopulateDataStore(IDataLayer dataLayer)
        {
            using (var uow = new UnitOfWork(dataLayer))
            {
                // If there are no test items yet, create some
                if (!new XPQuery<TestObjectLookUp>(uow).Any())
                {
                    for (var i = 0; i < 100; i++)
                    {
                        new TestObjectLookUp(uow)
                        {
                            Name = string.Format("LookUp {0:00}", i),
                            AltName = string.Format("AltLookUp {0:00}", i)
                        };
                    }
                    uow.CommitChanges();
                }

                if (!new XPQuery<TestObject>(uow).Any())
                {
                    for (var i = 0; i < 100; i++)
                    {
                        var lookup = new XPQuery<TestObjectLookUp>(uow).FirstOrDefault(l => l.Name == string.Format("LookUp {0:00}", i));

                        new TestObject(uow)
                        {
                            Text = string.Format("Text {0:00}", i),
                            SpinInt = i,
                            SpinLong = i,
                            Checkbox = (i % 2 == 0),
                            Label = string.Format("Label {0:00}", i),
                            Password = string.Format("Password {0:00}", i),
                            Progress = i * 10,
                            RichText = null,
                            DateTime = DateTime.Now,
                            HyperLink = (i == 0 ? "someone@somewhere.com" : "http://www.totalimagegroup.com.au"),
                            IncrementingTime = i * 10,
                            Comments = null,
                            LookUp = lookup,
                            AltLookUp = lookup,
                            Memo = null,
                            Currency = (decimal)i + ((decimal)i * 0.011m),
                            Decimal = (decimal)i + ((decimal)i * 0.011m),
                            Option = TestEnum.None
                        };
                    }
                    uow.CommitChanges();
                }

                if (!new XPQuery<TestObjectGrid>(uow).Any())
                {
                    for (var i = 0; i < 50; i++)
                    {
                        var testObject = new XPQuery<TestObject>(uow).FirstOrDefault(t => t.Text == string.Format("Text {0:00}", i));
                        for (var j = 0; j < i; j++)
                        {
                            new TestObjectGrid(uow)
                            {
                                Name = string.Format("Grid {0:00}-{1}", i, j),
                                TestObject = testObject
                            };
                        }
                    }
                    uow.CommitChanges();
                }
            }
        }
    }
}
