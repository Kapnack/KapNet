using System;

public class NetAttribute : Attribute
{
    public uint id;
    public PacketMetaData metaData;

    public NetAttribute(uint id)
    {
        this.id = id;
        metaData = PacketMetaData.None;
    }

    public NetAttribute(uint id, PacketMetaData metaData)
    {
        this.id = id;
        this.metaData = metaData;
    }
}
