using System;
using System.Windows;
using HelixToolkit.Wpf;

namespace altis_gcs
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 초기화 및 데이터 바인딩
            InitializeGraph();

            //로켓 모델 호출
            LoadRocketModel();
        }

        private void InitializeGraph()
        {
            // LiveCharts 예제 그래프 데이터 초기화
            var series = new LiveCharts.Wpf.LineSeries
            {
                Title = "Accelerometer X",
                Values = new LiveCharts.ChartValues<double> { 3, 4, 6, 3, 8 }
            };
            AccelerometerChart.Series.Add(series);
        }

        private void LoadRocketModel()
        {
            var importer = new ObjReader();

            // 절대 경로 설정
            var modelPath = @"C:\Users\문기준\source\repos\altis_gcs\altis_gcs\Models\rocket.obj";
            if (!System.IO.File.Exists(modelPath))
            {
                MessageBox.Show($"모델 파일이 존재하지 않습니다: {modelPath}");
                return;
            }

            var model = importer.Read(modelPath);

            RocketModel.Content = model;
        }




    }
}
