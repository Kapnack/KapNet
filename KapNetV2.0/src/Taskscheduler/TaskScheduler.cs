using ImageCampus.ToolBox.Services;
using KapNetV2._0.src.Interface;
using System;
using System.Collections.Generic;

namespace Net
{
    public sealed class TaskScheduler : IService, ITickable
    {
        public bool IsPersistance => false;

        public sealed class ScheduledCall 
        {
            public readonly Action callback;
            public float remainingTime;

            public ScheduledCall(Action callback, float remainingTime)
            {
                this.callback = callback;
                this.remainingTime = remainingTime;
            }
        }

        private readonly List<ScheduledCall> scheduledCalls;

        public TaskScheduler()
        {
            this.scheduledCalls = new List<ScheduledCall>();
        }

        public void Schedule(Action callback, float remainingTime) 
        {
            scheduledCalls.Add(new ScheduledCall(callback, remainingTime));
        }

        public void Tick(float deltaTime)
        {
            for (int i = scheduledCalls.Count - 1; i >= 0; i--)
            {
                ScheduledCall call = scheduledCalls[i];
                call.remainingTime -= deltaTime;

                if (call.remainingTime <= 0.0f)
                {
                    scheduledCalls.RemoveAt(i);
                    call.callback.Invoke();
                }
            }
        }

    }
}
