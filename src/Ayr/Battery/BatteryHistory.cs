using System;
using System.IO;
using System.Linq;
using System.Xml;
using Ayr.App;

namespace Ayr.Battery
{
    /// <summary>
    /// What Windows remembers about the battery, from its own battery report: when the battery last ran the laptop, and
    /// since when Windows has kept records. Batteries like MSI's report neither their age nor their charge cycles.
    /// </summary>
    public sealed class BatteryHistory
    {
        private const string ReportNamespace = "http://schemas.microsoft.com/battery/2012";
        private static readonly TimeSpan ReportTimeout = TimeSpan.FromMinutes(1);

        /// <summary>A battery gauge relearns its capacity only while the battery runs the laptop.</summary>
        private static readonly TimeSpan CapacityGoesStaleAfter = TimeSpan.FromDays(60);

        private BatteryHistory(DateTime? lastRanLaptop, DateTime recordsSince)
        {
            LastRanLaptop = lastRanLaptop;
            RecordsSince = recordsSince;
        }

        /// <summary>
        /// The start of the last period (usually a week) in which the battery ran the laptop, since Windows only totals time
        /// on battery per period; null when it hasn't at any time Windows has records for.
        /// </summary>
        public DateTime? LastRanLaptop { get; }

        public DateTime RecordsSince { get; }

        public bool CapacityIsStale => !LastRanLaptop.HasValue || DateTime.Now - LastRanLaptop.Value > CapacityGoesStaleAfter;

        /// <summary>Null when Windows has no records of the battery yet.</summary>
        public static BatteryHistory Read()
        {
            XmlDocument report = WindowsBatteryReport();
            var names = new XmlNamespaceManager(report.NameTable);
            names.AddNamespace("b", ReportNamespace);
            XmlElement[] periods = report.SelectNodes("//b:History/b:HistoryEntry", names).Cast<XmlElement>().ToArray();
            if (periods.Length == 0)
            {
                return null;
            }
            XmlElement lastOnBattery = periods.LastOrDefault(period => TimeOnBattery(period) > TimeSpan.Zero);
            DateTime? lastRanLaptop = lastOnBattery == null ? (DateTime?)null : StartOf(lastOnBattery);
            return new BatteryHistory(lastRanLaptop, StartOf(periods[0]));
        }

        private static XmlDocument WindowsBatteryReport()
        {
            string reportFile = Path.Combine(Path.GetTempPath(), "ayr-battery-report.xml");
            try
            {
                new ExternalCommand("powercfg.exe", "/batteryreport /xml /output \"" + reportFile + "\"").RunOrThrow(ReportTimeout);
                var report = new XmlDocument();
                report.Load(reportFile);
                return report;
            }
            finally
            {
                File.Delete(reportFile);
            }
        }

        private static TimeSpan TimeOnBattery(XmlElement period)
        {
            string duration = period.GetAttribute("ActiveDcTime");
            return string.IsNullOrEmpty(duration) ? TimeSpan.Zero : XmlConvert.ToTimeSpan(duration);
        }

        private static DateTime StartOf(XmlElement period) =>
            XmlConvert.ToDateTime(period.GetAttribute("LocalStartDate"), XmlDateTimeSerializationMode.Unspecified);
    }
}
