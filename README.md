# StaticProxyGenerator
Interface proxy generator.
At compile time creates class that implements target interface. Instantiation of the generated class accepts an InterceptorHandler which is called for all method calls.

## Example

```
public interface IMyIfce
{
  Task<string> AsyncMethod(string arg1);
  Task MethodWithNoReturn(int arg1, string arg2);
  void SyncMethodNoArgs();
}
```
Create proxy of interface:
```
object interceptionHandler(MethodInfo method, object[] args, Type[] genericArguments)
{
    switch (method.name)
    {
        case nameof(IMyIfce.AsyncMethod): 
            return Task.FromResult((string)args[0]);
        ... 
    }
}

// Get instance of proxy
IMyIfce ifceProxy = ProxyGeneratorHelpers.InstantiateProxy<IMyIfce>(interceptionHandler);
```
Super happy place:
```
string result = await ifceProxy.AsyncMethod("an arg");
...
await ifceProxy.MethodWithNoReturn(4, "arg2");
...
ifceProxy.SyncMethodNoArgs();
```
