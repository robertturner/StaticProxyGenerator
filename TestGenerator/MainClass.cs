using StaticProxyInterfaces;
using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;

namespace TestGenerator
{
    public class MainClass
    {


		public static void Main(string[] args)
        {


            static object handler(MethodInfo method, object[] args, Type[] genericArguments)
            {
                switch (method.Name)
                {
                    case "GetStr":
                        return method.Name + " here goes! " + (string)args[0];
                    case "Add5To":
                        return (int)args[0] + 5;
                    case "AGenMethod":
                        return ((int)args[0] + 10).ToString();
                    case nameof(IAnIfceToProxy.StartServiceByName):
                        return Task.FromResult(ServiceStartResult.Val1);
                    default:
                        throw new MissingMethodException("Unrecognised method: " + method.Name);
                }
            }

            var proxyIfce = ProxyGeneratorHelpers.InstantiateProxy<IAnIfceToProxy>(handler);

            Console.WriteLine(proxyIfce.GetStr("An arg"));
			Console.WriteLine($"Adding 5 result: {proxyIfce.Add5To(6)}");
			Console.WriteLine($"Gen method: {proxyIfce.AGenMethod<string>(6)}");
            Console.WriteLine($"Task method: {proxyIfce.StartServiceByName(null, 0).Result}");

        }
    }
}
