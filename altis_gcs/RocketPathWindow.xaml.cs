using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;

namespace altis_gcs
{
    /*TODO*/
    /* 위성항법 경로 표현, 관성항법 경로 표현 따로 구현되어 있음.
     테스트 끝나는대로 메인탭에 통합 예정 */
    public partial class RocketPathWindow : Window
    {
        private readonly Navigation inertialNav = new Navigation();

        private List<PointLatLng> pathPoints = new List<PointLatLng>();

        public RocketPathWindow()
        {
            InitializeComponent();
            InitializeMap();
        }

        private void InitializeMap()
        {
            mapControl.MapProvider = GMapProviders.OpenStreetMap; // 오픈스트리트맵 사용
            mapControl.Position = new PointLatLng(34.61013470769485, 127.20767755769276); // 초기 위치 (고흥 우주센터)
            mapControl.MinZoom = 1;
            mapControl.MaxZoom = 18;
            mapControl.Zoom = 13; // 초기 줌 레벨
        }

        private void AddPoint_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(latTextBox.Text, out double lat) && double.TryParse(lngTextBox.Text, out double lng))
            {
                PointLatLng newPoint = new PointLatLng(lat, lng);
                pathPoints.Add(newPoint);
                UpdatePath();
                mapControl.Position = newPoint; // 지도 중심 이동
            }
            else
            {
                MessageBox.Show("유효한 위도와 경도를 입력하세요!");
            }
        }

        private void UpdatePath()
        {
            if (pathPoints.Count > 1)
            {
                var route = new GMapRoute(pathPoints)
                {
                    Shape = new System.Windows.Shapes.Path { Stroke = System.Windows.Media.Brushes.Red, StrokeThickness = 2 }
                };
                mapControl.Markers.Clear();
                mapControl.Markers.Add(route);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


        private void TestPath_Click(object sender, RoutedEventArgs e)
        {
            // 테스트 데이터 생성
            var sensorData = GenerateTestSensorData(50); // 50개 데이터 포인트
            var startPoint = new PointLatLng(34.61013470769485, 127.20767755769276); // 시작 위치

            // 경로 계산
            var path = inertialNav.CalculatePath(sensorData, startPoint);

            // 지도에 경로 표시
            if (path.Count > 1)
            {
                var route = new GMapRoute(path);
                route.Shape = new System.Windows.Shapes.Path
                {
                    Stroke = System.Windows.Media.Brushes.Red,
                    StrokeThickness = 2
                };
                mapControl.Markers.Clear();
                mapControl.Markers.Add(route);
                //mapControl.ZoomAndCenterMarkers(null); // 경로에 맞게 줌 조정
            }
        }
        // 테스트 데이터 생성 메서드
        private List<SensorData> GenerateTestSensorData(int count)
        {
            // 위에서 정의한 메서드 삽입
            var dataList = new List<SensorData>();
            Random rand = new Random();
            for (int i = 0; i < count; i++)
            {
                dataList.Add(new SensorData
                {
                    AccelX = rand.NextDouble() * 0.2 - 0.1,
                    AccelY = rand.NextDouble() * 0.2 - 0.1,
                    AccelZ = rand.NextDouble() * 0.2 - 0.1,
                    GyroX = rand.NextDouble() * 0.02 - 0.01,
                    GyroY = rand.NextDouble() * 0.02 - 0.01,
                    GyroZ = rand.NextDouble() * 0.02 - 0.01,
                    DeltaTime = 0.1
                });
            }
            return dataList;
        }
    }
}