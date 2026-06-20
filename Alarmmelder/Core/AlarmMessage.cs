using System;

namespace MioneAlarmmelder.Core
{
    public sealed class AlarmMessage
    {
        public string DateText { get; set; }
        public string TimeText { get; set; }
        public string Code { get; set; }
        public string Location { get; set; }
        public string CowNumber { get; set; }
        public string Priority { get; set; }
        public string ClearText { get; set; }
        public string Cause { get; set; }
        public string Solution { get; set; }

        public static bool TryParse(string line, out AlarmMessage alarm)
        {
            alarm = null;
            if (String.IsNullOrEmpty(line)) return false;
            string[] values = line.Split(new char[] { ';' });
            if (values.Length < 5) return false;
            for (int i = 0; i < values.Length; i++) values[i] = values[i].Trim();
            if (values[0].Length == 0 || values[1].Length == 0 || values[2].Length == 0) return false;
            alarm = new AlarmMessage
            {
                DateText = values[0], TimeText = values[1], Code = values[2],
                Location = values[3], CowNumber = values[4], Priority = "System", ClearText = "Alarmtext nicht gefunden", Cause = "", Solution = ""
            };
            return true;
        }
    }
}
