using System;
using System.Collections.Generic;
using System.Text;

namespace StaticProxyInterfaces
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class StaticProxyGenerateAttribute : Attribute
    { 
        public StaticProxyGenerateAttribute() { }
        public StaticProxyGenerateAttribute(params Type[] additionalInterfaces) => AdditionalInterfacesToProxy = additionalInterfaces;

        public Type[] AdditionalInterfacesToProxy { get; }
    }
}
