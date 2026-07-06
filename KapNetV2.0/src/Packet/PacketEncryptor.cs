using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

public class PacketEncryptor
{
    private readonly byte[] key;
    private readonly NetworkRandomNumberGenerator ivGenerator;

    private readonly Dictionary<uint, byte[]> generatedIvs = new Dictionary<uint, byte[]>();
    private uint nextExpectedPacketId = 0;

    public PacketEncryptor(ulong seed)
    {
        NetworkRandomNumberGenerator keyGenerator = new NetworkRandomNumberGenerator(seed);

        key = keyGenerator.GetKey();

        ivGenerator = new NetworkRandomNumberGenerator((ulong)BitConverter.ToInt64(key, 0));
    }

    private byte[] GetIvForPacket(uint packetId)
    {
        if (generatedIvs.TryGetValue(packetId, out byte[] cachedIv))
            return cachedIv;

        while (nextExpectedPacketId <= packetId)
        {
            byte[] newIv = ivGenerator.GetIV();
            generatedIvs[nextExpectedPacketId] = newIv;
            nextExpectedPacketId++;
        }

        return generatedIvs[packetId];
    }

    public byte[] Encrypt(byte[] payload, uint packetId)
    {
        byte[] iv = GetIvForPacket(packetId);

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(payload, 0, payload.Length);
                cs.FlushFinalBlock();

                return ms.ToArray();
            }
        }
    }

    public byte[] Decrypt(byte[] encryptedData, uint packetId)
    {
        byte[] iv = GetIvForPacket(packetId);

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cs.Write(encryptedData, 0, encryptedData.Length);
                cs.FlushFinalBlock();

                return ms.ToArray();
            }
        }
    }
}