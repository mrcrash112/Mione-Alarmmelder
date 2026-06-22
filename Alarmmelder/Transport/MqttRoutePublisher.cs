using System;
using MioneAlarmmelder.Core;

namespace MioneAlarmmelder.Transport
{
    public static class MqttRoutePublisher
    {
        public static MqttRoutePublishResult Publish(AppSettings settings, string subTopic, string payload, bool retain)
        {
            MqttRoutePublishResult result = new MqttRoutePublishResult();
            if (settings == null) return result;

            if (settings.SystemMqttReady)
            {
                result.SystemEnabled = true;
                try
                {
                    MqttPublisher.Publish(SystemMqtt.Host, SystemMqtt.Port, SystemMqtt.User, SystemMqtt.Password, Topic(settings.SystemMqttTopicRoot, subTopic), payload, retain);
                    result.SystemSuccessful = true;
                }
                catch (Exception ex)
                {
                    result.SystemError = ex.Message;
                }
            }

            if (settings.BackupMqttConfigured)
            {
                result.BackupEnabled = true;
                try
                {
                    MqttPublisher.Publish(settings.MqttHost, settings.MqttPort, settings.MqttUser, settings.MqttPassword, Topic(settings.BackupMqttTopicRoot, subTopic), payload, retain);
                    result.BackupSuccessful = true;
                }
                catch (Exception ex)
                {
                    result.BackupError = ex.Message;
                }
            }

            return result;
        }

        private static string Topic(string root, string subTopic)
        {
            string top = (root ?? "").Trim().Trim('/');
            if (top.Length == 0) throw new InvalidOperationException("MQTT-Top-Topic fehlt.");
            return top + "/" + subTopic.TrimStart('/');
        }
    }

    public sealed class MqttRoutePublishResult
    {
        public bool SystemEnabled { get; set; }
        public bool SystemSuccessful { get; set; }
        public string SystemError { get; set; }
        public bool BackupEnabled { get; set; }
        public bool BackupSuccessful { get; set; }
        public string BackupError { get; set; }
    }
}
