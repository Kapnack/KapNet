using System;

namespace KapNet
{
    [Flags]
    public enum PacketMetaData : int
    {
        None = 1 << 0,
        Crytical = 1 << 1,
        Reliable = 1 << 2,
        Encrypted = 1 << 3,
        Ordenable = 1 << 4
    }
}