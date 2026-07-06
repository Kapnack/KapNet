using ImageCampus.ToolBox.Events;

namespace Net
{
    public struct ServerAssignedEvent : IEvent
    {
        public string ip;
        public int port;

        public void Assign(params object[] parameters)
        {
            ip = (string)parameters[0];
            port = (int)parameters[1];
        }

        public void Reset()
        {
            ip = string.Empty;
            port = 0;
        }
    }
}
