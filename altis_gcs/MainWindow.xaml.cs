﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Series;

namespace altis_gcs
{
    public partial class MainWindow : Window
    {
        private SerialCommunication _serialComm;
        private CancellationTokenSource _dataProcessingCts;

        private readonly DataProcessor _dataProcessor; //CSV 데이터 처리기
        private readonly ModelManager _modelManager;
        private readonly MapController _mapController;
        private readonly TimerManager _timerManager;


        /* 그래프 vars */
        public PlotModel CombinedAccelerationPlotModel { get; private set; } = new PlotModel { Title = "가속도 그래프" };
        public PlotModel GyroPlotModel { get; private set; } = new PlotModel { Title = "자이로 그래프" };

        private readonly ObservableCollection<DataPoint> _accelXPoints = new ObservableCollection<DataPoint>();
        private readonly ObservableCollection<DataPoint> _accelYPoints = new ObservableCollection<DataPoint>();
        private readonly ObservableCollection<DataPoint> _accelZPoints = new ObservableCollection<DataPoint>();
        private readonly ObservableCollection<DataPoint> _gyroXPoints = new ObservableCollection<DataPoint>();
        private readonly ObservableCollection<DataPoint> _gyroYPoints = new ObservableCollection<DataPoint>();
        private readonly ObservableCollection<DataPoint> _gyroZPoints = new ObservableCollection<DataPoint>();

        private double _timeCounter = 0;
        private readonly int _maxPoints = 100; // 표시할 최대 데이터 포인트 수

        private readonly List<TelemetryData> _flightDataLog = new List<TelemetryData>(); //데이터 저장용


        /*----------------------------------------------------------------------------------------------*/

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            //_serialComm = new SerialCommunication("COM1", 9600); // 기본 포트와 보드레이트 /*보드레이트 변경 가능성 有*/
            //_serialComm.DataReceived += (s, data) => SystemLogs.Items.Add("Received: " + data);

            _dataProcessor = new DataProcessor();
            _modelManager = new ModelManager(ModelVisual);
            _mapController = new MapController(mapControl);
            _timerManager = new TimerManager();
            _timerManager.ElapsedTimeUpdated += (s, elapsed) => TimerDisplay.Text = elapsed.ToString(@"mm\:ss");
            _dataProcessingCts = new CancellationTokenSource();

            RefreshPorts();

            // 그래프 모델 초기화
            CombinedAccelerationPlotModel = new PlotModel { Title = "가속도 그래프" };
            CombinedAccelerationPlotModel.Series.Add(new LineSeries { Title = "Accel X", ItemsSource = _accelXPoints });
            CombinedAccelerationPlotModel.Series.Add(new LineSeries { Title = "Accel Y", ItemsSource = _accelYPoints });
            CombinedAccelerationPlotModel.Series.Add(new LineSeries { Title = "Accel Z", ItemsSource = _accelZPoints });

            GyroPlotModel = new PlotModel { Title = "자이로 그래프" };
            GyroPlotModel.Series.Add(new LineSeries { Title = "Gyro X", ItemsSource = _gyroXPoints });
            GyroPlotModel.Series.Add(new LineSeries { Title = "Gyro Y", ItemsSource = _gyroYPoints });
            GyroPlotModel.Series.Add(new LineSeries { Title = "Gyro Z", ItemsSource = _gyroZPoints });

            Acceleration.Model = CombinedAccelerationPlotModel;
            Gyro.Model = GyroPlotModel;

        }

