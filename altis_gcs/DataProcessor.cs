using System;
using System.Collections.Generic;
using System.Globalization; // 이 네임스페이스가 필요합니다.
using System.IO;
using System.Threading.Tasks;
using OxyPlot;

namespace altis_gcs
{
    /// <summary>
    /// CSV 데이터를 비동기적으로 로드하고 처리하는 클래스
    /// </summary>
    public class DataProcessor
    {
        public async Task<(List<DataPoint> accelX, List<DataPoint> accelY, List<DataPoint> accelZ,
                           List<DataPoint> gyroX, List<DataPoint> gyroY, List<DataPoint> gyroZ,
                           List<DataPoint> quatX, List<DataPoint> quatY, List<DataPoint> quatZ, List<DataPoint> quatW,
                           List<TelemetryData> telemetryDatas)>
                             LoadCsvDataAsync(string filePath, int chunkSize = 1000)
        {
            var dataPointsAccelX = new List<DataPoint>();
            var dataPointsAccelY = new List<DataPoint>();
            var dataPointsAccelZ = new List<DataPoint>();
            var dataPointsGyroX = new List<DataPoint>();
            var dataPointsGyroY = new List<DataPoint>();
            var dataPointsGyroZ = new List<DataPoint>();
            var dataPointsQuatX = new List<DataPoint>();
            var dataPointsQuatY = new List<DataPoint>();
            var dataPointsQuatZ = new List<DataPoint>();
            var dataPointsQuatW = new List<DataPoint>(); // 실수부

            var telemetryDataList = new List<TelemetryData>(); // TelemetryData 객체를 저장할 리스트

            using (var reader = new StreamReader(filePath))
            {
                string line;
                bool isFirstLine = true;
                int lineCount = 0;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (isFirstLine)
                    {
                        isFirstLine = false;
                        continue; // 헤더 라인 스킵
                    }

                    try
                    {
                        var values = line.Split(',');

                        // 예상되는 필드 개수 확인
                        if (values.Length < 16) // 최소 16개 필드 (ftv_ej까지 포함)
                        {
                            Console.WriteLine($"Skipping malformed line {lineCount}: Not enough fields. Line: {line}");
                            continue;
                        }

                        // TelemetryData 객체 생성 및 파싱
                        var telemetryData = new TelemetryData();
                        int currentIndex = 0;

                        // 1. time
                        // NumberStyles.Any 추가
                        if (long.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out long time))
                        {
                            telemetryData.Time = time;
                        }

                        // 2. alt
                        // NumberStyles.Any 추가
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double alt))
                        {
                            telemetryData.Altitude = alt;
                        }

                        // 3. vel
                        // NumberStyles.Any 추가
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double vel))
                        {
                            telemetryData.Velocity = vel;
                        }

                        // 4. accel (3개)
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double accelX)) { telemetryData.AccelX = accelX; }
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double accelY)) { telemetryData.AccelY = accelY; }
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double accelZ)) { telemetryData.AccelZ = accelZ; }

                        // 5. gyro (3개)
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double gyroX)) { telemetryData.GyroX = gyroX; }
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double gyroY)) { telemetryData.GyroY = gyroY; }
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double gyroZ)) { telemetryData.GyroZ = gyroZ; }

                        // 6. quat (4개: x, y, z, w 순서로 데이터에 들어옴)
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double quatX)) { telemetryData.QuaternionX = quatX; }
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double quatY)) { telemetryData.QuaternionY = quatY; }
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double quatZ)) { telemetryData.QuaternionZ = quatZ; }
                        if (double.TryParse(values[currentIndex++], NumberStyles.Any, CultureInfo.InvariantCulture, out double quatW)) { telemetryData.QuaternionW = quatW; } // 실수부

                        // 7. ftv_ej (3개) - 필요 시 TelemetryData에 추가 속성 정의 후 파싱
                        currentIndex += 3; // 현재는 스킵

                        // OxyPlot용 DataPoint 추가 (Time을 X축으로 사용)
                        dataPointsAccelX.Add(new DataPoint(telemetryData.Time, telemetryData.AccelX));
                        dataPointsAccelY.Add(new DataPoint(telemetryData.Time, telemetryData.AccelY));
                        dataPointsAccelZ.Add(new DataPoint(telemetryData.Time, telemetryData.AccelZ));
                        dataPointsGyroX.Add(new DataPoint(telemetryData.Time, telemetryData.GyroX));
                        dataPointsGyroY.Add(new DataPoint(telemetryData.Time, telemetryData.GyroY));
                        dataPointsGyroZ.Add(new DataPoint(telemetryData.Time, telemetryData.GyroZ));
                        dataPointsQuatX.Add(new DataPoint(telemetryData.Time, telemetryData.QuaternionX));
                        dataPointsQuatY.Add(new DataPoint(telemetryData.Time, telemetryData.QuaternionY));
                        dataPointsQuatZ.Add(new DataPoint(telemetryData.Time, telemetryData.QuaternionZ));
                        dataPointsQuatW.Add(new DataPoint(telemetryData.Time, telemetryData.QuaternionW));

                        telemetryDataList.Add(telemetryData); // TelemetryData 객체 리스트에 추가

                        lineCount++;

                        // 청크 단위로 처리하여 UI 응답성 유지
                        if (lineCount % chunkSize == 0)
                        {
                            await Task.Yield(); // UI 스레드에 제어권을 넘김
                        }
                    }
                    catch (FormatException fe)
                    {
                        Console.WriteLine($"Format error parsing line {lineCount}: {fe.Message}. Line: {line}");
                    }
                    catch (IndexOutOfRangeException ie)
                    {
                        Console.WriteLine($"Index out of range error parsing line {lineCount}: {ie.Message}. Line: {line}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An unexpected error occurred parsing line {lineCount}: {ex.Message}. Line: {line}");
                    }
                }
            }

            return (dataPointsAccelX, dataPointsAccelY, dataPointsAccelZ,
                    dataPointsGyroX, dataPointsGyroY, dataPointsGyroZ,
                    dataPointsQuatX, dataPointsQuatY, dataPointsQuatZ, dataPointsQuatW,
                    telemetryDataList); // TelemetryData 객체 리스트도 반환
        }
    }
}