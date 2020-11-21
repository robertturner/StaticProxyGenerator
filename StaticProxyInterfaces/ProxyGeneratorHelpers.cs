using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace StaticProxyInterfaces
{
    public static class ProxyGeneratorHelpers
    {
		public static Type RetrieveProxyType(Type interfaceType)
		{
			if (interfaceType == null)
				throw new ArgumentNullException(nameof(interfaceType));
			if (!interfaceType.IsInterface)
				throw new ArgumentException($"Type {interfaceType.FullName} is not an interface type!");
			var text = interfaceType.FullName + "Implementation";
			var type = interfaceType.GetTypeInfo().Assembly.GetType(text);
			if (type == null)
				throw new InvalidOperationException($"There is no auto-generated proxy for interface {interfaceType.FullName}. Ensure interface has [StaticProxyGenerate] attribute and that StaticProxyInterfaces is referenced as an Analyzer");
			return type;
		}

		static readonly ConcurrentDictionary<Type, Func<InterceptorHandler, object>> activatorCache = new ConcurrentDictionary<Type, Func<InterceptorHandler, object>>();
        public static TInterface InstantiateProxy<TInterface>(InterceptorHandler interceptor) => (TInterface)InstantiateProxy(typeof(TInterface), interceptor);
        public static object InstantiateProxy(Type interfaceType, InterceptorHandler interceptor)
        {
            if (interfaceType == null)
                throw new ArgumentNullException(nameof(interfaceType));
            return activatorCache.GetOrAdd(interfaceType, _ =>
            {
                var classType = RetrieveProxyType(interfaceType);
                var ctor = classType.GetConstructor(new Type[] { typeof(InterceptorHandler) });
                var handlerParam = Expression.Parameter(typeof(InterceptorHandler), nameof(interceptor));
                var createInst = Expression.New(ctor, handlerParam);
                var expr = Expression.Lambda(typeof(Func<InterceptorHandler, object>), createInst, handlerParam);
                return (Func<InterceptorHandler, object>)expr.Compile();
            })(interceptor);
        }
    }
}
