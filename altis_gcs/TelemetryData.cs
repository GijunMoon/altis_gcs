using System;
using System.Collections.Generic;
using System.Linq;

namespace altis_gcs
{
    public class TelemetryData
    {
        public Dictionary<string, double> Parameters { get; set; } = new Dictionary<string, double>();
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            Timestamp = DateTime.Now; // 현재 시간으로 설정
            var paramStrings = string.Join(", ", Parameters.Select(p => $"{p.Key}: {p.Value}"));
            return $"{Timestamp}: {paramStrings}";
        }
    }
}
