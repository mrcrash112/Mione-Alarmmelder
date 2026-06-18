using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace MioneAlarmmelder.Transport
{
    public static class TcpPublisher
    {
        public static void Publish(string host, int port, string json)
        {
            using (TcpClient client = Connect(host, port, 5000))
            using (NetworkStream stream = client.GetStream())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json + "\r\n");
                stream.Write(bytes, 0, bytes.Length); stream.Flush();
            }
        }

        internal static TcpClient Connect(string host, int port, int timeout)
        {
            TcpClient client = new TcpClient();
            IAsyncResult result = client.BeginConnect(host, port, null, null);
            if (!result.AsyncWaitHandle.WaitOne(timeout, false)) { client.Close(); throw new IOException("Zeitüberschreitung beim Verbindungsaufbau zu " + host); }
            client.EndConnect(result); return client;
        }
    }
}
