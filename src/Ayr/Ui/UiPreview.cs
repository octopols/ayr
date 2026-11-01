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
    /// Renders the windows off-screen with live, read-only data and saves them as PNG files, one per page of the main
    /// window, so the UI can be checked without anyone clicking anything. Nothing is written to the hardware.
    /// </summary>
    public sealed class UiPreview
    {
        private const int SettleMs = 4000;
        private const int PageSettleMs = 2500;
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
            using (var main = new MainWindow(backend))
            {
                Open(main);
                for (int page = 0; page < main.PageCount; page++)
                {
                    main.ShowPage(page);
                    Settle(PageSettleMs);
                    Save(main, "main-" + (page + 1) + ".png", output);
                }
                main.Close();
            }
            SlowState slow = backend.ReadSlow(includeAutostart: false);
            using (var curve = new FanCurveWindow(backend, slow.Desired.CustomCurves, slow.CurveTemperatures))
            {
                Open(curve);
                Save(curve, "curve.png", output);
                curve.Close();
            }
            using (var quick = new QuickPanel(backend) { CloseOnDeactivate = false })
            {
                Open(quick);
                Save(quick, "quick.png", output);
                quick.Close();
            }
            using (Process self = Process.GetCurrentProcess())
            {
                output.WriteLine("process: {0:0.0} MB private, {1:0.0} MB working set",
                    self.PrivateMemorySize64 / BytesPerMegabyte, self.WorkingSet64 / BytesPerMegabyte);
            }
        }

        private static void Open(Form window)
        {
            window.Location = OffScreen;
            window.Show();
            Settle(SettleMs);
        }

        // The windows read the hardware on worker threads; pumping messages lets those readings arrive and paint.
        private static void Settle(int milliseconds)
        {
            Stopwatch settling = Stopwatch.StartNew();
            while (settling.ElapsedMilliseconds < milliseconds)
            {
                Application.DoEvents();
                Thread.Sleep(PumpIntervalMs);
            }
        }

        private void Save(Form window, string fileName, TextWriter output)
        {
            using (var bitmap = new Bitmap(window.Width, window.Height))
            {
                window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size));
                string path = Path.Combine(_directory, fileName);
                bitmap.Save(path, ImageFormat.Png);
                output.WriteLine("saved {0} ({1}x{2})", path, window.Width, window.Height);
            }
        }
    }
}
