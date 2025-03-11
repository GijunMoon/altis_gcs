using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Series;

namespace altis_gcs
{
    public partial class MainWindow : Window
    {
        private SerialCommunication _serialComm;
        private readonly DataProcessor _dataProcessor; //CSV 데이터 처리기
        private readonly ModelManager _modelManager;
        private readonly MapController _mapController;
        private readonly TimerManager _timerManager;
        private readonly Navigation _calculator; //비행 경로 연산기 

        public PlotModel CombinedAccelerationPlotModel { get; private set; } = new PlotModel { Title = "가속도 그래프" };
        public PlotModel GyroPlotModel { get; private set; } = new PlotModel { Title = "자이로 그래프" };

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            //_serialComm = new SerialCommunication("COM1", 9600); // 기본 포트와 보드레이트 /*보드레이트 변경 가능성 有*/
            //_serialComm.DataReceived += (s, data) => SystemLogs.Items.Add("Received: " + data);

            _dataProcessor = new DataProcessor();
            _modelManager = new ModelManager(ModelVisual, "C:/Users/문기준/source/repos/altis_gcs/altis_gcs/Models/rocket.obj");
            _mapController = new MapController(mapControl);
            _timerManager = new TimerManager();
            _timerManager.ElapsedTimeUpdated += (s, elapsed) => TimerDisplay.Text = elapsed.ToString(@"mm\:ss");

            RefreshPorts();
        }

        private void RefreshPorts()
        {
            SerialPortComboBox.Items.Clear();
            foreach (string port in SerialPort.GetPortNames())
            {
                SerialPortComboBox.Items.Add(port);
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (SerialPortComboBox.SelectedItem == null)
            {
                MessageBox.Show("포트를 선택하세요.");
                return;
            }

            string portName = SerialPortComboBox.SelectedItem.ToString();
            int baudRate = int.Parse(((ComboBoxItem)BaudRateComboBox.SelectedItem).Content.ToString());
            int dataBits = int.Parse(((ComboBoxItem)DataBitsComboBox.SelectedItem).Content.ToString());

            /*변수 구현만*/
            Parity parity = (Parity)Enum.Parse(typeof(Parity), ((ComboBoxItem)ParityComboBox.SelectedItem).Content.ToString());
            StopBits stopBits = (StopBits)Enum.Parse(typeof(StopBits), ((ComboBoxItem)StopBitsComboBox.SelectedItem).Content.ToString());

            _serialComm = new SerialCommunication(portName, baudRate);

            _serialComm.Connect();
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_serialComm == null || !_serialComm.isConnected)
                {
                    MessageBox.Show("Not connected to any port.");
                    return;
                } else
                {
                    _serialComm.Disconnect();
                    MessageBox.Show("연결 해제");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("포트 에러 발생: " + ex);
            }
        }

