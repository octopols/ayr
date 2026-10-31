using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Ayr.Controllers;

namespace Ayr.Ui
{
    /// <summary>
    /// Renders the windows off-screen with live, read-only data and saves them as PNG files, so the UI can be checked
    /// without anyone clicking anything. Nothing is written to the hardware.
    /// </summary>
    public sealed class UiPreview
    {
        private const int SettleMs = 4000;
        private const int PumpIntervalMs = 30;
        private const double BytesPerMegabyte = 1048576.0;
        private static readonly Point OffScreen = new Point(-20000, -20000);

        private readonly string _directory;

        public UiPreview(string directory)
        {
            _directory = directory;
        }

        public void Run(TextWriter output)
        {
            Directory.CreateDirectory(_directory);
            var backend = new UiBackend(new HardwareContext());
            Capture(new MainWindow(backend), "main.png", output);
            Capture(new QuickPanel(backend) { CloseOnDeactivate = false }, "quick.png", output);
            using (Process self = Process.GetCurrentProcess())
            {
                output.WriteLine("process: {0:0.0} MB private, {1:0.0} MB working set",
                    self.PrivateMemorySize64 / BytesPerMegabyte, self.WorkingSet64 / BytesPerMegabyte);
            }
        }

        private void Capture(Form window, string fileName, TextWriter output)
        {
            using (window)
            {
                window.Location = OffScreen;
                window.Show();
                Stopwatch settling = Stopwatch.StartNew();
                while (settling.ElapsedMilliseconds < SettleMs)
                {
                    Application.DoEvents();
                    Thread.Sleep(PumpIntervalMs);
                }
                using (var bitmap = new Bitmap(window.Width, window.Height))
                {
                    window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size));
                    string path = Path.Combine(_directory, fileName);
                    bitmap.Save(path, ImageFormat.Png);
                    output.WriteLine("saved {0} ({1}x{2})", path, window.Width, window.Height);
                }
                window.Close();
            }
        }
    }
}
