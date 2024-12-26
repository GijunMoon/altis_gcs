using LiveCharts;

namespace altis_gcs
{
    public static class DataModel
    {
        // Example property to be bound to the chart
        public static ChartValues<double> AccelerometerX { get; } =
            new ChartValues<double> { 1, 2, 3, 4, 5 };
    }
}
