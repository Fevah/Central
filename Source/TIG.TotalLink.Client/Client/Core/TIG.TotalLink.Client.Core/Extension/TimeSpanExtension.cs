using System;

namespace TIG.TotalLink.Client.Core.Extension
{
    public static class TimeSpanExtension
    {
        public static string Format(this TimeSpan timeSpan)
        {
            var timeValue = Math.Round(timeSpan.TotalSeconds);
            var timeMeasure = "second";

            if (timeSpan.TotalDays > 1)
            {
                timeValue = Math.Round(timeSpan.TotalDays, 2);
                timeMeasure = "day";
            }
            else if (timeSpan.TotalHours > 1)
            {
                timeValue = Math.Round(timeSpan.TotalHours, 2);
                timeMeasure = "hour";
            }
            else if (timeSpan.TotalMinutes > 1)
            {
                timeValue = Math.Round(timeSpan.TotalMinutes, 2);
                timeMeasure = "minute";
            }

            return string.Format("{0} {1}{2}", timeValue, timeMeasure, (timeValue < 1 || timeValue > 1 ? "s" : string.Empty));
        }
    }
}
