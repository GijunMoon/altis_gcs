using System;
using System.Windows.Threading;

namespace altis_gcs
{
    public class TimerManager
    {
        private readonly DispatcherTimer _timer;
        private DateTime _startTime;

        public event EventHandler<TimeSpan> ElapsedTimeUpdated;

        public TimerManager()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            _startTime = DateTime.Now;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void Reset()
        {
            _timer.Stop();
            /*시스템 리셋 이후 재발사까지 타이머 중단*/
            //ElapsedTimeUpdated?.Invoke(this, TimeSpan.Zero);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            TimeSpan elapsed = DateTime.Now - _startTime;
            ElapsedTimeUpdated?.Invoke(this, elapsed);
        }
    }
}