using System;
using System.Security.Cryptography;
using ImageCampus.ToolBox.Services;

public class NetworkRandomNumberGenerator : RandomNumberGenerator, IService
{
    private ulong _currentState;

    private const ulong Multiplier = 1103515245; // a
    private const ulong Increment = 12345; // c
    private const ulong Modulus = 2147483648; // m (2^31)

    public bool IsPersistance => false;

    public NetworkRandomNumberGenerator(ulong seed)
    {
        _currentState = seed;
    }

    public long GetLong(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        GetBytes(data.AsSpan());

        return BitConverter.ToInt64(data, 0);
    }

    public override void GetBytes(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        GetBytes(data.AsSpan());
    }

    public void GetBytes(Span<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            _currentState = (Multiplier * _currentState + Increment) % Modulus;

            data[i] = (byte)(_currentState >> 16);
        }
    }

    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);

        Span<byte> buffer = stackalloc byte[4];
        GetBytes(buffer);

        uint value = BitConverter.ToUInt32(buffer.ToArray(), 0);
        long span = (long)maxInclusive - minInclusive + 1;

        return (int)(minInclusive + (long)(value % (uint)span));
    }

    public byte[] GetKey()
    {
        byte[] keyData = new byte[32];
        GetBytes(keyData);
        return keyData;
    }

    public byte[] GetIV()
    {
        byte[] ivData = new byte[16];
        GetBytes(ivData);
        return ivData;
    }
}
