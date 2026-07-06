using System;

[Flags]
public enum PacketMetaData : int
{
    None = 1 << 0,
    Descartable = None,
    Crytical = 1 << 1 | Reliable | Ordenable | Encrypted,
    Reliable = 1 << 2,
    Encrypted = 1 << 3,
    Ordenable = 1 << 4 | Reliable
}