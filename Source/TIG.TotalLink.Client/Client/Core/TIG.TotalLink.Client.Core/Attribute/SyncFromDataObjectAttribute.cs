using System;

namespace TIG.TotalLink.Client.Core.Attribute
{
    /// <summary>
    /// Apply this attribute to an ObservableCollection of viewmodels in a class which inherits from EntityViewModelBase to automatically
    /// sync the collection with the related property on the datamodel.
    /// For this to work correctly the collection must be assigned before the constructor is called.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SyncFromDataObjectAttribute : System.Attribute
    {
    }
}
