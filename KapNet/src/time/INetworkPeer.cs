using System.Net;

namespace KapNet.src.time
{
    internal interface INetworkPeer
    {
        bool IsConnected { get; }
        void SendRaw(byte[] data, IPEndPoint ip);
        void SendRaw(byte[] data);
    }
}
