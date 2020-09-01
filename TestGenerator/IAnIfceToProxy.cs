﻿using StaticProxyInterfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TestGenerator
{

    public enum ServiceStartResult
    {
        Val1,
        Val2
    }

    [StaticProxyGenerate]
    public interface IAnIfceToProxy
    {

        string GetStr(string source);

        int Add5To(int startVal);

        string AGenMethod<T>(int aVal) where T : class;

        Task<ServiceStartResult> StartServiceByName(string name, uint flags);


    }
}
