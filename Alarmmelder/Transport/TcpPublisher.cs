using System;
using System.IO;
using System.Net;
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

        public static void PublishWithProgress(string host, int port, string json, Action<AlarmProgressEvent> progress, int waitMilliseconds)
        {
            using (TcpClient client = Connect(host, port, 5000))
            using (NetworkStream stream = client.GetStream())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json + "\r\n");
                stream.Write(bytes, 0, bytes.Length); stream.Flush();
                client.ReceiveTimeout = Math.Max(1000, waitMilliseconds);
                DateTime until = DateTime.UtcNow.AddMilliseconds(waitMilliseconds);
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    while (DateTime.UtcNow < until)
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;
                        AlarmProgressEvent value;
                        if (AlarmProgressEvent.TryParse(line, "TCP", out value) && progress != null) progress(value);
                        if (line.IndexOf("\"type\":\"alarmResult\"", StringComparison.Ordinal) >= 0) break;
                    }
                }
            }
        }

        internal static TcpClient Connect(string host, int port, int timeout)
        {
            IPAddress[] addresses = Dns.GetHostAddresses(host); Exception lastError = null;
            for (int i = 0; i < addresses.Length; i++)
            {
                if (addresses[i].AddressFamily != AddressFamily.InterNetwork && addresses[i].AddressFamily != AddressFamily.InterNetworkV6) continue;
                TcpClient client = null;
                try
                {
                    client = new TcpClient(addresses[i].AddressFamily);
                    IAsyncResult result = client.BeginConnect(addresses[i], port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(timeout, false)) throw new IOException("Zeitüberschreitung bei " + addresses[i]);
                    client.EndConnect(result); return client;
                }
                catch (Exception ex) { lastError = ex; if (client != null) client.Close(); }
            }
            throw new IOException("Verbindung zu " + host + " über IPv4 oder IPv6 fehlgeschlagen.", lastError);
        }
    }
}
