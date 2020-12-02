using StaticProxyInterfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TestGenerator
{

    public interface IBaseIfce
    {
        string BaseMethod(string arg);
    }

    public enum ServiceStartResult
    {
        Val1,
        Val2
    }

    [StaticProxyGenerate]
    public interface IAnIfceToProxy : IBaseIfce, IDisposable
    {

        string GetStr(string source);

        int Add5To(int startVal);

        string AGenMethod<T, T2>(int aVal) where T : class, IDisposable where T2 : notnull;
        TStruct AGenMethod_TStruct<TStruct>(int aVal) where TStruct : struct;

        Task<ServiceStartResult> StartServiceByName(string name, uint flags);


    }
}
