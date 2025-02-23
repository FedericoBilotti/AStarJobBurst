using System;
using System.Collections.Generic;
using Utilities;

// In a game this will be in a bootstrapper scene.
public class ServiceLocator : Singleton<ServiceLocator>
{
    private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

    public void RegisterService<T>(T service)
    {
        Type type = typeof(T);

        _services.TryAdd(type, service);
    }

    public T GetService<T>()
    {
        if (_services.TryGetValue(typeof(T), out object service))
        {
            return (T)service;
        }
        
        throw new Exception($"Service of type {typeof(T)} not found.");
    }
}