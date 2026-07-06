using System;
using System.Collections.Generic;
using System.Net;

namespace Net
{
    internal sealed class MatchMakerConnection : ConnectionHandler
    {
        private struct ServerEntry
        {
            public IPEndPoint endPoint;
            public DateTime lastSeen;
        }

        private sealed class WaitingPlayer
        {
            public IPEndPoint endPoint;
            public string name;
            public DateTime lastSeen;
        }

        private struct Assignment
        {
            public string serverIp;
            public int serverPort;
            public int matchId;
        }

        private const double WaitingTimeoutSeconds = 6d;

        private readonly List<ServerEntry> servers = new List<ServerEntry>();
        private readonly List<WaitingPlayer> waitingQueue = new List<WaitingPlayer>();
        private readonly Dictionary<IPEndPoint, Assignment> assignments = new Dictionary<IPEndPoint, Assignment>();

        private int roundRobin = 0;
        private int nextMatchId = 1;

        public MatchMakerConnection() : base()
        {
            AddPacketStrategy(PacketType.ServerRegister, HandleServerRegister);
            AddPacketStrategy(PacketType.ServerDeregister, HandleServerDeregister);
            AddPacketStrategy(PacketType.RequestMatch, HandleRequestMatch);
        }

        public void Connect(int port)
        {
            connection = new UdpConnection(port, this);
            Console.WriteLine($"[MatchMaker] Listening on UDP port {port}.");
        }

        private int IndexOfServer(IPEndPoint endPoint)
        {
            for (int i = 0; i < servers.Count; ++i)
                if (servers[i].endPoint.Equals(endPoint))
                    return i;
            return -1;
        }

        private void HandleServerRegister(NetworkPacket networkPacket)
        {
            int listenPort = packetReader.ReadInt();
            IPEndPoint endPoint = new IPEndPoint(networkPacket.ipEndPoint.Address, listenPort);

            int index = IndexOfServer(endPoint);
            if (index >= 0)
            {
                ServerEntry existing = servers[index];
                existing.lastSeen = DateTime.UtcNow;
                servers[index] = existing;
            }
            else
            {
                servers.Add(new ServerEntry { endPoint = endPoint, lastSeen = DateTime.UtcNow });
                Console.WriteLine($"[MatchMaker] Server registered: {endPoint}. Total servers: {servers.Count}.");
            }

            TryFormMatches();
        }

        private void HandleServerDeregister(NetworkPacket networkPacket)
        {
            int listenPort = packetReader.ReadInt();
            IPEndPoint endPoint = new IPEndPoint(networkPacket.ipEndPoint.Address, listenPort);

            int index = IndexOfServer(endPoint);
            if (index >= 0)
            {
                servers.RemoveAt(index);
                Console.WriteLine($"[MatchMaker] Server deregistered: {endPoint}. Total servers: {servers.Count}.");
            }
        }

        private int IndexOfWaiting(IPEndPoint endPoint)
        {
            for (int i = 0; i < waitingQueue.Count; i++)
                if (waitingQueue[i].endPoint.Equals(endPoint))
                    return i;
            return -1;
        }

        private void HandleRequestMatch(NetworkPacket networkPacket)
        {
            string playerName = packetReader.ReadString();
            IPEndPoint player = networkPacket.ipEndPoint;

            PruneStaleWaiting();

            if (assignments.TryGetValue(player, out Assignment existing))
            {
                SendTo(player, PacketType.ServerAssigned, PacketMetaData.None, existing.serverIp, existing.serverPort);
                return;
            }

            int waitingIndex = IndexOfWaiting(player);
            if (waitingIndex >= 0)
            {
                waitingQueue[waitingIndex].lastSeen = DateTime.UtcNow;
                return;
            }

            waitingQueue.Add(new WaitingPlayer { endPoint = player, name = playerName, lastSeen = DateTime.UtcNow });
            Console.WriteLine($"[MatchMaker] '{playerName}' ({player}) queued. Waiting: {waitingQueue.Count}.");

            TryFormMatches();
        }

        private void PruneStaleWaiting()
        {
            DateTime now = DateTime.UtcNow;
            for (int i = waitingQueue.Count - 1; i >= 0; --i)
            {
                if ((now - waitingQueue[i].lastSeen).TotalSeconds > WaitingTimeoutSeconds)
                {
                    Console.WriteLine($"[MatchMaker] Dropping stale queued player '{waitingQueue[i].name}' ({waitingQueue[i].endPoint}).");
                    waitingQueue.RemoveAt(i);
                }
            }
        }

        private void TryFormMatches()
        {
            while (waitingQueue.Count >= 2 && servers.Count > 0)
            {
                WaitingPlayer a = waitingQueue[0];
                WaitingPlayer b = waitingQueue[1];
                waitingQueue.RemoveAt(1);
                waitingQueue.RemoveAt(0);

                ServerEntry server = servers[roundRobin % servers.Count];
                roundRobin++;

                Assignment assignment = new Assignment
                {
                    serverIp = server.endPoint.Address.ToString(),
                    serverPort = server.endPoint.Port,
                    matchId = nextMatchId++
                };

                assignments[a.endPoint] = assignment;
                assignments[b.endPoint] = assignment;

                Console.WriteLine($"[MatchMaker] Match {assignment.matchId}: '{a.name}' vs '{b.name}' -> {assignment.serverIp}:{assignment.serverPort}.");

                SendTo(a.endPoint, PacketType.ServerAssigned, PacketMetaData.None, assignment.serverIp, assignment.serverPort);
                SendTo(b.endPoint, PacketType.ServerAssigned, PacketMetaData.None, assignment.serverIp, assignment.serverPort);
            }
        }

        protected override void HandleHandShake(NetworkPacket networkPacket) { }
        protected override void HandlePing(NetworkPacket networkPacket) { }
        protected override void HandleClientLeft(NetworkPacket networkPacket) { }

        protected override bool HandleReliablePacketRecived(ref NetworkPacket networkPacket, byte[] _)
        {
            return true;
        }

        protected override void OnParameterValueChange(Type valueType, object value, PacketMetaData metaData, uint[] adress) { }
        protected override void OnValueBecameNull(PacketMetaData metadata, uint[] address) { }
    }
}