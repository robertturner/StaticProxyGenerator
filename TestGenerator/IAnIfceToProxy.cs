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

    [StaticProxyGenerate(typeof(IDisposable), typeof(ICloneable))]
    public interface IAnIfceToProxy : IBaseIfce
    {

        string GetStr(string source);

        int Add5To(int startVal);

        string AGenMethod<T, T2>(int aVal) where T : class, IDisposable where T2 : notnull;
        TStruct AGenMethod_TStruct<TStruct>(int aVal) where TStruct : struct;

        Task<ServiceStartResult> StartServiceByName(string name, uint flags);


    }

    [StaticProxyGenerate]
    public interface IAGenIfceToProxy<T> : ICloneable where T : class, IDisposable
    {

        string GetStr(string source);

        int Add5To(int startVal);

        T RetT(int startVal);


        string AGenMethod<T2>(int aVal) where T2 : notnull;
        TStruct AGenMethod_TStruct<TStruct>(int aVal) where TStruct : struct;

        Task<ServiceStartResult> StartServiceByName(string name, uint flags);


    }


}
