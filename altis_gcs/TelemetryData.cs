using System;
using System.Collections.Generic;
using System.Linq;

namespace altis_gcs
{
    public class TelemetryData
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> Parameters { get; set; } = new Dictionary<string, double>();

        // 쿼터니언 데이터 (x, y, z, w 순서로 파싱)
        public double QuaternionX { get; set; }
        public double QuaternionY { get; set; }
        public double QuaternionZ { get; set; }
        public double QuaternionW { get; set; } // 실수부

        // 가속도계 데이터
        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }

        // 자이로스코프 데이터
        public double GyroX { get; set; }
        public double GyroY { get; set; }
        public double GyroZ { get; set; }

        // 시간, 고도, 속도 등 다른 주요 값들을 직접 속성으로 가질 수도 있습니다.
        public long Time { get; set; } // 타임스탬프 (millisec)
        public double Altitude { get; set; }
        public double Velocity { get; set; }


        public TelemetryData()
        {
            // ParameterDictionary는 필요한 경우 계속 사용
            Parameters = new Dictionary<string, double>();
        }
    }
}
