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

namespace altis_gcs
{
    public partial class MainWindow : Window
    {
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
        public PlotModel CombinedAccelerationPlotModel { get; private set; } = new PlotModel { Title = "Combined Acceleration" };
        public PlotModel GyroPlotModel { get; private set; } = new PlotModel { Title = "Gyro Data" };

        public MainWindow()
        {
            InitializeComponent();
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
            RollData.Text = $"{e.NewValue}°";
            RollTransform.Rotation = new AxisAngleRotation3D(new Vector3D(1, 0, 0), e.NewValue);
        }

        private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PitchData.Text = $"{e.NewValue}°";
            PitchTransform.Rotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), e.NewValue);
        }

        private void YawSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            YawData.Text = $"{e.NewValue}°";
            YawTransform.Rotation = new AxisAngleRotation3D(new Vector3D(0, 0, 1), e.NewValue);
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
            RollSlider.Value = 0;
            PitchSlider.Value = 0;
            YawSlider.Value = 0;
            LatSlider.Value = 0;
            LongSlider.Value = 0;
        }
    }
}
