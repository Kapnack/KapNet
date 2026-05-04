using System;
using System.Net;

public class PacketAwaitingResponce
{
    public byte[] data;
    public uint packetID;
    public IPEndPoint ipEndPoint;
    public DateTime lastTimeSent;

    public PacketAwaitingResponce(byte[] data, uint packetID, IPEndPoint ipEndPoint, DateTime lastTimeSent)
    {
        this.data = data;
        this.packetID = packetID;
        this.ipEndPoint = ipEndPoint;
        this.lastTimeSent = lastTimeSent;
    }
}
