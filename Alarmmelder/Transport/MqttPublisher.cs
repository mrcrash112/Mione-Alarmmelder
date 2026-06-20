using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace MioneAlarmmelder.Transport
{
    // Kleiner MQTT-3.1.1-QoS-0-Client, damit das Programm ohne Zusatzpakete auf .NET 3.5 läuft.
    public static class MqttPublisher
    {
        public static void Publish(string host, int port, string user, string password, string topic, string payload)
        {
            using (TcpClient client = TcpPublisher.Connect(host, port, 5000))
            using (NetworkStream stream = client.GetStream())
            {
                client.ReceiveTimeout = 5000; client.SendTimeout = 5000;
                SendConnect(stream, user, password);
                byte[] answer = new byte[4]; ReadFully(stream, answer, 0, 4);
                if (answer[0] != 0x20 || answer[1] != 0x02 || answer[3] != 0x00) throw new IOException("MQTT-Anmeldung abgelehnt (Code " + answer[3] + ").");
                bool retain = topic.EndsWith("/MiOne/Config/Mobile", StringComparison.Ordinal) ||
                              topic.EndsWith("/MiOne/Config/Mobile/modemImei", StringComparison.Ordinal);
                SendPublish(stream, topic, payload, retain);
                stream.WriteByte(0xE0); stream.WriteByte(0x00); stream.Flush();
            }
        }

        private static void SendConnect(Stream stream, string user, string password)
        {
            MemoryStream body = new MemoryStream();
            WriteString(body, "MQTT"); body.WriteByte(4);
            byte flags = 2;
            if (!String.IsNullOrEmpty(user) || !String.IsNullOrEmpty(password)) flags |= 0x80;
            if (!String.IsNullOrEmpty(password)) flags |= 0x40;
            body.WriteByte(flags); body.WriteByte(0); body.WriteByte(30);
            WriteString(body, "Mione-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            if (!String.IsNullOrEmpty(user) || !String.IsNullOrEmpty(password)) WriteString(body, user);
            if (!String.IsNullOrEmpty(password)) WriteString(body, password);
            SendPacket(stream, 0x10, body.ToArray());
        }

        private static void SendPublish(Stream stream, string topic, string payload, bool retain)
        {
            MemoryStream body = new MemoryStream(); WriteString(body, topic);
            byte[] data = Encoding.UTF8.GetBytes(payload); body.Write(data, 0, data.Length);
            SendPacket(stream, (byte)(retain ? 0x31 : 0x30), body.ToArray());
        }

        private static void SendPacket(Stream stream, byte header, byte[] body)
        {
            stream.WriteByte(header); int length = body.Length;
            do { int digit = length % 128; length /= 128; if (length > 0) digit |= 128; stream.WriteByte((byte)digit); } while (length > 0);
            stream.Write(body, 0, body.Length); stream.Flush();
        }
        private static void WriteString(Stream stream, string value) { byte[] b = Encoding.UTF8.GetBytes(value ?? ""); stream.WriteByte((byte)(b.Length >> 8)); stream.WriteByte((byte)b.Length); stream.Write(b, 0, b.Length); }
        private static void ReadFully(Stream stream, byte[] b, int o, int c) { while (c > 0) { int n = stream.Read(b, o, c); if (n <= 0) throw new EndOfStreamException(); o += n; c -= n; } }
    }
}
