namespace KapNet
{
    public enum PacketType : int
    {
        Handshake,
        Acknowledgement,
        ClientJoined,
        ClientLeft,
        vInt,
        vShort,
        vfloat,
        vDouble,
        vLong,
        vUInt,
        Ping,
        ServerShutDown,
        ConnectToServer
    }
}