using System;
using System.IO.Ports;
using System.Windows;

namespace altis_gcs
{
    public class SerialCommunication
    {
        private SerialPort _serialPort;
        private readonly string _portName;
        private readonly int _baudRate;

        public event EventHandler<string> DataReceived;
        public bool isConnected = false;

        public SerialCommunication(string portName, int baudRate)
        {
            _portName = portName;
            _baudRate = baudRate;
        }

        public void Connect()
        {
            _serialPort = new SerialPort(_portName, _baudRate);
            _serialPort.DataReceived += OnDataReceived;

            try //빈 포트 연결 방지
            {
                _serialPort.Open();
                isConnected = true;
                MessageBox.Show("Connected to " + _portName);
            }
            catch(Exception ex)
            {
                isConnected = false;
                MessageBox.Show("포트 에러 발생: " + ex);
            }
        }

        public void Disconnect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                isConnected = false;
            }
        }

        public void Send(string message)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.WriteLine(message);
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string incomingData = _serialPort.ReadLine();
                DataReceived?.Invoke(this, incomingData);
            }
            catch (Exception ex)
            {
                // UI 스레드에서 오류 처리 필요 시 별도 이벤트로 전달 가능
            }
        }
    }
}