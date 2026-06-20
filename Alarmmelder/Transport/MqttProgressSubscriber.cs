using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MioneAlarmmelder.Core;

namespace MioneAlarmmelder.Transport
{
    public sealed class MqttProgressSubscriber : IDisposable
    {
        private readonly AppSettings settings;
        private volatile bool stopping;
        private Thread worker;
        private TcpClient activeClient;
        public event EventHandler<AlarmProgressEvent> ProgressReceived;

        public MqttProgressSubscriber(AppSettings value) { settings = value; }

        public void Start()
        {
            if (worker != null || !settings.MqttEnabled) return;
            worker = new Thread(Run); worker.IsBackground = true; worker.Name = "MQTT Alarmstatus"; worker.Start();
        }

        private void Run()
        {
            while (!stopping)
            {
                try { Listen(); }
                catch (Exception ex) { if (!stopping) ErrorLogger.Log("MQTT-Alarmstatus", ex); }
                for (int i = 0; i < 30 && !stopping; i++) Thread.Sleep(100);
            }
        }

        private void Listen()
        {
            using (TcpClient client = TcpPublisher.Connect(settings.MqttHost, settings.MqttPort, 5000))
            using (NetworkStream stream = client.GetStream())
            {
                activeClient = client; client.ReceiveTimeout = 5000; client.SendTimeout = 5000;
                SendConnect(stream); byte header; byte[] body; ReadPacket(stream, out header, out body);
                if ((header >> 4) != 2 || body.Length < 2 || body[1] != 0) throw new IOException("MQTT-Anmeldung für Alarmstatus abgelehnt.");
                SendSubscribe(stream, new string[] { Topic("MiOne/AlarmStatus"), Topic("MiOne/ModemStatus") }); ReadPacket(stream, out header, out body);
                if ((header >> 4) != 9) throw new IOException("MQTT-Alarmstatus konnte nicht abonniert werden.");
                client.ReceiveTimeout = 15000;
                while (!stopping)
                {
                    try
                    {
                        ReadPacket(stream, out header, out body);
                        if ((header >> 4) == 3) HandlePublish(header, body);
                    }
                    catch (IOException)
                    {
                        if (stopping) return;
                        stream.WriteByte(0xC0); stream.WriteByte(0); stream.Flush();
                    }
                }
            }
        }

        private void HandlePublish(byte header, byte[] body)
        {
            if (body.Length < 2) return;
            int topicLength = (body[0] << 8) | body[1];
            int offset = 2 + topicLength + (((header >> 1) & 3) > 0 ? 2 : 0);
            if (topicLength < 1 || offset > body.Length) return;
            string json = Encoding.UTF8.GetString(body, offset, body.Length - offset);
            AlarmProgressEvent value;
            if (!AlarmProgressEvent.TryParse(json, "MQTT", out value) && !AlarmProgressEvent.TryParseModemStatus(json, "MQTT", out value)) return;
            if (!String.Equals(value.ModemImei, settings.ModemImei, StringComparison.Ordinal)) return;
            if (ProgressReceived != null) ProgressReceived(this, value);
        }

        private void SendConnect(Stream stream)
        {
            MemoryStream body = new MemoryStream(); WriteString(body, "MQTT"); body.WriteByte(4);
            byte flags = 2; if (!String.IsNullOrEmpty(settings.MqttUser) || !String.IsNullOrEmpty(settings.MqttPassword)) flags |= 0x80;
            if (!String.IsNullOrEmpty(settings.MqttPassword)) flags |= 0x40;
            body.WriteByte(flags); body.WriteByte(0); body.WriteByte(60);
            WriteString(body, "Mione-Status-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            if (!String.IsNullOrEmpty(settings.MqttUser) || !String.IsNullOrEmpty(settings.MqttPassword)) WriteString(body, settings.MqttUser);
            if (!String.IsNullOrEmpty(settings.MqttPassword)) WriteString(body, settings.MqttPassword);
            SendPacket(stream, 0x10, body.ToArray());
        }

        private void SendSubscribe(Stream stream, string[] topics)
        {
            MemoryStream body = new MemoryStream(); body.WriteByte(0); body.WriteByte(1);
            for (int i = 0; i < topics.Length; i++) { WriteString(body, topics[i]); body.WriteByte(0); }
            SendPacket(stream, 0x82, body.ToArray());
        }

        private string Topic(string subTopic)
        {
            string user = (settings.MqttUser ?? "").Trim().Trim('/');
            if (user.Length == 0) throw new InvalidOperationException("MQTT-Benutzername/Top-Topic fehlt.");
            return user + "/" + subTopic.TrimStart('/');
        }

        private static void ReadPacket(Stream stream, out byte header, out byte[] body)
        {
            int first = stream.ReadByte(); if (first < 0) throw new EndOfStreamException(); header = (byte)first;
            int multiplier = 1, length = 0, digit;
            do { digit = stream.ReadByte(); if (digit < 0) throw new EndOfStreamException(); length += (digit & 127) * multiplier; multiplier *= 128; if (multiplier > 128 * 128 * 128 * 128) throw new IOException("Ungültige MQTT-Paketlänge."); } while ((digit & 128) != 0);
            body = new byte[length]; int offset = 0;
            while (offset < length) { int read = stream.Read(body, offset, length - offset); if (read <= 0) throw new EndOfStreamException(); offset += read; }
        }

        private static void SendPacket(Stream stream, byte header, byte[] body)
        {
            stream.WriteByte(header); int length = body.Length;
            do { int digit = length % 128; length /= 128; if (length > 0) digit |= 128; stream.WriteByte((byte)digit); } while (length > 0);
            stream.Write(body, 0, body.Length); stream.Flush();
        }

        private static void WriteString(Stream stream, string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value ?? ""); stream.WriteByte((byte)(data.Length >> 8));
            stream.WriteByte((byte)data.Length); stream.Write(data, 0, data.Length);
        }

        public void Dispose()
        {
            stopping = true; try { if (activeClient != null) activeClient.Close(); } catch { }
            if (worker != null && worker.IsAlive) worker.Join(1500); worker = null; activeClient = null;
        }
    }
}