        private void SendSerialMessage_Click(object sender, RoutedEventArgs e)
        {
            string message = SerialMessageTextBox.Text;

            try
            {
                if (_serialComm != null)
                {
                    _serialComm.Send(message);
                    SystemLogs.Items.Add("Sent: " + message);
                }
                else
                {
                    MessageBox.Show("포트 연결되지 않음!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("메시지 전송 실패: " + ex.Message);
            }
        }


        private void FlightDataSave_Click(object sender, RoutedEventArgs e)
        {
            /*!비행시작 후 통신으로 들어오는 모든 데이터 저장하도록 로직 수정!*/
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Save Flight Data"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                List<string> data = new List<string>
                {
                    "Time,Altitude,Velocity,Latitude,Longitude"
                };
                /*비행데이터 컨트롤 로직 작성*/
                File.WriteAllLines(saveFileDialog.FileName, data);
            }
        }

        private async void SelectCsvFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "CSV 파일 선택"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                this.DataLabel.Visibility = Visibility.Hidden;
                var (accelX, accelY, accelZ, gyroX, gyroY, gyroZ) = await _dataProcessor.LoadCsvDataAsync(openFileDialog.FileName);

                CombinedAccelerationPlotModel = new PlotModel { Title = "가속도 그래프" };
                CombinedAccelerationPlotModel.Series.Add(new LineSeries { Title = "Accel X", ItemsSource = accelX });
                CombinedAccelerationPlotModel.Series.Add(new LineSeries { Title = "Accel Y", ItemsSource = accelY });
                CombinedAccelerationPlotModel.Series.Add(new LineSeries { Title = "Accel Z", ItemsSource = accelZ });

                GyroPlotModel = new PlotModel { Title = "자이로 그래프" };
                GyroPlotModel.Series.Add(new LineSeries { Title = "Gyro X", ItemsSource = gyroX });
                GyroPlotModel.Series.Add(new LineSeries { Title = "Gyro Y", ItemsSource = gyroY });
                GyroPlotModel.Series.Add(new LineSeries { Title = "Gyro Z", ItemsSource = gyroZ });

                Acceleration.Model = CombinedAccelerationPlotModel;
                Gyro.Model = GyroPlotModel;
            }
        }

        private void RollSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RollData.Text = $"{e.NewValue}°";
            _modelManager.UpdateTransform(RollSlider.Value, PitchSlider.Value, YawSlider.Value);
        }

        private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PitchData.Text = $"{e.NewValue}°";
            _modelManager.UpdateTransform(RollSlider.Value, PitchSlider.Value, YawSlider.Value);
        }

        private void YawSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            YawData.Text = $"{e.NewValue}°";
            _modelManager.UpdateTransform(RollSlider.Value, PitchSlider.Value, YawSlider.Value);
        }

        private void StartLaunch_Click(object sender, RoutedEventArgs e)
        {
            _timerManager.Start();
        }

        private void ResetSystem_Click(object sender, RoutedEventArgs e)
        {
            _timerManager.Reset();
            MessageBox.Show("시스템 리셋");
            /*시스템 리셋 로직 구현*/
        }

        private void AltitudeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            AltitudeData.Text = $"{e.NewValue:0} m";
        }

        private void VelocitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VelocityData.Text = $"{e.NewValue:0} m/s";
        }

        // 비상 버튼 클릭 이벤트
        private void Emergency_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("비상사출");
        }

        // 포트 새로고침 버튼 클릭 이벤트
        private void RefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
        }

        // 매개변수 업데이트 버튼 클릭 이벤트
        private void UpdateParameters_Click(object sender, RoutedEventArgs e)
        {
            if (_serialComm == null || !_serialComm.isConnected)
            {
                MessageBox.Show("Not connected to any port.");
                return;
            }

            try
            {
                string maxAltitude = MaxAltitudeTextBox.Text;
                string maxTime = MaxTimeTextBox.Text;
                string command = $"ALT:{maxAltitude},SPD:{maxTime}";
                _serialComm.Send(command);
                MessageBox.Show("Parameters updated.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update parameters: " + ex.Message);
            }
        }

        // 위도 슬라이더 값 변경 이벤트
        private void LatSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LatData.Text = $"{e.NewValue:F4}";
        }

        // 경도 슬라이더 값 변경 이벤트
        private void LongSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LongData.Text = $"{e.NewValue:F4}";
        }

        // 데이터 초기화 버튼 클릭 이벤트
        private void ResetData_Click(object sender, RoutedEventArgs e)
        {
            RollSlider.Value = -90;
            PitchSlider.Value = 0;
            YawSlider.Value = 0;
            LatSlider.Value = 0;
            LongSlider.Value = 0;
            _modelManager.UpdateTransform(RollSlider.Value, PitchSlider.Value, YawSlider.Value);
            RollSlider.Value = 0;
        }

        private void ShowPath_Click(object sender, RoutedEventArgs e)
        {
            RocketPathWindow popup = new RocketPathWindow();
            popup.Show();
        }
    }
}