        private void RefreshPorts()
        {
            PortComboBox.Items.Clear();
            foreach (string port in SerialPort.GetPortNames())
            {
                PortComboBox.Items.Add(port);
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (PortComboBox.SelectedItem == null || BaudRateComboBox.SelectedItem == null)
            {
                MessageBox.Show("모든 설정 값을 선택하세요!");
                return;
            }

            try
            {
                Debug.WriteLine("Connecting to port...");
                string portName = PortComboBox.SelectedItem.ToString();
                int baudRate = int.Parse(((ComboBoxItem)BaudRateComboBox.SelectedItem).Content.ToString());

                _serialComm = new SerialCommunication(portName, baudRate);

                var currentSettings = new ParameterSettings
                {
                    ParameterOrder = ParameterOrderTextBox.Text.Split(',').Select(p => p.Trim()).ToList(),
                    CommType = (PlainTextRadioButton.IsChecked == true)
                        ? CommunicationType.Text
                        : CommunicationType.Binary
                };
                _serialComm.SetParameterSettings(currentSettings);

                // 이벤트 핸들러
                _serialComm.DataReceived += (s, data) =>
                {
                    Dispatcher.Invoke(() => SystemLogs.Items.Add(data));
                };
                _serialComm.TelemetryDataParsed += (s, data) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        SystemLogs.Items.Add(data.ToString());
                        // ScrollViewer 강제 스크롤 다운
                        var scrollViewer = FindDescendant<ScrollViewer>(SystemLogs);
                        scrollViewer?.ScrollToEnd();

                        // UI 업데이트 (예: Roll, Pitch, Yaw 표시)
                        if (data.Parameters.ContainsKey("Roll")) RollData.Text = data.Parameters["Roll"].ToString();
                        if (data.Parameters.ContainsKey("Pitch")) PitchData.Text = data.Parameters["Pitch"].ToString();
                        if (data.Parameters.ContainsKey("Yaw")) YawData.Text = data.Parameters["Yaw"].ToString();


                        OnTelemetryDataParsed(sender, data); //바이너리 파싱
                    });
                };
                _serialComm.Connect();

