using System;
using System.IO.Ports;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.Runtime.InteropServices;
using altis_gcs; // ParameterSettings, TelemetryData 등 별도 파일 참조

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
                        bytesRead = await Task.Run(() => serialPort.Read(buffer, 0, buffer.Length), cancellationToken);
                    }
                    catch (TimeoutException)
                    {
                        // Timeout은 빈번히 발생할 수 있으므로 무시하고 루프를 계속 진행
                        continue;
                    }

                    if (bytesRead > 0)
                    {
                        if (parameterSettings.CommType == CommunicationType.Binary)
                        {
                            ProcessBinaryPacket(buffer.AsSpan(0, 56));
                        } else
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

            if (parameterSettings.ParameterCount == 0 ||
                values.Length != parameterSettings.ParameterCount)
            {
                DataReceived?.Invoke(this, $"Invalid data format: {lineStr}");
                return;
            }

            var telemetryData = new TelemetryData();
            for (int i = 0; i < parameterSettings.ParameterCount; i++)
            {
                if (double.TryParse(values[i], out double value))
                {
                    string paramName = parameterSettings.ParameterOrder[i];
                    telemetryData.Parameters[paramName] = value;
                }
            }

            TelemetryDataParsed?.Invoke(this, telemetryData);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TelemetryPacket
    {
        public long Timestamp;
        public double AccelX;
        public double AccelY;
        public double AccelZ;
        public double GyroX;
        public double GyroY;
        public double GyroZ;
    }
}
