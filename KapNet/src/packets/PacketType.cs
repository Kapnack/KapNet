namespace KapNet
{
    public enum PacketType : int
    {
        Handshake,
        Acknowledgement,
        ClientJoined,
        ClientLeft,
        Position,
        Spawn,
        Destroy,
        RaceState,
        Ping,
        Data,
        ServerShutDown,
        ConnectToServer
    }
}