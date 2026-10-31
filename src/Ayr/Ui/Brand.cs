using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Ayr.Ui
{
    /// <summary>
    /// Ayr's mark, pre-rendered by scripts/make-assets.ps1: the app icon, a white tray icon, and the white mark at the exact
    /// badge sizes so its strokes stay crisp. Callers own and dispose what they load.
    /// </summary>
    public static class Brand
    {
        private const string IconResource = "Ayr.Ui.app.ico";
        private const string TrayIconResource = "Ayr.Ui.tray.ico";
        private static readonly int[] MarkSizes = { 24, 32, 40, 48 };

        public static Icon LoadIcon(Size size) => LoadIcon(IconResource, size);

        public static Icon LoadTrayIcon() => LoadIcon(TrayIconResource, SystemInformation.SmallIconSize);

        /// <summary>The smallest mark at least <paramref name="pixels"/> tall, or the largest there is.</summary>
        public static Image LoadMark(int pixels)
        {
            int size = MarkSizes.Where(candidate => candidate >= pixels).DefaultIfEmpty(MarkSizes.Max()).First();
            using (Stream stream = Resource("Ayr.Ui.mark-" + size + ".png"))
            using (Image image = Image.FromStream(stream))
            {
                // GDI+ needs the stream for the image's whole lifetime, so the pixels are copied out.
                return new Bitmap(image);
            }
        }

        private static Icon LoadIcon(string resource, Size size)
        {
            using (Stream stream = Resource(resource))
            {
                return new Icon(stream, size);
            }
        }

        private static Stream Resource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
    }
}
