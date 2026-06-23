using ImageCampus.ToolBox.Services;
using KapNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Network.Packets
{
	internal sealed class NetworkMessageSecurity
	{
		public long key;
		public NetworkRandomNumberGenerator ivGenerator;
		public Dictionary<uint, long> generatedIvs = new Dictionary<uint, long>();
		public uint nextExpectedPacketId = 0;
	}

	internal sealed class NetworkRandomRegistry : IService
	{
		public bool IsPersistance => true;

		EventBus EventBus => ServiceProvider.Instance.GetService<EventBus>();
		NetworkService NetworkService => ServiceProvider.Instance.GetService<NetworkService>();

		private Dictionary<int, Dictionary<PacketType, NetworkMessageSecurity>> clientToEncryption =
			new Dictionary<int, Dictionary<PacketType, NetworkMessageSecurity>>();

		private Dictionary<int, Dictionary<PacketType, NetworkMessageSecurity>> clientToDecryption =
			new Dictionary<int, Dictionary<PacketType, NetworkMessageSecurity>>();

		private List<PacketType> packetTypesToSecure = new List<PacketType>();

		private long _seed;

		private byte[] byteBuffer = new byte[8];
		private long _longBuffer = 0;

		private Dictionary<int, NetworkRandomNumberGenerator> randomGenerator =
			new Dictionary<int, NetworkRandomNumberGenerator>();


		internal NetworkRandomRegistry()
		{
			EventBus.Subscribe<CreatedPacketTypeClassEvent>(HandlePacketTypeClassCreated);
		}

		public void SetSeed(long seed, int clientid, bool isServer) // ADDED isServer
		{
			_seed = seed;
            NetBananaConsole.Log("set seed: " + seed + "for client id: " + clientid);

			foreach (KeyValuePair<int, Client> item in NetworkService.Connections)
			{
				if (clientid == item.Key)
				{
					//TODO: Check if client already existed and troubleshoot it
					randomGenerator.Add(item.Key, new NetworkRandomNumberGenerator(_seed));
					clientToEncryption.Add(clientid, new Dictionary<PacketType, NetworkMessageSecurity>());
					clientToDecryption.Add(clientid, new Dictionary<PacketType, NetworkMessageSecurity>());

					for (int i = 0; i < packetTypesToSecure.Count; i++)
					{
						if (isServer)
						{
							// Server generates Encryption first (Value 1), then Decryption (Value 2)
							GenerateEncryptionSecurity(item.Key, packetTypesToSecure[i]);
							GenerateDecryptionSecurity(item.Key, packetTypesToSecure[i]);
						}
						else
						{
							// Client generates Decryption first (Value 1) to match Server's Encryption
							// Then generates Encryption (Value 2) to match Server's Decryption
							GenerateDecryptionSecurity(item.Key, packetTypesToSecure[i]);
							GenerateEncryptionSecurity(item.Key, packetTypesToSecure[i]);
						}
					}
				}
			}
		}

		private void HandlePacketTypeClassCreated(in CreatedPacketTypeClassEvent createdPacketTypeClassEvent)
		{
			packetTypesToSecure.Add(createdPacketTypeClassEvent.PacketType);
		}


		private void GenerateEncryptionSecurity(int clientid, PacketType packetType)
		{
			NetworkRandomNumberGenerator randomGen = randomGenerator[clientid];

			clientToEncryption[clientid].Add(packetType, new NetworkMessageSecurity());

			_longBuffer = randomGen.GetLong(byteBuffer);

			clientToEncryption[clientid][packetType].key = _longBuffer;
			clientToEncryption[clientid][packetType].ivGenerator = new NetworkRandomNumberGenerator(_longBuffer);
		}

		private void GenerateDecryptionSecurity(int clientid, PacketType packetType)
		{
			NetworkRandomNumberGenerator randomGen = randomGenerator[clientid];

			clientToDecryption[clientid].Add(packetType, new NetworkMessageSecurity());

			_longBuffer = randomGen.GetLong(byteBuffer);

			clientToDecryption[clientid][packetType].key = _longBuffer;
			clientToDecryption[clientid][packetType].ivGenerator = new NetworkRandomNumberGenerator(_longBuffer);
		}


public byte[] DecryptPayload(NetworkPacket networkPacket)
       {
          return Decrypt(networkPacket);
       }

       private byte[] Decrypt(NetworkPacket networkPacket)
       {
          NetworkMessageSecurity security = clientToDecryption[networkPacket.clientId][networkPacket.packetType];
          byte[] baseKey = BitConverter.GetBytes(security.key);
          
          long ivLong;
          if (security.generatedIvs.ContainsKey(networkPacket.packetId))
          {
              ivLong = security.generatedIvs[networkPacket.packetId];
          }
          else
          {
              while (security.nextExpectedPacketId <= networkPacket.packetId)
              {
                  long newIv = security.ivGenerator.GetLong(byteBuffer);
                  security.generatedIvs.Add(security.nextExpectedPacketId, newIv);
                  security.nextExpectedPacketId++;
              }
              ivLong = security.generatedIvs[networkPacket.packetId];
          }

          byte[] baseIV = BitConverter.GetBytes(ivLong);
          
          // 2. Expand them to fit AES requirements
          byte[] Key = ExpandTo32Bytes(baseKey);
          byte[] IV = ExpandTo16Bytes(baseIV);

          // Create an Aes object with the specified key and IV.
          using (Aes aesAlg = Aes.Create())
          {
             aesAlg.Key = Key;
             aesAlg.IV = IV;

             ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

             using (MemoryStream msDecrypt = new MemoryStream(networkPacket.payload))
             {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                   using (MemoryStream msPlain = new MemoryStream())
                   {
                      csDecrypt.CopyTo(msPlain);
                      return msPlain.ToArray();
                   }
                }
             }
          }
       }

       public byte[] EncryptPayload(NetworkPacket networkPacket)
       {
          return Encrypt(networkPacket);
       }

       private byte[] Encrypt(NetworkPacket networkPacket)
       {
          NetworkMessageSecurity security = clientToEncryption[networkPacket.clientId][networkPacket.packetType];
          byte[] baseKey = BitConverter.GetBytes(security.key);
          
          long ivLong;
          if (security.generatedIvs.ContainsKey(networkPacket.packetId))
          {
              ivLong = security.generatedIvs[networkPacket.packetId];
          }
          else
          {
              while (security.nextExpectedPacketId <= networkPacket.packetId)
              {
                  long newIv = security.ivGenerator.GetLong(byteBuffer);
                  security.generatedIvs.Add(security.nextExpectedPacketId, newIv);
                  security.nextExpectedPacketId++;
              }
              ivLong = security.generatedIvs[networkPacket.packetId];
          }

          byte[] baseIV = BitConverter.GetBytes(ivLong);

          // 2. Expand them to fit AES requirements
          byte[] Key = ExpandTo32Bytes(baseKey);
          byte[] IV = ExpandTo16Bytes(baseIV);

          byte[] encrypted;

          using (Aes aesAlg = Aes.Create())
          {
             aesAlg.Key = Key;
             aesAlg.IV = IV;

             ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

             using (MemoryStream msEncrypt = new MemoryStream())
             {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                   // FIX: Removed StreamWriter! Write the raw bytes directly to the CryptoStream.
                   csEncrypt.Write(networkPacket.payload, 0, networkPacket.payload.Length);
                }
                encrypted = msEncrypt.ToArray();
             }
          }

          return encrypted;
       }
		
		/// <summary>
		/// Quadruplicates an 8-byte array to create a 32-byte array (256-bit).
		/// </summary>
		private byte[] ExpandTo32Bytes(byte[] eightByteArray)
		{
			byte[] expanded = new byte[32];
			Buffer.BlockCopy(eightByteArray, 0, expanded, 0, 8);
			Buffer.BlockCopy(eightByteArray, 0, expanded, 8, 8);
			Buffer.BlockCopy(eightByteArray, 0, expanded, 16, 8);
			Buffer.BlockCopy(eightByteArray, 0, expanded, 24, 8);
			return expanded;
		}

		/// <summary>
		/// Duplicates an 8-byte array to create a 16-byte array (128-bit).
		/// </summary>
		private byte[] ExpandTo16Bytes(byte[] eightByteArray)
		{
			byte[] expanded = new byte[16];
			Buffer.BlockCopy(eightByteArray, 0, expanded, 0, 8);
			Buffer.BlockCopy(eightByteArray, 0, expanded, 8, 8);
			return expanded;
		}
	}
}