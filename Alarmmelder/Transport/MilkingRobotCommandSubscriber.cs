using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using MioneAlarmmelder.Core;

namespace MioneAlarmmelder.Transport
{
    public sealed class MilkingRobotCommandSubscriber : IDisposable
    {
        private readonly AppSettings settings;
        private readonly DairyPlanCommandBridge dairyPlanBridge;
        private volatile bool stopping;
        private Thread worker;
        private TcpClient activeClient;
        public event EventHandler<MilkingRobotCommandEventArgs> CommandReceived;

        public MilkingRobotCommandSubscriber(AppSettings settings) { this.settings = settings; dairyPlanBridge = new DairyPlanCommandBridge(settings); }

        public void Start()
        {
            if (worker != null || !settings.MqttEnabled || !settings.DpProcessEnabled) return;
            worker = new Thread(Run); worker.IsBackground = true; worker.Name = "MQTT Melkroboter-Befehle"; worker.Start();
        }

        private void Run()
        {
            while (!stopping)
            {
                try { Listen(); }
                catch (Exception ex) { if (!stopping) ErrorLogger.Log("Melkroboter-MQTT-Befehl", ex); }
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
                if ((header >> 4) != 2 || body.Length < 2 || body[1] != 0) throw new IOException("MQTT-Anmeldung für Melkroboter-Befehle abgelehnt.");
                SendSubscribe(stream, new string[] { Topic("Melkroboter/Command"), Topic("Melkroboter/Befehl") });
                WaitForSubscribeAck(stream);
                client.ReceiveTimeout = 60000;
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
                        if (!SendPing(stream)) return;
                    }
                }
            }
        }

        private void WaitForSubscribeAck(NetworkStream stream)
        {
            byte header; byte[] body; DateTime until = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow <= until)
            {
                ReadPacket(stream, out header, out body);
                if ((header >> 4) == 9) return;
                if ((header >> 4) == 3) HandlePublish(header, body);
            }
            throw new IOException("MQTT-Melkroboter-Befehle konnten nicht abonniert werden.");
        }

        private void HandlePublish(byte header, byte[] body)
        {
            if (body.Length < 2) return;
            int topicLength = (body[0] << 8) | body[1];
            int offset = 2 + topicLength + (((header >> 1) & 3) > 0 ? 2 : 0);
            if (topicLength < 1 || offset > body.Length) return;
            string topic = Encoding.UTF8.GetString(body, 2, topicLength);
            string payload = Encoding.UTF8.GetString(body, offset, body.Length - offset);
            MilkingRobotCommand command = MilkingRobotCommand.Parse(payload);
            bool ok; string state; string message;
            string result = Execute(command, out ok, out state, out message);
            string resultTopic = Topic("Melkroboter/Result");
            bool resultPublished = false;
            try
            {
                MqttPublisher.Publish(settings.MqttHost, settings.MqttPort, settings.MqttUser, settings.MqttPassword, resultTopic, result, false);
                resultPublished = true;
            }
            catch (Exception ex)
            {
                state = "publishError";
                message = "Result konnte nicht an MQTT gesendet werden: " + ex.Message;
                ErrorLogger.Log("Melkroboter-MQTT-Result", ex);
            }
            OnCommandReceived(new MilkingRobotCommandEventArgs(DateTime.Now, topic, resultTopic, command, ok, state, message, resultPublished, payload, result));
        }

        private string Execute(MilkingRobotCommand command, out bool ok, out string state, out string message)
        {
            bool valid = MilkingRobotPublisher.IsKnownFunction(command.Name);
            ok = false;
            if (!valid) { state = "invalidCommand"; message = "Unbekannte Melkroboter-Funktion."; return MilkingRobotPublisher.BuildCommandResultJson(command, false, state, message); }
            string missing = MissingParameter(command);
            if (missing.Length > 0) { state = "invalidParameters"; message = "Parameter fehlt: " + missing; return MilkingRobotPublisher.BuildCommandResultJson(command, false, state, message); }
            DairyPlanCommandResult bridgeResult = dairyPlanBridge.Execute(command);
            ok = bridgeResult.Success; state = bridgeResult.State; message = bridgeResult.Message;
            return MilkingRobotPublisher.BuildCommandResultJson(command, ok, state, message);
        }

        private void OnCommandReceived(MilkingRobotCommandEventArgs e)
        {
            EventHandler<MilkingRobotCommandEventArgs> handler = CommandReceived;
            if (handler != null) handler(this, e);
        }

        private static string MissingParameter(MilkingRobotCommand command)
        {
            string[] required = MilkingRobotPublisher.RequiredParametersFor(command.Name);
            for (int i = 0; i < required.Length; i++)
            {
                if (command.Parameter(required[i]).Length == 0) return required[i];
            }
            return "";
        }

        private bool SendPing(Stream stream)
        {
            try { stream.WriteByte(0xC0); stream.WriteByte(0); stream.Flush(); return true; }
            catch (IOException) { return false; }
            catch (ObjectDisposedException) { return false; }
        }

        private void SendConnect(Stream stream)
        {
            MemoryStream body = new MemoryStream(); WriteString(body, "MQTT"); body.WriteByte(4);
            byte flags = 2; if (!String.IsNullOrEmpty(settings.MqttUser) || !String.IsNullOrEmpty(settings.MqttPassword)) flags |= 0x80;
            if (!String.IsNullOrEmpty(settings.MqttPassword)) flags |= 0x40;
            body.WriteByte(flags); body.WriteByte(0); body.WriteByte(60);
            WriteString(body, "Mione-RobotCmd-" + Guid.NewGuid().ToString("N").Substring(0, 8));
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
            do { digit = stream.ReadByte(); if (digit < 0) throw new EndOfStreamException(); length += (digit & 127) * multiplier; multiplier *= 128; if (multiplier > 128 * 128 * 128 * 128) throw new IOException("Ungueltige MQTT-Paketlaenge."); } while ((digit & 128) != 0);
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

    public sealed class MilkingRobotCommand
    {
        public string Name = "";
        public string RequestId = "";
        public string BoxNumber = "";
        public string RobotPosition = "";
        public string SamplingBox = "";
        public string FeedingType = "";
        public string RawPayload = "";

        public string Parameter(string name)
        {
            if (String.Equals(name, "boxNumber", StringComparison.OrdinalIgnoreCase)) return BoxNumber;
            if (String.Equals(name, "robotPosition", StringComparison.OrdinalIgnoreCase)) return RobotPosition;
            if (String.Equals(name, "samplingBox", StringComparison.OrdinalIgnoreCase)) return SamplingBox;
            if (String.Equals(name, "feedingType", StringComparison.OrdinalIgnoreCase)) return FeedingType;
            return "";
        }

        public static MilkingRobotCommand Parse(string payload)
        {
            MilkingRobotCommand command = new MilkingRobotCommand(); command.RawPayload = payload ?? "";
            try
            {
                Dictionary<string, object> values = new JavaScriptSerializer().DeserializeObject(command.RawPayload) as Dictionary<string, object>;
                if (values == null) return command;
                command.Name = Text(values, "command"); if (command.Name.Length == 0) command.Name = Text(values, "function"); if (command.Name.Length == 0) command.Name = Text(values, "action");
                command.RequestId = Text(values, "requestId");
                command.BoxNumber = Text(values, "boxNumber"); if (command.BoxNumber.Length == 0) command.BoxNumber = Text(values, "box");
                command.RobotPosition = Text(values, "robotPosition");
                command.SamplingBox = Text(values, "samplingBox");
                command.FeedingType = Text(values, "feedingType");
            }
            catch { }
            return command;
        }

        private static string Text(Dictionary<string, object> values, string key)
        {
            object value; return values.TryGetValue(key, out value) && value != null ? value.ToString() : "";
        }
    }

    public sealed class MilkingRobotCommandEventArgs : EventArgs
    {
        public DateTime Time { get; private set; }
        public string ReceivedTopic { get; private set; }
        public string ResultTopic { get; private set; }
        public MilkingRobotCommand Command { get; private set; }
        public bool Ok { get; private set; }
        public string State { get; private set; }
        public string Message { get; private set; }
        public bool ResultPublished { get; private set; }
        public string Payload { get; private set; }
        public string ResultPayload { get; private set; }

        public MilkingRobotCommandEventArgs(DateTime time, string receivedTopic, string resultTopic, MilkingRobotCommand command, bool ok, string state, string message, bool resultPublished, string payload, string resultPayload)
        {
            Time = time; ReceivedTopic = receivedTopic; ResultTopic = resultTopic; Command = command; Ok = ok; State = state; Message = message; ResultPublished = resultPublished; Payload = payload; ResultPayload = resultPayload;
        }
    }
}
