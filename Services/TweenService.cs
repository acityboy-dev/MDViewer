using System.Windows.Threading;

namespace MDViewer.Services;

public sealed class TweenService
{
    public void Animate(double from, double to, TimeSpan duration, Action<double> update, Action? completed = null)
    {
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };

        timer.Tick += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - started;
            var progress = Math.Clamp(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            update(from + ((to - from) * eased));

            if (progress >= 1)
            {
                timer.Stop();
                update(to);
                completed?.Invoke();
            }
        };

        timer.Start();
    }
}
