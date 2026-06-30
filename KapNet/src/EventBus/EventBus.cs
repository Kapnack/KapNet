using ImageCampus.ToolBox.Pool;
using ImageCampus.ToolBox.Services;
using System;
using System.Collections.Generic;

namespace ImageCampus.ToolBox.Events
{
    public sealed class EventBus : IService
    {
        public delegate void EventCallback<EventType>(in EventType callback) where EventType : struct, IEvent;
        public bool IsPersistance => false;

        private readonly ConcurrentPool eventPool = new ConcurrentPool();
        private readonly Dictionary<Type, List<Delegate>> subscribers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<EventType>(EventCallback<EventType> callback) where EventType : struct, IEvent
        {
            Type eventType = typeof(EventType);
            if (!subscribers.ContainsKey(eventType))
                subscribers.Add(eventType, new List<Delegate>());
            subscribers[eventType].Add(callback);
        }

        public EventCallback<EventType> SubscribeAndReturn<EventType>(EventCallback<EventType> callback) where EventType : struct, IEvent
        {
            Subscribe(callback);
            return callback;
        }

        public void Unsubscribe<EventType>(EventCallback<EventType> callback) where EventType : struct, IEvent
        {
            Type eventType = typeof(EventType);
            if (subscribers.TryGetValue(eventType, out List<Delegate> subscriptions))
                subscriptions.Remove(callback);
        }

        public void Raise<EventType>(params object[] parameters) where EventType : struct, IEvent
        {
            Type eventType = typeof(EventType);
            EventType raisingEvent = eventPool.Get<EventType>(parameters);
            if (subscribers.TryGetValue(eventType, out List<Delegate> subscriptions))
            {
                foreach (Delegate callback in subscriptions)
                {
                    ((EventCallback<EventType>)callback)?.Invoke(raisingEvent);
                }
            }
            eventPool.Release(raisingEvent);
        }

        public void Clear()
        {
            subscribers.Clear();
        }
    }
}
