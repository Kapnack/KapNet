using KapNet;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
public class UdpConnection
{
    private struct DataReceived
    {
        public byte[] data;

        public IPEndPoint ipEndPoint;
    }

    private readonly UdpClient connection;

    private IReceiveData receiver = null;

    private bool isRunning = true;

    private Queue<DataReceived> dataReceivedQueue = new Queue<DataReceived>();

    private Thread recieveData;

    public UdpConnection(int port, IReceiveData receiver = null)
    {
        connection = new UdpClient(port);
        this.receiver = receiver;
        recieveData = new Thread(StartReceiveLoop);
        recieveData.Start();
    }

    public UdpConnection(IPAddress ip, int port, IReceiveData receiver = null)
    {
        connection = new UdpClient();
        Connect(ip, port);
        this.receiver = receiver;
        recieveData = new Thread(StartReceiveLoop);
        recieveData.Start();
    }

    private void Connect(IPAddress ip, int port)
    {
        connection.Connect(ip, port);
    }

    public void Close()
    {
        isRunning = false;
        recieveData.Abort();
        connection.Close();
    }

    public void FlushReceiveData()
    {
        lock (dataReceivedQueue)
        {
            while (dataReceivedQueue.Count > 0)
            {
                DataReceived dataReceived = dataReceivedQueue.Dequeue();
                if (receiver != null)
                {
                    receiver.OnReceiveData(dataReceived.data, dataReceived.ipEndPoint);
                }
            }
        }
    }

    public async void StartReceiveLoop()
    {
        while (isRunning)
        {
            try
            {
                UdpReceiveResult result = await connection.ReceiveAsync();

                if (IsValidCheckSum(result.Buffer))
                {
                    lock (dataReceivedQueue)
                    {
                        dataReceivedQueue.Enqueue(new DataReceived
                        {
                            data = result.Buffer,
                            ipEndPoint = result.RemoteEndPoint
                        });
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                break;
            }
        }
    }

    private bool IsValidCheckSum(byte[] data)
    {
        return PacketUtility.CalculateCheckSum(data, 0, 8) == PacketUtility.GetCheckSum1(data) && PacketUtility.CalculateCheckSum(data, 0, 4) == PacketUtility.GetCheckSum2(data);
    }

    public void Send(byte[] data)
    {
        connection.Send(data, data.Length);
    }

    public void Send(byte[] data, IPEndPoint ipEndPoint)
    {
        connection.Send(data, data.Length, ipEndPoint);
    }
}