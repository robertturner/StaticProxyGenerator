using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace StaticProxyInterfaces
{
    public delegate object InterceptorHandler(MethodInfo method, object[] args, Type[] genericArguments);
    
}
