using ImageCampus.ToolBox.Events;

namespace Net
{
    public struct HandShackeRecievedEvent : IEvent
    {
        public uint clientID;

        public void Assign(params object[] parameters)
        {
            clientID = (uint)parameters[0];
        }

        public void Reset()
        {
            clientID = default(uint);
        }
    }
}
