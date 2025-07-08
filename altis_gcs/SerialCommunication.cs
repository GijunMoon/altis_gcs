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
            if (values.Length != 16) // 이 값을 하드코딩하거나 parameterSettings에서 가져와야 함
            {
                DataReceived?.Invoke(this, $"Invalid CSV data format (expected 16 fields): {lineStr}");
                return;
            }

            var telemetryData = new TelemetryData();

            telemetryData.Time = long.Parse(values[0]);
            telemetryData.Altitude = double.Parse(values[1]);
            telemetryData.Velocity = double.Parse(values[2]);
            telemetryData.AccelX = double.Parse(values[3]);
            telemetryData.AccelY = double.Parse(values[4]);
            telemetryData.AccelZ = double.Parse(values[5]);
            telemetryData.GyroX = double.Parse(values[6]);
            telemetryData.GyroY = double.Parse(values[7]);
            telemetryData.GyroZ = double.Parse(values[8]);
            telemetryData.QuaternionX = double.Parse(values[9]);
            telemetryData.QuaternionY = double.Parse(values[10]);
            telemetryData.QuaternionZ = double.Parse(values[11]);
            telemetryData.QuaternionW = double.Parse(values[12]);

            // 7. ftv_ej (3개) - 필요에 따라 TelemetryData에 추가 속성 정의
            // 현재는 파싱만 하고 특별히 저장하지 않음.
            //currentIndex += 3; // 3개 스킵하거나 저장

            // 선택적으로 Dictionary에도 저장 (기존 ParameterSettings 방식 유지를 원한다면)
            // 이 부분은 데이터를 어떻게 활용할지에 따라 다릅니다.
            // 직접적인 속성으로 저장하는 것이 더 효율적일 수 있습니다.
            for (int i = 0; i < values.Length; i++)
            {
                if (double.TryParse(values[i], out double value))
                {
                    if (i < parameterSettings.ParameterOrder.Count)
                    {
                        string paramName = parameterSettings.ParameterOrder[i].Trim();
                        telemetryData.Parameters[paramName] = value;
                    }
                }
            }


            TelemetryDataParsed?.Invoke(this, telemetryData);
            DataReceived?.Invoke(this, $"[DEBUG] Parsed: {string.Join(",", telemetryData.Parameters.Select(kv => $"{kv.Key}={kv.Value}"))}");

        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TelemetryPacket
    {
        public long Time;
        public double Altitude;
        public double Velocity;
        public double AccelX;
        public double AccelY;
        public double AccelZ;
        public double GyroX;
        public double GyroY;
        public double GyroZ;
        public double QuaternionX;
        public double QuaternionY;
        public double QuaternionZ;
        public double QuaternionW;
        public double ftv_ej1;
        public double ftv_ej2;
        public double ftv_ej3;
    }

}
