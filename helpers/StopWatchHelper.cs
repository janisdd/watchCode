using System.Diagnostics;

namespace watchCode.helpers
{
    public static class StopWatchHelper
    {
        public static string GetElapsedTime(Stopwatch stopwatch)
        {
            return stopwatch.Elapsed.ToString();

        }
    }
}
