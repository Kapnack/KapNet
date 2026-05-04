namespace KapNet
{
    internal static class PacketLayout
    {
        public const int PacketTypeOffSet = 0;
        public const int PacketIDOffSet = sizeof(PacketType);
        public const int PacketMetaDataOffSet = PacketIDOffSet + sizeof(uint);
        public const int PacketPayloadOffSet = PacketMetaDataOffSet + sizeof(uint);
        public const int CheckSum2EndOffSet = sizeof(int);
        public const int CheckSum1EndOffSet = CheckSum2EndOffSet * 2;

        public const int PacketConstSpace = sizeof(PacketType) + sizeof(uint) + sizeof(PacketMetaData) + sizeof(int) * 2;
    }
}
