using System;
using System.Collections.Generic;
using System.Text;

namespace StaticProxyInterfaces
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class StaticProxyGenerateAttribute : Attribute
    { }
}
