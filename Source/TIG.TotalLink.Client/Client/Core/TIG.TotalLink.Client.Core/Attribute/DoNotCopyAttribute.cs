using System;

namespace TIG.TotalLink.Client.Core.Attribute
{
    /// <summary>
    /// Apply this attribute to a property to specify that the property should not be copied when the object is cloned.
    /// Use this attribute when the property value will be automatically assigned elsewhere during a cloning operation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DoNotCopyAttribute : System.Attribute
    {
    }
}
