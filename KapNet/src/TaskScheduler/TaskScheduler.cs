using ImageCampus.ToolBox.Services;
using System;
using System.Collections.Generic;

namespace ImageCampus.ToolBox.Scheduling
{
    public sealed class TaskScheduler : IService, ITickable
    {
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
        public bool IsPersistance => false;

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
