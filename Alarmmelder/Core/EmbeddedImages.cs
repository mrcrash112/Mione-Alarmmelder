using System.Drawing;
using System.IO;
using System.Reflection;

namespace MioneAlarmmelder.Core
{
    public static class EmbeddedImages
    {
        public static Image LoadLogo()
        {
            return Load("MioneAlarmmelder.Assets.logo.png");
        }

        public static Image LoadBanner()
        {
            return Load("MioneAlarmmelder.Assets.Banner.png");
        }

        private static Image Load(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new InvalidDataException("Eingebettete Bildressource fehlt: " + resourceName);
                using (Image source = Image.FromStream(stream)) return new Bitmap(source);
            }
        }
    }
}
