using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;

namespace altis_gcs
{
    public partial class RocketPathWindow : Window
    {
        private readonly Navigation inertialNav = new Navigation();
        private List<PointLatLng> insPathPoints = new List<PointLatLng>(); // 관성항법 경로
        private List<PointLatLng> gpsPathPoints = new List<PointLatLng>(); // GPS 경로

        public RocketPathWindow()
        {
            InitializeComponent();
            InitializeMap();
        }

        private void InitializeMap()
        {
            mapControl.MapProvider = GMapProviders.OpenStreetMap;
            mapControl.Position = new PointLatLng(34.61013470769485, 127.20767755769276); // 고흥 우주센터
            mapControl.MinZoom = 1;
            mapControl.MaxZoom = 18;
            mapControl.Zoom = 13;
        }

        private void AddPoint_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(latTextBox.Text, out double lat) && double.TryParse(lngTextBox.Text, out double lng))
            {
                PointLatLng newPoint = new PointLatLng(lat, lng);
                gpsPathPoints.Add(newPoint);
                UpdatePath(gpsPathPoints, System.Windows.Media.Brushes.Blue, "GPS Path");
                mapControl.Position = newPoint;
            }
            else
            {
                MessageBox.Show("유효한 위도와 경도를 입력하세요!");
            }
        }

        private void UpdatePath(List<PointLatLng> points, System.Windows.Media.Brush color, string name)
        {
            if (points.Count > 1)
            {
                var route = new GMapRoute(points)
                {
                    Shape = new System.Windows.Shapes.Path { Stroke = color, StrokeThickness = 2 }
                };
                mapControl.Markers.Add(route);
            }
        }

        private void ClearPaths_Click(object sender, RoutedEventArgs e)
        {
            mapControl.Markers.Clear();
            insPathPoints.Clear();
            gpsPathPoints.Clear();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // 관성항법 경로 테스트
        private void TestInsPath_Click(object sender, RoutedEventArgs e)
        {
            var sensorData = GenerateTestSensorData(50);
            var startPoint = new PointLatLng(34.61013470769485, 127.20767755769276);

            insPathPoints = inertialNav.CalculatePath(sensorData, startPoint);

            mapControl.Markers.Clear();
            UpdatePath(insPathPoints, System.Windows.Media.Brushes.Red, "INS Path");
         
        }

        // GPS 경로 테스트
        private void TestGpsPath_Click(object sender, RoutedEventArgs e)
        {
            gpsPathPoints = GenerateTestGpsPath(50, new PointLatLng(34.61013470769485, 127.20767755769276));

            mapControl.Markers.Clear();
            UpdatePath(gpsPathPoints, System.Windows.Media.Brushes.Blue, "GPS Path");
            
        }

        // 두 경로 모두 테스트
        private void TestBothPaths_Click(object sender, RoutedEventArgs e)
        {
            var sensorData = GenerateTestSensorData(50);
            var startPoint = new PointLatLng(34.61013470769485, 127.20767755769276);

            insPathPoints = inertialNav.CalculatePath(sensorData, startPoint);
            gpsPathPoints = GenerateTestGpsPath(50, startPoint);

            mapControl.Markers.Clear();
            UpdatePath(insPathPoints, System.Windows.Media.Brushes.Red, "INS Path");
            UpdatePath(gpsPathPoints, System.Windows.Media.Brushes.Blue, "GPS Path");
            mapControl.ZoomAndCenterMarkers(null);
        }

        // 관성항법 테스트 데이터 생성
        private List<SensorData> GenerateTestSensorData(int count)
        {
            var dataList = new List<SensorData>();
            Random rand = new Random();
            double accelX = 0.1; // 초기 X축 가속
            double accelY = 0.0; // 초기 Y축 가속
            double gyroZ = 0.02; // Z축 회전 (요)

            for (int i = 0; i < count; i++)
            {
                // 간단한 시나리오: X축 가속 후 Y축으로 방향 전환
                if (i > count / 2)
                {
                    accelX -= 0.002; // X축 가속 감소
                    accelY += 0.002; // Y축 가속 증가
                    gyroZ = 0.01;    // 방향 전환
                }

                dataList.Add(new SensorData
                {
                    AccelX = accelX + (rand.NextDouble() * 0.02 - 0.01), // 노이즈 추가
                    AccelY = accelY + (rand.NextDouble() * 0.02 - 0.01),
                    AccelZ = rand.NextDouble() * 0.02 - 0.01,
                    GyroX = rand.NextDouble() * 0.002 - 0.001,
                    GyroY = rand.NextDouble() * 0.002 - 0.001,
                    GyroZ = gyroZ + (rand.NextDouble() * 0.002 - 0.001),
                    DeltaTime = 1.0
                });
            }
            return dataList;
        }

        // GPS 테스트 데이터 생성
        private List<PointLatLng> GenerateTestGpsPath(int count, PointLatLng startPoint)
        {
            var path = new List<PointLatLng> { startPoint };
            Random rand = new Random();
            double currentLat = startPoint.Lat;
            double currentLng = startPoint.Lng;

            for (int i = 0; i < count - 1; i++)
            {
                // 간단한 시나리오: 북동쪽으로 이동 후 남동쪽으로 방향 전환
                double deltaLat = (i < count / 2 ? 0.0001 : 0.00005) + (rand.NextDouble() * 0.00002 - 0.00001);
                double deltaLng = (i < count / 2 ? 0.0001 : 0.00015) + (rand.NextDouble() * 0.00002 - 0.00001);

                currentLat += deltaLat;
                currentLng += deltaLng;
                path.Add(new PointLatLng(currentLat, currentLng));
            }
            return path;
        }
    }
}