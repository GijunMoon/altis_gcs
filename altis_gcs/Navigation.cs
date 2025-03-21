using System;
using System.Collections.Generic;
using GMap.NET;

namespace altis_gcs
{
    public class SensorData
    {
        public double AccelX { get; set; }  // m/s²
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
        public double GyroX { get; set; }   // rad/s
        public double GyroY { get; set; }
        public double GyroZ { get; set; }
        public double DeltaTime { get; set; } // 초
    }

    public class Navigation
    {
        private const double Gravity = 9.81; // 중력 가속도 (m/s²)
        private double[] position = { 0, 0, 0 }; // 누적 위치 (x, y, z) in meters
        private double[] velocity = { 0, 0, 0 }; // 초기 속도
        private double[] angles = { 0, 0, 0 };   // 롤, 피치, 요 (radians)

        public List<PointLatLng> CalculatePath(List<SensorData> sensorData, PointLatLng startPoint)
        {
            List<PointLatLng> path = new List<PointLatLng> { startPoint }; // 시작점 추가

            // 현재 위도/경도 (시작점에서 시작)
            double currentLat = startPoint.Lat;
            double currentLng = startPoint.Lng;

            // 이전 위치 저장 (미터 단위)
            double previousX = 0;
            double previousY = 0;

            foreach (var data in sensorData)
            {
                // 1. 자이로로 자세 계산 (각도 누적)
                angles[0] += data.GyroX * data.DeltaTime; // 롤
                angles[1] += data.GyroY * data.DeltaTime; // 피치
                angles[2] += data.GyroZ * data.DeltaTime; // 요

                // 2. 중력 보정 (간단히 Z축만 보정, 실제로는 회전 행렬 필요)
                var correctedAccel = new double[]
                {
                    data.AccelX,
                    data.AccelY,
                    data.AccelZ + Gravity // Z축에 중력 보정
                };

                // 3. 속도와 위치 업데이트 (누적)
                for (int i = 0; i < 3; i++)
                {
                    velocity[i] += correctedAccel[i] * data.DeltaTime;
                    position[i] += velocity[i] * data.DeltaTime;
                }

                // 4. 상대 이동 거리 계산 (이전 위치와의 차이)
                double deltaX = position[0] - previousX;
                double deltaY = position[1] - previousY;

                // 5. 상대 위치를 위도/경도로 변환
                // 1도 ≈ 111,139미터 (위도), 경도는 현재 위도에 따라 조정
                double deltaLat = deltaX / 111139.0; // X축 이동을 위도로 변환
                double deltaLng = deltaY / (111139.0 * Math.Cos(currentLat * Math.PI / 180)); // Y축 이동을 경도로 변환

                // 6. 현재 위치 갱신
                currentLat += deltaLat;
                currentLng += deltaLng;

                // 새로운 위치 추가
                path.Add(new PointLatLng(currentLat, currentLng));

                // 7. 이전 위치 갱신
                previousX = position[0];
                previousY = position[1];
            }

            return path;
        }
    }
}