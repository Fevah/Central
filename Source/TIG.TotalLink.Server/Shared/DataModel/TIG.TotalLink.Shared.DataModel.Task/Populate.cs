using System.Linq;
using System.Runtime.CompilerServices;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Shared.DataModel.Task
{
    public class Populate : IPopulateDataStore
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PopulateDataStore(IDataLayer dataLayer)
        {
            using (var uow = new UnitOfWork(dataLayer))
            {
                // If there are no test tasks yet, create some
                if (!new XPQuery<Task>(uow).Any())
                {
                    // Create 100 tasks at the root level
                    for (var i = 0; i < 100; i++)
                    {
                        new Task(uow)
                        {
                            Name = string.Format("Task {0:00}", i)
                        };
                    }
                    uow.CommitChanges();

                    // Create 100 tasks at level 1, for 10 of the root level tasks
                    for (var i = 0; i < 10; i++)
                    {
                        var parent = new XPQuery<Task>(uow).FirstOrDefault(l => l.Name == string.Format("Task {0:00}", i));
                        for (var j = 0; j < 100; j++)
                        {
                            new Task(uow)
                            {
                                Name = string.Format("Task {0:00}-{1:00}", i, j),
                                Parent = parent
                            };
                        }
                    }
                    uow.CommitChanges();

                    // Create 100 tasks at level 3, for 10 of the level 2 tasks
                    for (var i = 0; i < 10; i++)
                    {
                        for (var j = 0; j < 10; j++)
                        {
                            var parent = new XPQuery<Task>(uow).FirstOrDefault(l => l.Name == string.Format("Task {0:00}-{1:00}", i, j));
                            for (var k = 0; k < 100; k++)
                            {
                                new Task(uow)
                                {
                                    Name = string.Format("Task {0:00}-{1:00}-{2:00}", i, j, k),
                                    Parent = parent
                                };
                            }
                        }
                    }
                    uow.CommitChanges();
                }
            }
        }
    }
}
