using System;
using System.Collections.Concurrent;

namespace ImageCampus.ToolBox.Pool
{
    public sealed class ConcurrentPool
    {
        private readonly ConcurrentDictionary<Type, ConcurrentStack<IResettable>> concurrentPool =
            new ConcurrentDictionary<Type, ConcurrentStack<IResettable>>();

        public ResettableType Get<ResettableType>(params object[] parameters) where ResettableType : IResettable 
        {
            Type ressetteableType = typeof(ResettableType);
            if (!concurrentPool.ContainsKey(ressetteableType))
                concurrentPool.TryAdd(ressetteableType, new ConcurrentStack<IResettable>());

            ResettableType value;
            if (concurrentPool[ressetteableType].Count > 0)
            {
                concurrentPool[ressetteableType].TryPop(out IResettable resettable);
                value = (ResettableType)resettable;
            }
            else
            {
                value = (ResettableType)Activator.CreateInstance(ressetteableType);
            }

            value.Assign(parameters);
            return value;
        }

        public void Release<ResettableType>(ResettableType ressettable) where ResettableType : IResettable 
        {
            ressettable.Reset();
            concurrentPool[typeof(ResettableType)].Push(ressettable);
        }
    }
}
