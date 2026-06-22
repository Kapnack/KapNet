using System;
using System.Security.Cryptography;

public class NetworkRandomNumberGenerator : RandomNumberGenerator
{
    private long _currentState;

    private const long Multiplier = 1103515245; // a
    private const long Increment = 12345; // c
    private const long Modulus = 2147483648; // m (2^31)

    public NetworkRandomNumberGenerator(long seed)
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

    public override void GetBytes(Span<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            _currentState = (Multiplier * _currentState + Increment) % Modulus;

            data[i] = (byte)(_currentState >> 16);
        }
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