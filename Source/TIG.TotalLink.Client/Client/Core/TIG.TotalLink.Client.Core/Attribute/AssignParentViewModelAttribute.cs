using System;

namespace TIG.TotalLink.Client.Core.Attribute
{
    /// <summary>
    /// Apply this attribute to an ObservableCollection of viewmodels in a class which inherits from EntityViewModelBase to automatically
    /// assign/clear the ISupportParentViewModel.ParentViewModel property when items are added/removed from the collection.
    /// For this to work correctly the collection must be assigned before the constructor is called.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AssignParentViewModelAttribute : System.Attribute
    {
    }
}
