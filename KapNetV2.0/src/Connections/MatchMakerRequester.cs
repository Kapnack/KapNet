using ImageCampus.ToolBox.Events;
using ImageCampus.ToolBox.Services;
using System.Net;

namespace Net
{
    public sealed class MatchMakerRequester : IReceiveData
    {
        private const float RESEND_INTERVAL = 0.5f;

        private readonly IPAddress matchMakerAddress;
        private readonly int matchMakerPort;
        private readonly string playerName;

        private readonly PacketFactory packetFactory = new PacketFactory();
        private readonly PacketWriter packetWriter = new PacketWriter();
        private readonly PacketReader packetReader = new PacketReader();

        private EventBus EventBus => ServiceProvider.Instance.GetService<EventBus>();

        private UdpConnection connection;
        private float resendTimer;
        private bool assigned;

        public bool Assigned => assigned;

        public MatchMakerRequester(string matchMakerIp, int matchMakerPort, string playerName)
        {
            matchMakerAddress = IPAddress.Parse(matchMakerIp);
            this.matchMakerPort = matchMakerPort;
            this.playerName = playerName;
        }

        public void Start()
        {
            connection = new UdpConnection(matchMakerAddress, matchMakerPort, this);
            SendRequest();
            resendTimer = RESEND_INTERVAL;
        }

        public void Tick(float deltaTime)
        {
            if (connection == null)
                return;

            connection.FlushReceiveData();

            if (assigned)
                return;

            resendTimer -= deltaTime;
            if (resendTimer <= 0f)
            {
                SendRequest();
                resendTimer = RESEND_INTERVAL;
            }
        }

        private void SendRequest()
        {
            packetWriter.Reset();
            packetWriter.Write(playerName);
            byte[] payload = packetWriter.GetBytes();
            packetWriter.Reset();

            (byte[] data, uint _) = packetFactory.Create(PacketType.RequestMatch, payload, PacketMetaData.None);
            connection.Send(data);
        }

        public void OnReceiveData(byte[] data, IPEndPoint sender)
        {
            if (PacketUtility.GetType(data) != PacketType.ServerAssigned)
                return;

            byte[] payload = PacketUtility.GetPayload(data);
            packetReader.AssignData(payload);

            string ip = packetReader.ReadString();
            int port = packetReader.ReadInt();

            assigned = true;
            EventBus.Raise<ServerAssignedEvent>(ip, port);
        }

        public void Stop()
        {
            if (connection != null)
            {
                connection.Close();
                connection = null;
            }
        }
    }
}