                if (currentSettings.CommType == CommunicationType.Text) {
                    Task.Run(() => _serialComm.ProcessLinesAsync(_dataProcessingCts.Token)); // 텍스트 모드일 때만 데이터 파싱 태스크 시작
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"연결 실패: {ex.Message}");
            }
        }

        public static T FindDescendant<T>(DependencyObject d) where T : DependencyObject
        {
            if (d == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(d, i);

                if (child is T t)
                    return t;

                T descendant = FindDescendant<T>(child);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }


        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_serialComm == null || !_serialComm.IsConnected)
                {
                    MessageBox.Show("연결 된 포트가 없습니다.");
                    return;
                }
                else
                {
                    _dataProcessingCts.Cancel();
                    _serialComm?.Disconnect();
                    _dataProcessingCts = new CancellationTokenSource();
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
                if (_serialComm != null && !string.IsNullOrEmpty(message))
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

        private void ApplyParameterSettings_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ParameterOrderTextBox.Text))
            {
                MessageBox.Show("파라미터 순서를 입력하세요!");
                return;
            }

            var settings = new ParameterSettings
            {
                ParameterOrder = ParameterOrderTextBox.Text.Split(',').Select(p => p.Trim()).ToList(),

                            // UI의 라디오 버튼 선택에 따라 통신 방식 설정
                CommType = (PlainTextRadioButton.IsChecked == true)
                    ? CommunicationType.Text
                    : CommunicationType.Binary
            };
            _serialComm?.SetParameterSettings(settings);
            MessageBox.Show("파라미터 설정이 적용되었습니다.");
        }

        private double _prevYaw = 0.0;
        private double _prevTime = 0.0; // 시간 누적 (초 단위)
        private double _currentYaw = 0.0;

        private void OnTelemetryDataParsed(object sender, TelemetryData data)
        {
            this.DataLabel.Visibility = Visibility.Hidden; //안내 레이블 숨김

            // UI 스레드에서 실행
            Dispatcher.Invoke(() =>
            {
                // 시간 계산 (예: 0.1초 간격)
                double deltaTime = 0.1;
                _prevTime += deltaTime;

                // 가속도/자이로 값 추출
                double ax = data.Parameters.TryGetValue("AccelX", out var v1) ? v1 : 0;
                double ay = data.Parameters.TryGetValue("AccelY", out var v2) ? v2 : 0;
                double az = data.Parameters.TryGetValue("AccelZ", out var v3) ? v3 : 0;
                double gz = data.Parameters.TryGetValue("GyroZ", out var v4) ? v4 : 0;

                double velocity = data.Parameters.TryGetValue("Velocity", out var v5) ? v5 : 0;
                double altitude = data.Parameters.TryGetValue("Altitude", out var v6) ? v6 : 0;

                double g = 9.81;
                double G = Math.Sqrt(ax * ax + ay * ay + az * az) / g;

                // Pitch, Roll 계산 (라디안 → 도)
                double pitch = Math.Atan2(-ax, Math.Sqrt(ay * ay + az * az)) * 180.0 / Math.PI;
                double roll = Math.Atan2(ay, az) * 180.0 / Math.PI;

                // Yaw 계산 (적분)
                _currentYaw += gz * deltaTime; // GyroZ는 rad/s 또는 deg/s 단위, 센서에 맞게 보정 필요

                // UI 표시
                RollData.Text = roll.ToString("F2");
                PitchData.Text = pitch.ToString("F2");
                YawData.Text = _currentYaw.ToString("F2");

                VelocityData.Text = velocity.ToString("F2");
                AltitudeData.Text = altitude.ToString("F2");

                GData.Text = G.ToString("F2");

                _modelManager.UpdateTransform(roll, pitch, _currentYaw);

                // 데이터 포인트 추가
                if (data.Parameters.TryGetValue("AccelX", out double accelX))
                    AddDataPoint(_accelXPoints, _timeCounter, accelX);

                if (data.Parameters.TryGetValue("AccelY", out double accelY))
                    AddDataPoint(_accelYPoints, _timeCounter, accelY);

                if (data.Parameters.TryGetValue("AccelZ", out double accelZ))
                    AddDataPoint(_accelZPoints, _timeCounter, accelZ);

                if (data.Parameters.TryGetValue("GyroX", out double gyroX))
                    AddDataPoint(_gyroXPoints, _timeCounter, gyroX);

                if (data.Parameters.TryGetValue("GyroY", out double gyroY))
                    AddDataPoint(_gyroYPoints, _timeCounter, gyroY);

                if (data.Parameters.TryGetValue("GyroZ", out double gyroZ))
                    AddDataPoint(_gyroZPoints, _timeCounter, gyroZ);

                _timeCounter += 0.1; // 시간 증가 (실제 시간 간격에 맞게 조정)

                // 그래프 업데이트
                CombinedAccelerationPlotModel.InvalidatePlot(true);
                GyroPlotModel.InvalidatePlot(true);

                // 비행 데이터 저장
                _flightDataLog.Add(data);
            });
        }

        private void AddDataPoint(ObservableCollection<DataPoint> points, double x, double y)
        {
            // 최대 포인트 수 제한
            if (points.Count >= _maxPoints)
                points.RemoveAt(0);

            points.Add(new DataPoint(x, y));
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
                    "Time,Altitude,Velocity,AccelX,AccelY,AccelZ,GyroX,GyroY,GyroZ,QuaternionX,QuaternionY,QuaternionZ,ftv_ej1,ftv_ej2,ftv_ej3"
                };
                /*비행데이터 컨트롤 로직 작성*/
                foreach(var telemetryData in _flightDataLog)
                {
                    string line = $"{telemetryData.Time}," +
                                  $"{telemetryData.Altitude}," +
                                  $"{telemetryData.Velocity}," +
                                  $"{telemetryData.AccelX}," +
                                  $"{telemetryData.AccelY}," +
                                  $"{telemetryData.AccelZ}," +
                                  $"{telemetryData.GyroX}," +
                                  $"{telemetryData.GyroY}," +
                                  $"{telemetryData.GyroZ}," +
                                  $"{telemetryData.QuaternionX}," +
                                  $"{telemetryData.QuaternionY}," +
                                  $"{telemetryData.QuaternionZ}," +
                                  $"{telemetryData.QuaternionW}," +
                                  $"{(telemetryData.ftv_ej1 ? 1 : 0)}," +
                                  $"{(telemetryData.ftv_ej2 ? 1 : 0)}," +
                                  $"{(telemetryData.ftv_ej3 ? 1 : 0)}";
                    data.Add(line);
                }
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
                var (accelX, accelY, accelZ, gyroX, gyroY, gyroZ, quatX, quatY, quatZ, quatW, telemData) = await _dataProcessor.LoadCsvDataAsync(openFileDialog.FileName);

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

        /*레거시 삭제됨*/

        private void StartLaunch_Click(object sender, RoutedEventArgs e)
        {
            _timerManager.Start();
        }

        private void ResetSystem_Click(object sender, RoutedEventArgs e)
        {
            _timerManager.Reset();
            MessageBox.Show("미구현 기능입니다.");
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


        /* 사 출 로 직 */

        // 비상 버튼 클릭 이벤트
        private void Emergency_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("비상사출");
            /* 시험 발사 이전까지 비상 사출 신호 전달 로직 반드시 구현할 것 ! */

            string rawPayload = "EJECT";
            //string atCommand = $"AT+SEND=0,{rawPayload.Length},{rawPayload}";


            try
            {
                if (_serialComm != null && !string.IsNullOrEmpty(rawPayload))
                {
                    _serialComm.Send(rawPayload);
                    SystemLogs.Items.Add("Sent: " + rawPayload);
                }
                else
                {
                    MessageBox.Show("포트 연결되지 않음!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("비상사출 실패: " + ex.Message);
            }
        }

        private bool[] servoStates = new bool[5]; // 각 서보의 상태 저장

        private void SetServoIndicator(int servoIndex, bool isActive)
        {
            // 각 Indicator 이름에 맞게 처리
            Ellipse indicator = null;
            switch (servoIndex)
            {
                case 0: indicator = Servo1Indicator; break;
                case 1: indicator = Servo2Indicator; break;
                case 2: indicator = Servo3Indicator; break;
                case 3: indicator = Servo4Indicator; break;
                case 4: indicator = Servo5Indicator; break;
            }
            if (indicator != null)
                indicator.Fill = isActive ? Brushes.LimeGreen : Brushes.Red;
        }

        private void Servo1_Click(object sender, RoutedEventArgs e)
        {
            if (_serialComm != null && _serialComm.IsConnected)
            {
                servoStates[0] = !servoStates[0]; // 토글(ON/OFF)
                SetServoIndicator(0, servoStates[0]);

                _serialComm.Send("SERVO1");
                SystemLogs.Items.Add("Servo 1 activated.");
            }
            else
            {
                MessageBox.Show("Not connected to any port.");
            }
        }

        private void Servo2_Click(object sender, RoutedEventArgs e)
        {

            if (_serialComm != null && _serialComm.IsConnected)
            {
                servoStates[1] = !servoStates[1]; // 토글(ON/OFF)
                SetServoIndicator(1, servoStates[1]);

                _serialComm.Send("SERVO2");
                SystemLogs.Items.Add("Servo 2 activated.");
            }
            else
            {
                MessageBox.Show("Not connected to any port.");
            }
        }

        private void Servo3_Click(object sender, RoutedEventArgs e)
        {

            if (_serialComm != null && _serialComm.IsConnected)
            {
                servoStates[2] = !servoStates[2];
                SetServoIndicator(2, servoStates[2]);

                _serialComm.Send("SERVO3");
                SystemLogs.Items.Add("Servo 3 activated.");
            }
            else
            {
                MessageBox.Show("Not connected to any port.");
            }
        }

        private void Servo4_Click(object sender, RoutedEventArgs e)
        {

            if (_serialComm != null && _serialComm.IsConnected)
            {
                servoStates[3] = !servoStates[3];
                SetServoIndicator(3, servoStates[3]);

                _serialComm.Send("SERVO4");
                SystemLogs.Items.Add("Servo 4 activated.");
            }
            else
            {
                MessageBox.Show("Not connected to any port.");
            }
        }

        private void Servo5_Click(object sender, RoutedEventArgs e)
        {

            if (_serialComm != null && _serialComm.IsConnected)
            {
                servoStates[4] = !servoStates[4];
                SetServoIndicator(4, servoStates[4]);

                _serialComm.Send("SERVO5");
                SystemLogs.Items.Add("Servo 5 activated.");
            }
            else
            {
                MessageBox.Show("Not connected to any port.");
            }
        }

        // 포트 새로고침 버튼 클릭 이벤트
        private void RefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
        }

        /*레거시*/
        // 매개변수 업데이트 버튼 클릭 이벤트
        /*private void UpdateParameters_Click(object sender, RoutedEventArgs e)
        {
            if (_serialComm == null || !_serialComm.IsConnected)
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
        }*/

        // 위도 슬라이더 값 변경 이벤트. 레거시 삭제됨

        private void ShowPath_Click(object sender, RoutedEventArgs e)
        {
            RocketPathWindow popup = new RocketPathWindow();
            popup.Show();
        }
    }
}