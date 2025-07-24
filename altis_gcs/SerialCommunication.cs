using System;
using System.IO.Ports;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.Runtime.InteropServices;
using altis_gcs;
using System.IO;
using System.Linq; // ParameterSettings, TelemetryData 등 별도 파일 참조

namespace altis_gcs
{
    public class SerialCommunication : IDisposable
    {
        private SerialPort serialPort;
        private readonly Pipe pipe = new Pipe();
        private bool isRunning;
        private CancellationTokenSource cts;
        private ParameterSettings parameterSettings;

        public event EventHandler<string> DataReceived;
        public event EventHandler<TelemetryData> TelemetryDataParsed;

        public bool IsConnected { get; private set; } = false;

        public SerialCommunication(string portName, int baudRate, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
        {
            serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            cts = new CancellationTokenSource();
            parameterSettings = new ParameterSettings();
        }

        public void SetParameterSettings(ParameterSettings settings)
        {
            parameterSettings = settings;
        }

        public void Connect()
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    serialPort.Open();
                    IsConnected = true;
                    isRunning = true;
                    Task.Run(() => ReadSerialPortAsync(cts.Token));
                    DataReceived?.Invoke(this, $"Connected to {serialPort.PortName}");
                }
            }
            catch (TimeoutException ex)
            {
                IsConnected = false;
                DataReceived?.Invoke(this, $"Timeout: {ex.Message}");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                DataReceived?.Invoke(this, $"Connection error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Disconnect();
            serialPort?.Dispose();
            cts?.Dispose();
        }

        public void Send(string message)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.WriteLine(message);
                }
            }
            catch (TimeoutException ex)
            {
                DataReceived?.Invoke(this, $"Send timeout: {ex.Message}");
            }
            catch (Exception ex)
            {
                DataReceived?.Invoke(this, $"Send error: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                isRunning = false;
                cts.Cancel();
                serialPort.Close();
                IsConnected = false;
                DataReceived?.Invoke(this, "Disconnected");
            }
        }

        // Binary packet processing
        private async Task ReadSerialPortAsync(CancellationToken cancellationToken)
        {
            var writer = pipe.Writer;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

            try
            {
                while (isRunning && !cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = 0;
                    try
                    {
                        bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    }
                    catch (IOException ex)
                    {
                        DataReceived?.Invoke(this, $"비정상 IO 종료: {ex.Message}");
                        continue; // ignore
                    }
                    catch (OperationCanceledException)
                    {
                        break; // 취소 요청 시 루프 종료
                    }

                    if (bytesRead > 0)
                    {
                        if (parameterSettings.CommType == CommunicationType.Binary)
                        {
                            ProcessBinaryPacket(buffer.AsSpan(0, 56));
                        }
                        else
                        {
                            await writer.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DataReceived?.Invoke(this, $"Serial read error: {ex.Message}");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                await writer.CompleteAsync();
            }
        }

        private unsafe void ProcessBinaryPacket(Span<byte> data)
        {
            if (parameterSettings.ParameterCount == 0) return;
            if (data.Length < sizeof(TelemetryPacket)) return;

            fixed (byte* ptr = data)
            {
                var packet = *(TelemetryPacket*)ptr;
                var telemetryData = new TelemetryData();

                for (int i = 0; i < parameterSettings.ParameterCount; i++)
                {
                    string paramName = parameterSettings.ParameterOrder[i];
                    double value = GetSensorValue(packet, paramName);
                    telemetryData.Parameters[paramName] = value;
                }

                telemetryData.Timestamp = DateTime.Now;
                TelemetryDataParsed?.Invoke(this, telemetryData);
            }
        }

        private double GetSensorValue(TelemetryPacket packet, string paramName)
        {
            return paramName switch
            {
                "AccelX" => packet.AccelX,
                "AccelY" => packet.AccelY,
                "AccelZ" => packet.AccelZ,
                "GyroX" => packet.GyroX,
                "GyroY" => packet.GyroY,
                "GyroZ" => packet.GyroZ,
                _ => throw new ArgumentException($"Invalid parameter: {paramName}")
            };
        }

        // CSV packet processing
        public async Task ProcessLinesAsync(CancellationToken cancellationToken)
        {
            PipeReader reader = pipe.Reader;
            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    ProcessLine(line);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) break;
            }
            await reader.CompleteAsync();
        }

        private bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            SequencePosition? position = buffer.PositionOf((byte)'\n');
            if (!position.HasValue)
            {
                line = default;
                return false;
            }

            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        private void ProcessLine(ReadOnlySequence<byte> line)
        {
            string lineStr = System.Text.Encoding.UTF8.GetString(line.ToArray());
            DataReceived?.Invoke(this, lineStr);

            string[] values = lineStr.Split(',');

            // 필드 개수 확인 (Parsing order에 따라 16개 예상)
            // parameterSettings.ParameterCount가 16으로 설정되어야 합니다.
            try
            {
                if (values.Length < 16)
                {
                    DataReceived?.Invoke(this, $"Invalid field count: {values.Length} → {lineStr}");
                    return;
                }

                var telemetryData = new TelemetryData
                {
                    Time = long.Parse(values[0]),
                    Altitude = float.Parse(values[1]),
                    Velocity = float.Parse(values[2]),
                    AccelX = float.Parse(values[3]),
                    AccelY = float.Parse(values[4]),
                    AccelZ = float.Parse(values[5]),
                    GyroX = float.Parse(values[6]),
                    GyroY = float.Parse(values[7]),
                    GyroZ = float.Parse(values[8]),
                    QuaternionX = float.Parse(values[9]),
                    QuaternionY = float.Parse(values[10]),
                    QuaternionZ = float.Parse(values[11]),
                    QuaternionW = float.Parse(values[12]),
                    ftv_ej1 = values[13] == "1",
                    ftv_ej2 = values[14] == "1",
                    ftv_ej3 = values[15] == "1"
                };

                // Parameters 딕셔너리에도 넣기
                for (int i = 0; i < Math.Min(values.Length, parameterSettings.ParameterOrder.Count); i++)
                {
                    if (double.TryParse(values[i], out var value))
                        telemetryData.Parameters[parameterSettings.ParameterOrder[i]] = value;
                }

                DataReceived?.Invoke(this, $"[Parsed OK]: {string.Join(",", telemetryData.Parameters.Select(kv => $"{kv.Key}={kv.Value}"))}");

                TelemetryDataParsed?.Invoke(this, telemetryData);
            }
            catch (Exception ex)
            {
                DataReceived?.Invoke(this, $"[Telemetry Parse Fail] {ex.Message}");
            }

        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TelemetryPacket
    {
        public long Time;
        public float Altitude;
        public float Velocity;
        public float AccelX;
        public float AccelY;
        public float AccelZ;
        public float GyroX;
        public float GyroY;
        public float GyroZ;
        public float QuaternionX;
        public float QuaternionY;
        public float QuaternionZ;
        public float QuaternionW;
        public bool ftv_ej1; //강제사출
        public bool ftv_ej2; //타이머
        public bool ftv_ej3; //고도
    }
    /*특수 문자열 (사출) : EJECT_parachute*/

}
