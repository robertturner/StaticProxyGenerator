using StaticProxyInterfaces;
using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;

namespace TestGenerator
{
    public class MainClass
    {
        class Dummy : IDisposable
        {
            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }

        public static void Main(string[] args)
        {


            static object handler(object instance, MethodInfo method, object[] args, Type[] genericArguments)
            {
                switch (method.Name)
                {
                    case "GetStr":
                        return method.Name + " here goes! " + (string)args[0];
                    case "Add5To":
                        return (int)args[0] + 5;
                    case "AGenMethod":
                        return ((int)args[0] + 10).ToString();
                    case nameof(IAnIfceToProxy.AGenMethod_TStruct):
                        return 4;
                    case nameof(IAnIfceToProxy.StartServiceByName):
                        return Task.FromResult(ServiceStartResult.Val1);
                    case nameof(IBaseIfce.BaseMethod):
                        return args[0];
                    case nameof(IDisposable.Dispose):
                        break;
                    case "MethodGenArg":
                        return null;
                    default:
                        throw new MissingMethodException("Unrecognised method: " + method.Name);
                }
                return null;
            }

            var proxyIfce = ProxyGeneratorHelpers.InstantiateProxy<IAnIfceToProxy>(handler);

            Console.WriteLine(proxyIfce.GetStr("An arg"));
			Console.WriteLine($"Adding 5 result: {proxyIfce.Add5To(6)}");
			Console.WriteLine($"Gen method: {proxyIfce.AGenMethod<Dummy, int>(6)}");
            Console.WriteLine($"Gen method2: {proxyIfce.AGenMethod_TStruct<int>(6)}");
            Console.WriteLine($"Task method: {proxyIfce.StartServiceByName(null, 0).Result}");
            Console.WriteLine($"Task method: {proxyIfce.BaseMethod("hi")}");

            ((IDisposable)proxyIfce).Dispose();

            var proxyGenIfce = ProxyGeneratorHelpers.InstantiateProxy<IAGenIfceToProxy<IDisposable>>(handler);
            proxyGenIfce.MethodGenArg((IDisposable)null);
            proxyGenIfce.Add5To(6);

            var proxyGen2Ifce = ProxyGeneratorHelpers.InstantiateProxy<IAGenIfceToProxy<IDisposable, int>>(handler);
            proxyGen2Ifce.RetT(5);
            proxyGen2Ifce.RetT("bob");

        }
    }
}
