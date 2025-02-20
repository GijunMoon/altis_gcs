using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Series;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using System.IO.Ports;
using HelixToolkit.Wpf;
using System.Windows.Threading;
using System.Text;

namespace altis_gcs
{
    public partial class MainWindow : Window
    {

        private SerialPort serialPort;

        // 타이머와 발사 시작 시간 저장
        private DispatcherTimer launchTimer;
        private DateTime launchStartTime;


        /// <summary>
        /// Map 표현에 대한 정의
        /// </summary>
        PointLatLng start;
        PointLatLng end;

        // marker
        GMapMarker currentMarker;

        // zones list
        List<GMapMarker> Circles = new List<GMapMarker>();

        /// <summary>
        /// Plot 표현에 대한 코드
        /// </summary>
        public PlotModel CombinedAccelerationPlotModel { get; private set; } = new PlotModel { Title = "가속도 그래프" };
        public PlotModel GyroPlotModel { get; private set; } = new PlotModel { Title = "자이로 그래프" };

        public MainWindow()
        {
            InitializeComponent();
            RefreshPorts();

            //로켓 obj 로드
            LoadObjFile("C:/Users/문기준/source/repos/altis_gcs/altis_gcs/Models/rocket.obj");
            var init_rollTransform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), -90));
            ModelVisual.Transform = init_rollTransform;


            DataContext = this;
            GoogleMapProvider.Instance.ApiKey = "AIzaSyCXJrDpszuNQfMEXKIifx5zYzhSq3Irpyg";

            // config map
            mapControl.MapProvider = GMapProviders.OpenStreetMap;
            mapControl.Position = new PointLatLng(35.1543, 128.0931);
            mapControl.MinZoom = 2;
            mapControl.MaxZoom = 17;
            mapControl.Zoom = 13;
            mapControl.ShowCenter = false;
            mapControl.DragButton = MouseButton.Left;
            mapControl.Position = new PointLatLng(35.1543, 128.0931);

            mapControl.MouseDoubleClick += new MouseButtonEventHandler(mapControl_MouseLeftButtonDown);

            // DispatcherTimer 초기화
            launchTimer = new DispatcherTimer();
            launchTimer.Interval = TimeSpan.FromSeconds(1); // 1초 간격 업데이트
            launchTimer.Tick += LaunchTimer_Tick;
        }

        void mapControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point clickPoint = e.GetPosition(mapControl);
            PointLatLng point = mapControl.FromLocalToLatLng((int)clickPoint.X, (int)clickPoint.Y);

            // Create a marker with a custom shape
            GMapMarker marker = new GMapMarker(point)
            {
                Shape = new Ellipse
                {
                    Width = 15,
                    Height = 15,
                    Fill = Brushes.Red,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2,
                    ToolTip = new ToolTip
                    {
                        Content = $"Lat: {point.Lat:F4}, Lng: {point.Lng:F4}"
                    }
                }
            };

            marker.Shape.MouseEnter += (s, ev) =>
            {
                var ellipse = s as Ellipse;
                ellipse.Fill = Brushes.Orange; // Change color on hover
            };

            marker.Shape.MouseLeave += (s, ev) =>
            {
                var ellipse = s as Ellipse;
                ellipse.Fill = Brushes.Red; // Revert color on leave
            };

            marker.Shape.MouseLeftButtonDown += (s, ev) =>
            {
                MessageBox.Show($"Marker clicked at: {point.Lat:F4}, {point.Lng:F4}");
                ev.Handled = true; // Prevent map from handling the click
            };

            mapControl.Markers.Add(marker);
        }

        private void Emergency_Click(object sender, EventArgs e)
        {
            MessageBox.Show("비상사출");
        }

        // "발사 시작" 버튼 클릭 시 호출되는 이벤트 핸들러
        private void StartLaunch_Click(object sender, RoutedEventArgs e)
        {
            // 타이머 시작 및 시작 시간 기록
            launchStartTime = DateTime.Now;
            launchTimer.Start();
        }

        // 타이머 Tick 이벤트: 매 초마다 경과 시간 업데이트
        private void LaunchTimer_Tick(object sender, EventArgs e)
        {
            TimeSpan elapsed = DateTime.Now - launchStartTime;
            // TimerDisplay 텍스트 업데이트 (분:초 형식)
            TimerDisplay.Text = elapsed.ToString(@"mm\:ss");
        }

        private void ResetSystem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("시스템 리셋");
        }

        private void AltitudeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 고도 슬라이더 값 변경 처리 로직
            AltitudeData.Text = e.NewValue.ToString("0") + " m";
        }

        private void VelocitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 속도 슬라이더 값 변경 처리 로직
            VelocityData.Text = e.NewValue.ToString("0") + " m/s";
        }
        private async void SelectCsvFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Select a CSV File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await LoadDataAndPlotAsync(openFileDialog.FileName);
            }
        }

        private async Task LoadDataAndPlotAsync(string filePath)
        {
            try
            {
                var dataPointsAccelX = new List<DataPoint>();
                var dataPointsAccelY = new List<DataPoint>();
                var dataPointsAccelZ = new List<DataPoint>();
                var dataPointsGyroX = new List<DataPoint>();
                var dataPointsGyroY = new List<DataPoint>();
                var dataPointsGyroZ = new List<DataPoint>();

                using (var reader = new StreamReader(filePath))
                {
                    string line;
                    bool isFirstLine = true;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (isFirstLine)
                        {
                            isFirstLine = false;
                            continue; // Skip the header line
                        }

                        try
                        {
                            var values = line.Split(',');
                            var time = double.Parse(values[0], CultureInfo.InvariantCulture);
                            var accelX = double.Parse(values[1], CultureInfo.InvariantCulture);
                            var accelY = double.Parse(values[2], CultureInfo.InvariantCulture);
                            var accelZ = double.Parse(values[3], CultureInfo.InvariantCulture);
                            var gyroX = double.Parse(values[4], CultureInfo.InvariantCulture); // Assuming gyro data starts from column 4
                            var gyroY = double.Parse(values[5], CultureInfo.InvariantCulture);
                            var gyroZ = double.Parse(values[6], CultureInfo.InvariantCulture);

                            dataPointsAccelX.Add(new DataPoint(time, accelX));
                            dataPointsAccelY.Add(new DataPoint(time, accelY));
                            dataPointsAccelZ.Add(new DataPoint(time, accelZ));
                            dataPointsGyroX.Add(new DataPoint(time, gyroX));
                            dataPointsGyroY.Add(new DataPoint(time, gyroY));
                            dataPointsGyroZ.Add(new DataPoint(time, gyroZ));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error parsing line: {line}. Exception: {ex.Message}");
                        }
                    }
                }

                // Create combined plot model for accelerations
                CombinedAccelerationPlotModel = new PlotModel { Title = "Combined Acceleration" };
                var accelSeriesX = new LineSeries { Title = "Accel X", StrokeThickness = 2, MarkerSize = 3, MarkerType = MarkerType.Circle };
                var accelSeriesY = new LineSeries { Title = "Accel Y", StrokeThickness = 2, MarkerSize = 3, MarkerType = MarkerType.Circle };
                var accelSeriesZ = new LineSeries { Title = "Accel Z", StrokeThickness = 2, MarkerSize = 3, MarkerType = MarkerType.Circle };
                accelSeriesX.ItemsSource = dataPointsAccelX;
                accelSeriesY.ItemsSource = dataPointsAccelY;
                accelSeriesZ.ItemsSource = dataPointsAccelZ;
                CombinedAccelerationPlotModel.Series.Add(accelSeriesX);
                CombinedAccelerationPlotModel.Series.Add(accelSeriesY);
                CombinedAccelerationPlotModel.Series.Add(accelSeriesZ);

                // Create plot model for gyro data
                GyroPlotModel = new PlotModel { Title = "Gyro Data" };
                var gyroSeriesX = new LineSeries { Title = "Gyro X", StrokeThickness = 2, MarkerSize = 3, MarkerType = MarkerType.Circle };
                var gyroSeriesY = new LineSeries { Title = "Gyro Y", StrokeThickness = 2, MarkerSize = 3, MarkerType = MarkerType.Circle };
                var gyroSeriesZ = new LineSeries { Title = "Gyro Z", StrokeThickness = 2, MarkerSize = 3, MarkerType = MarkerType.Circle };
                gyroSeriesX.ItemsSource = dataPointsGyroX;
                gyroSeriesY.ItemsSource = dataPointsGyroY;
                gyroSeriesZ.ItemsSource = dataPointsGyroZ;
                GyroPlotModel.Series.Add(gyroSeriesX);
                GyroPlotModel.Series.Add(gyroSeriesY);
                GyroPlotModel.Series.Add(gyroSeriesZ);

                // Bind models to the PlotViews
                Acceleration.Model = CombinedAccelerationPlotModel;
                Gyro.Model = GyroPlotModel;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while processing the file: {ex.Message}");
            }
        }

        // Define the event handlers for the sliders and reset button
        private void RollSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 텍스트 업데이트
            RollData.Text = $"{e.NewValue}°";

            UpdateTransform();
        }

        private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 텍스트 업데이트
            PitchData.Text = $"{e.NewValue}°";

            UpdateTransform();
        }

        private void YawSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            YawData.Text = $"{e.NewValue}°";
            UpdateTransform();
        }

        private void UpdateTransform()
        {
            // Roll, Pitch, Yaw 변환 생성
            var rollTransform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), RollSlider.Value));
            var pitchTransform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), PitchSlider.Value));
            var yawTransform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), YawSlider.Value));

            // Transform3DGroup을 사용하여 변환 결합
            var transformGroup = new Transform3DGroup();
            transformGroup.Children.Add(yawTransform); // Yaw 먼저 적용
            transformGroup.Children.Add(pitchTransform); // 그다음 Pitch
            transformGroup.Children.Add(rollTransform); // 마지막으로 Roll

            // ModelVisual에 변환 적용
            ModelVisual.Transform = transformGroup;
        }

        private void LatSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LatData.Text = $"{e.NewValue:F4}";
        }

        private void LongSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LongData.Text = $"{e.NewValue:F4}";
        }

        private void ResetData_Click(object sender, RoutedEventArgs e)
        {
            RollSlider.Value = -90;
            PitchSlider.Value = 0;
            YawSlider.Value = 0;
            LatSlider.Value = 0;
            LongSlider.Value = 0;

            UpdateTransform();

            RollSlider.Value = 0;
        }

        private void RefreshPorts()
        {
            SerialPortComboBox.Items.Clear();
            foreach (string port in SerialPort.GetPortNames())
            {
                SerialPortComboBox.Items.Add(port);
            }
        }
        private void RefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (SerialPortComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a port.");
                return;
            }

            string selectedPort = SerialPortComboBox.SelectedItem.ToString();
            serialPort = new SerialPort(selectedPort, 9600); // Baud rate 설정
            try
            {
                serialPort.Open();
                MessageBox.Show("Connected to " + selectedPort);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to connect: " + ex.Message);
            }
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
                MessageBox.Show("Disconnected");
            }
        }

        private void UpdateParameters_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("Not connected to any port.");
                return;
            }

            try
            {
                string maxAltitude = MaxAltitudeTextBox.Text;
                string maxSpeed = MaxSpeedTextBox.Text;

                // 예시: "ALT:1000,SPD:200" 형태로 전송
                string command = $"ALT:{maxAltitude},SPD:{maxSpeed}";
                serialPort.WriteLine(command);
                MessageBox.Show("Parameters updated.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update parameters: " + ex.Message);
            }
        }

        /// <summary>
        /// "전송" 버튼 클릭 시 호출되는 이벤트 핸들러.
        /// SerialMessageTextBox에 입력된 메시지를 아두이노로 전송합니다.
        /// </summary>
        private void SendSerialMessage_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    string message = SerialMessageTextBox.Text;
                    serialPort.WriteLine(message);
                    SystemLogs.Items.Add("Sent: " + message);
                }
                catch (Exception ex)
                {
                    SystemLogs.Items.Add("Error sending message: " + ex.Message);
                }
            }
            else
            {
                SystemLogs.Items.Add("Serial port is not connected.");
            }
        }

        /// <summary>
        /// 시리얼 포트로부터 데이터가 수신되면 호출되는 이벤트 핸들러.
        /// 수신된 데이터를 UI 스레드로 전달하여 SystemLogs에 출력합니다.
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string incomingData = serialPort.ReadLine();
                // UI 업데이트는 Dispatcher를 통해 실행 (DataReceived는 백그라운드 스레드에서 호출됨)
                Dispatcher.Invoke(() =>
                {
                    SystemLogs.Items.Add("Received: " + incomingData);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    SystemLogs.Items.Add("Error receiving data: " + ex.Message);
                });
            }
        }


        private void LoadObjFile(string filePath)
        {
            // ObjReader 인스턴스 생성
            var objReader = new ObjReader();

            try
            {
                // OBJ 파일 로드
                var model = objReader.Read(filePath);

                ModelVisual.Content = model;
            }
            catch (Exception ex) {
                MessageBox.Show("OBJ 파일을 로드할 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
