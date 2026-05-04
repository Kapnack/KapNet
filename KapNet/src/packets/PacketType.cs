namespace KapNet
{
    public enum PacketType : int
    {
        Handshake,
        Acknowledgement,
        SendID,
        ClientJoined,
        ClientLeft,
        Ping,
        Data,
        DisconnectClient,
        ServerShutDown,
        ConnectToServer
    }
}