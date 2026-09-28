using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace WindowsFish
{
    internal static class SettingsFiles
    {
        internal static XDocument Read(string path)
        {
            var options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = 1048576 };
            using (var reader = XmlReader.Create(path, options)) return XDocument.Load(reader);
        }
    }

    internal static class MonitorSelection
    {
        internal static Screen Resolve(Screen[] screens, string name)
        {
            if (screens.Length == 0) throw new InvalidOperationException("没有可用显示器。");
            return screens.FirstOrDefault(s => !string.IsNullOrEmpty(name) && s.DeviceName == name)
                ?? screens.FirstOrDefault(s => s.Primary) ?? screens[0];
        }
    }
}
