using Net;
using System;

namespace Networking.Core
{
    public class CheckSumFactory
    {
        public delegate int ChecksumMethod(ReadOnlySpan<byte> data);

        struct ChecksumMethodData
        {
            public ChecksumMethod method;
            public int methodId;
            public string name;
        }

        private readonly ChecksumMethodData _selectedMethodData;
        private readonly int _checkNum;
        private const int METHODS_AMOUNT = 7;

        //https://en.wikipedia.org/wiki/Golden_ratio
        //https://en.wikipedia.org/wiki/Tiny_Encryption_Algorithm
        private const uint GOLDEN_RATIO = 0x9E3779B9;

        //https://www.mathstat.dal.ca/~selinger/random/
        //https://en.wikipedia.org/wiki/Linear_congruential_generator
        private const uint LCG_CONSTANT = 1103515245;

        //https://en.wikipedia.org/wiki/MurmurHash
        //https://github.com/aappleby/smhasher/blob/master/src/MurmurHash2.cpp
        private const int MURMUR_HASH = 0x5bd1e995;

        public ulong Seed { get; private set; }

        public CheckSumFactory(ulong seed)
        {
            Seed = seed;

            int size = sizeof(int) * 2;
            byte[] randomBytes = new byte[size];

            using (NetworkRandomNumberGenerator rng = new NetworkRandomNumberGenerator(seed))
            {
                rng.GetBytes(randomBytes);
            }

            using (PacketReader packetReader = new PacketReader())
            {
                packetReader.AssignData(randomBytes);

                _checkNum = packetReader.ReadInt();

                int methodId = Math.Abs(packetReader.ReadInt()) % METHODS_AMOUNT;

                _selectedMethodData = new ChecksumMethodData();

                switch (methodId)
                {
                    case 0:
                        _selectedMethodData.method = RotateXorChecksum;
                        break;

                    case 1:
                        _selectedMethodData.method = PingPongCheckusum;
                        break;

                    case 2:
                        _selectedMethodData.method = AvalancheChecksum;
                        break;

                    case 3:
                        _selectedMethodData.method = DualLaneChecksum;
                        break;

                    case 4:
                        _selectedMethodData.method = ReverseChecksum;
                        break;

                    case 5:
                        _selectedMethodData.method = CellularChecksum;
                        break;

                    default:
                        _selectedMethodData.method = CipherChecksum;
                        break;
                }

                switch (methodId)
                {
                    case 0:
                        _selectedMethodData.name = nameof(RotateXorChecksum);
                        break;

                    case 1:
                        _selectedMethodData.name = nameof(PingPongCheckusum);
                        break;

                    case 2:
                        _selectedMethodData.name = nameof(AvalancheChecksum);
                        break;

                    case 3:
                        _selectedMethodData.name = nameof(DualLaneChecksum);
                        break;

                    case 4:
                        _selectedMethodData.name = nameof(ReverseChecksum);
                        break;

                    case 5:
                        _selectedMethodData.name = nameof(CellularChecksum);
                        break;

                    default:
                        _selectedMethodData.name = nameof(CipherChecksum);
                        break;
                }

                _selectedMethodData.methodId = methodId;
            }
        }

        //(pg. 15) https://www.highperformancegraphics.org/previous/www_2010/media/GPUAlgorithms/HPG2010_GPUAlgorithms_Zafar.pdf
        private int CipherChecksum(ReadOnlySpan<byte> span)
        {
            var left = (uint)_checkNum;
            var right = GOLDEN_RATIO;

            var k0 = 0xA341316Cu;
            var k1 = 0xC8013EA4u;
            var k2 = 0xAD90777Du;
            var k3 = 0x7E95761Eu;

            int primeA = 5;
            int primeB = 3;

            foreach (var @byte in span)
            {
                left += ((right << primeA) + @byte) ^ (right + k0) ^ ((right >> primeB) + k1);
                right += ((left << primeA) + @byte) ^ (left + k2) ^ ((left >> primeB) + k3);
            }

            return (int)(left ^ right);
        }

        //https://es.wikipedia.org/wiki/Aut%C3%B3mata_celular
        //Treats the bits as "cells".
        private int CellularChecksum(ReadOnlySpan<byte> span)
        {
            uint state = (uint)_checkNum;

            foreach (byte b in span)
            {

                uint left = state << 1;

                uint right = state >> 1;

                state ^= left + right + b;

                //Just to make sure the state doesn't become all 0 bits.
                state *= LCG_CONSTANT;
            }

            return (int)state;
        }

        private int ReverseChecksum(ReadOnlySpan<byte> span)
        {
            int hash = _checkNum;

            int totalBits = 32;
            int leftShift = 7;
            int rightShift = totalBits - leftShift;

            int primeMul = 13;

            for (int i = span.Length - 1; i >= 0; i--)
            {
                hash ^= span[i];
                hash = (hash << leftShift) | (hash >> rightShift);

                hash += i * primeMul;
            }

            return hash;
        }

        private int DualLaneChecksum(ReadOnlySpan<byte> span)
        {
            int a = _checkNum;
            int b = ~_checkNum; //negated

            int primeA = 31; // 2^5 - 1
            int primeB = 17; // 2^4 + 1

            for (int i = 0; i < span.Length; i++)
            {
                //same as using %2 == 0, checks even and odd indices. 
                //If the last bit is 0, then it's even. Otherwise, it's odd.
                //0 & 1 (0000 & 0001) = 0
                //1 & 1 (0001 & 0001) = 1, odd
                //2 & 1 (0010 & 0001) = 0, even
                if ((i & 1) == 0)
                    //multiply -> xor
                    a = (a * primeA) ^ span[i];
                else
                    //multiply -> add
                    b = (b * primeB) + span[i];
            }

            //combine results
            return a ^ b;
        }

        private int AvalancheChecksum(ReadOnlySpan<byte> span)
        {
            uint hash = (uint)_checkNum;

            int primeNum = 15;

            foreach (var @byte in span)
            {
                hash ^= @byte;
                hash *= MURMUR_HASH;
                hash ^= hash >> primeNum;
            }

            return (int)hash;
        }

        private int PingPongCheckusum(ReadOnlySpan<byte> span)
        {
            int hash = _checkNum;
            bool forward = true;

            int primeA = 33;
            int primeB = 17;

            for (int i = 0; i < span.Length; i++)
            {
                byte b = span[i];

                if (forward)
                    hash = (hash * primeA) ^ b;
                else
                    hash = (hash * primeB) + ~b;

                forward = !forward;
            }

            return hash;
        }

        private int RotateXorChecksum(ReadOnlySpan<byte> data)
        {
            uint hash = (uint)_checkNum;

            int totalBits = 32;
            int leftShift = 5; //prime number
            int rightShift = totalBits - leftShift;


            foreach (byte @byte in data)
            {
                hash = (hash << leftShift) | (hash >> rightShift);
                hash ^= @byte;
                hash *= GOLDEN_RATIO;
            }

            return (int)hash;
        }

        public ChecksumMethod GetChecksumOperation()
        {
            return _selectedMethodData.method;
        }

        public int GetChecksumOperationId()
        {
            return _selectedMethodData.methodId;
        }

        public string GetChecksumOperationName()
        {
            return _selectedMethodData.name;
        }

        // Conveniencia para no tener que hacer GetChecksumOperation()(data)
        // en cada lugar que necesite validar un paquete.
        public int Compute(ReadOnlySpan<byte> data)
        {
            return _selectedMethodData.method(data);
        }
    }
}