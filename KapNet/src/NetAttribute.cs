using System;

[Flags]
public enum PacketMetaData : int
{
    None = 1 << 0,
    Crytical = 1 << 1 | Reliable | Ordenable | Encrypted,
    Reliable = 1 << 2,
    Encrypted = 1 << 3,
    Ordenable = 1 << 4 | Reliable
}

public class NetAttribute : Attribute
{
    public uint id;
    public PacketMetaData metaData;

    public NetAttribute(uint id) { this.id = id; metaData = PacketMetaData.None; }
    public NetAttribute(uint id, PacketMetaData metaData) { this.id = id; this.metaData = metaData; }
}
