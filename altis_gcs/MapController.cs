using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows.Shapes;

namespace altis_gcs
{
    public class MapController
    {
        private readonly GMapControl _mapControl;

        public MapController(GMapControl mapControl)
        {
            _mapControl = mapControl;
            InitializeMap();
            _mapControl.MouseDoubleClick += MapControl_MouseDoubleClick;
        }

        private void InitializeMap()
        {
            GoogleMapProvider.Instance.ApiKey = "AIzaSyCXJrDpszuNQfMEXKIifx5zYzhSq3Irpyg";
            _mapControl.MapProvider = GMapProviders.OpenStreetMap;
            _mapControl.Position = new PointLatLng(35.1543, 128.0931);
            _mapControl.MinZoom = 2;
            _mapControl.MaxZoom = 17;
            _mapControl.Zoom = 13;
            _mapControl.ShowCenter = false;
            _mapControl.DragButton = MouseButton.Left;
        }

        private void MapControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Point clickPoint = e.GetPosition(_mapControl);
            PointLatLng point = _mapControl.FromLocalToLatLng((int)clickPoint.X, (int)clickPoint.Y);

            var marker = new GMapMarker(point)
            {
                Shape = new Ellipse
                {
                    Width = 15,
                    Height = 15,
                    Fill = Brushes.Red,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2,
                    ToolTip = new ToolTip { Content = $"Lat: {point.Lat:F4}, Lng: {point.Lng:F4}" }
                }
            };

            marker.Shape.MouseEnter += (s, ev) => ((Ellipse)s).Fill = Brushes.Orange;
            marker.Shape.MouseLeave += (s, ev) => ((Ellipse)s).Fill = Brushes.Red;
            marker.Shape.MouseLeftButtonDown += (s, ev) =>
            {
                MessageBox.Show($"Marker clicked at: {point.Lat:F4}, {point.Lng:F4}");
                ev.Handled = true;
            };

            _mapControl.Markers.Add(marker);
        }
    }
}