using System;
using System.IO.Ports;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Buffers;

namespace altis_gcs
{
    public class SerialCommunication : IDisposable
    {
        private SerialPort _serialPort;
        private readonly string _portName;
        private readonly int _baudRate;
        private readonly int _dataBits;
        private readonly Parity _parity;
        private readonly StopBits _stopBits;

        public event EventHandler<string> DataReceived;
        public event EventHandler<TelemetryData> TelemetryDataParsed; // 파싱된 데이터 이벤트
        public bool IsConnected { get; private set; } = false;

        private readonly Pipe _pipe;
        private bool _isRunning;
        private CancellationTokenSource _cts;
        private readonly List<TelemetryData> _telemetryDataList = new List<TelemetryData>(); // 데이터 저장
        private ParameterSettings _parameterSettings; // 파라미터 설정

        public SerialCommunication(string portName, int baudRate, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
        {
            _portName = portName;
            _baudRate = baudRate;
            _dataBits = dataBits;
            _parity = parity;
            _stopBits = stopBits;

            _serialPort = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
            {
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            _pipe = new Pipe();
            _isRunning = false;
            _cts = new CancellationTokenSource();
            _parameterSettings = new ParameterSettings(); // 기본 설정 초기화
        }

        public void SetParameterSettings(ParameterSettings settings)
        {
            _parameterSettings = settings;
        }

        public List<TelemetryData> GetTelemetryData()
        {
            return _telemetryDataList;
        }

        public void Connect()
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    _serialPort.Open();
                    IsConnected = true;
                    _isRunning = true;
                    Task.Run(() => ReadSerialPortAsync(_cts.Token));
                }
                DataReceived?.Invoke(this, $"Connected to {_portName}");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                DataReceived?.Invoke(this, $"포트 에러 발생: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _isRunning = false;
                _cts.Cancel();
                _serialPort.Close();
                IsConnected = false;
                DataReceived?.Invoke(this, "Disconnected");
            }
        }

        public void Send(string message)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.WriteLine(message);
            }
        }

        private async Task ReadSerialPortAsync(CancellationToken cancellationToken)
        {
            PipeWriter writer = _pipe.Writer;
            byte[] buffer = new byte[1024];

            try
            {
                while (_isRunning && !cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = await Task.Run(() => _serialPort.Read(buffer, 0, buffer.Length), cancellationToken);
                    if (bytesRead > 0)
                    {
                        await writer.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                DataReceived?.Invoke(this, $"Serial read error: {ex.Message}");
            }
            finally
            {
                await writer.CompleteAsync();
            }
        }

        public async Task ProcessLinesAsync(CancellationToken cancellationToken)
        {
            PipeReader reader = _pipe.Reader;

            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;
                SequencePosition? position;

                while ((position = buffer.PositionOf((byte)'\n')) != null)
                {
                    var line = buffer.Slice(0, position.Value);
                    ProcessLine(line);
                    buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                    break;
            }

            await reader.CompleteAsync();
        }

        private void ProcessLine(ReadOnlySequence<byte> line)
        {
            string lineStr = System.Text.Encoding.UTF8.GetString(line.ToArray());
            DataReceived?.Invoke(this, lineStr);

            // CSV 파싱
            string[] values = lineStr.Split(',');
            if (_parameterSettings.ParameterCount == 0 || values.Length < _parameterSettings.ParameterCount)
            {
                DataReceived?.Invoke(this, $"Invalid data format: {lineStr}");
                return;
            }

            // 설정된 파라미터 순서에 따라 데이터 매핑
            var telemetryData = new TelemetryData();
            for (int i = 0; i < _parameterSettings.ParameterCount; i++)
            {
                if (double.TryParse(values[i], out double value))
                {
                    string paramName = _parameterSettings.ParameterOrder[i];
                    telemetryData.Parameters[paramName] = value;
                }
            }

            // 데이터 저장
            _telemetryDataList.Add(telemetryData);
            TelemetryDataParsed?.Invoke(this, telemetryData);
        }

        public void Dispose()
        {
            Disconnect();
            _serialPort?.Dispose();
            _cts?.Dispose();
        }
    }
}