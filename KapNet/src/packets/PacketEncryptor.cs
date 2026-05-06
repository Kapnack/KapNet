using KapNet;
using System.IO;
using System.Security.Cryptography;

public class PacketEncryptor
{
    private readonly byte[] key;

    public PacketEncryptor(byte[] seed)
    {
        using (SHA256 sha = SHA256.Create())
        {
            key = sha.ComputeHash(seed);
        }
    }

    public (byte[] encryptedData, byte[] iv) Encrypt(byte[] payload)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.GenerateIV();

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(payload, 0, payload.Length);
                cs.FlushFinalBlock();

                return (ms.ToArray(), aes.IV);
            }
        }
    }

    public byte[] Decrypt(byte[] encryptedData, byte[] iv)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cs.Write(encryptedData, 0, encryptedData.Length);
                return ms.ToArray();
            }
        }
    }
}