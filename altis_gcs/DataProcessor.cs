using System;
using System.Collections.Generic;
using System.Globalization;
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
                          List<DataPoint> gyroX, List<DataPoint> gyroY, List<DataPoint> gyroZ)>
                          LoadCsvDataAsync(string filePath, int chunkSize = 1000)
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
                        var time = double.Parse(values[0], CultureInfo.InvariantCulture);
                        var accelX = double.Parse(values[1], CultureInfo.InvariantCulture);
                        var accelY = double.Parse(values[2], CultureInfo.InvariantCulture);
                        var accelZ = double.Parse(values[3], CultureInfo.InvariantCulture);
                        var gyroX = double.Parse(values[4], CultureInfo.InvariantCulture);
                        var gyroY = double.Parse(values[5], CultureInfo.InvariantCulture);
                        var gyroZ = double.Parse(values[6], CultureInfo.InvariantCulture);

                        dataPointsAccelX.Add(new DataPoint(time, accelX));
                        dataPointsAccelY.Add(new DataPoint(time, accelY));
                        dataPointsAccelZ.Add(new DataPoint(time, accelZ));
                        dataPointsGyroX.Add(new DataPoint(time, gyroX));
                        dataPointsGyroY.Add(new DataPoint(time, gyroY));
                        dataPointsGyroZ.Add(new DataPoint(time, gyroZ));

                        lineCount++;

                        // 청크 단위로 처리하여 UI 응답성 유지
                        if (lineCount % chunkSize == 0)
                        {
                            await Task.Yield(); // UI 스레드에 제어권을 넘김
                            // 필요 시 여기서 데이터를 즉시 처리하거나 메모리 정리 가능
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing line {lineCount}: {ex.Message}");
                    }
                }
            }

            return (dataPointsAccelX, dataPointsAccelY, dataPointsAccelZ,
                    dataPointsGyroX, dataPointsGyroY, dataPointsGyroZ);
        }
    }
}