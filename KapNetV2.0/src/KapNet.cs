using ImageCampus.ToolBox.Events;
using ImageCampus.ToolBox.Services;
using System.Net;

namespace Net
{
    public class KapNet : ITickable
    {
        internal ConnectionHandler Connection => ServiceProvider.Instance.GetService<ConnectionHandler>();
        internal ConnectionHandler ConnectionSetter
        {
            set
            {
                if (value != null)
                    ServiceProvider.Instance.AddService<ConnectionHandler>(value);
                else
                    ServiceProvider.Instance.RemoveService<ConnectionHandler>();
            }
        }

        private KapNet()
        {
        }

        private static void Init(object rootObject)
        {
            ServiceProvider.Instance.AddService<NetTree>(NetTree.Build(rootObject));
            ServiceProvider.Instance.AddService<PacketWriter>(new PacketWriter());
            ServiceProvider.Instance.AddService<PacketReader>(new PacketReader());
            ServiceProvider.Instance.AddService<PacketFactory>(new PacketFactory());
            ServiceProvider.Instance.AddService<TaskScheduler>(new TaskScheduler());
            ServiceProvider.Instance.AddService<EventBus>(new EventBus());

            // ---------------------------------------------------------------------------------
            // KapMatch change (requested item 1B): register and initialise the RPC system.
            // The original KapNet.Init never wired RPCFactory, so [RPC] methods were never
            // patched and the receive path (which fetches RPCFactory from the service provider)
            // would throw. RPCFactory.Init is guarded internally to run its Harmony patching only
            // once per process.
            // ---------------------------------------------------------------------------------
            ServiceProvider.Instance.AddService<RPCFactory>(new RPCFactory());
            ServiceProvider.Instance.GetService<RPCFactory>().Init();
        }

        public static KapNet Client(object rootObject, string name)
        {
            Init(rootObject);

            KapNet kapNet = new KapNet
            {
                ConnectionSetter = new ClientConnection(name)
            };

            return kapNet;
        }

        public static KapNet Server(object rootObject)
        {
            Init(rootObject);

            KapNet kapNet = new KapNet
            {
                ConnectionSetter = new ServerConnection()
            };

            return kapNet;
        }

        // KapMatch addition (2B): construct a matchmaker node.
        public static KapNet MatchMaker(object rootObject)
        {
            Init(rootObject);

            KapNet kapNet = new KapNet
            {
                ConnectionSetter = new MatchMakerConnection()
            };

            return kapNet;
        }

        public void Connect(int port, string name)
        {
            (Connection as ServerConnection).Connect(port);
        }

        public void Connect(string ip, int port)
        {
            (Connection as ClientConnection).Connect(ip, port);
        }

        // KapMatch addition (2B): server binds its listen port.
        public void StartServer(int port)
        {
            (Connection as ServerConnection).Connect(port);
        }

        // KapMatch addition (2B): matchmaker binds its listen port.
        public void StartMatchMaker(int port)
        {
            (Connection as MatchMakerConnection).Connect(port);
        }

        // KapMatch addition (2B): server announces itself to the matchmaker.
        public void RegisterWithMatchMaker(string matchMakerIp, int matchMakerPort)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(matchMakerIp), matchMakerPort);
            (Connection as ServerConnection).RegisterWithMatchMaker(endPoint);
        }

        public void Tick(float deltaTime)
        {
            Connection.Tick(deltaTime);
        }
    }
}
