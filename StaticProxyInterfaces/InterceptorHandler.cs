using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace StaticProxyInterfaces
{
    public delegate object InterceptorHandler(object instance, MethodInfo method, object[] args, Type[] genericArguments);
}
