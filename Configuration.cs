using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Text;

namespace SlideDiscWPF
{
    static internal class Configuration
    {
        private const string c_configDirectory = "FileMeta"; // Within the AppData folder
        private const string c_settingsFilename = "FMSlideShow_Settings.json";
        private const string c_bookmarkFilename = "FMSlideShow_Bookmark.txt";

        // Configuration constants
        private const string c_keyRootPath = "RootPath";
        private const string c_keyPaths = "Paths";
        private const string c_keyFadeTime = "FadeTime";
        private const string c_keyAdvanceTime = "AdvanceTime";
        private const string c_keyShowMetadata = "ShowMetadata";

        // Unlike CLR constants which are "True" and "False" JSON constants are all lower-case
        private const string c_true = "true";
        private const string c_false = "false";

        static Encoding s_utf8 = new UTF8Encoding(false);

        static string s_configPath = null;

        static Configuration()
        {
            if (s_configPath == null)
            {
                s_configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
                    c_configDirectory);
                if (!Directory.Exists(s_configPath))
                {
                    Directory.CreateDirectory(s_configPath);
                }
            }
        }

        public static SlideShow.Settings Load()
        {
            string path = Path.Combine(s_configPath, c_settingsFilename);

            // Create the result
            var list = new List<KeyValuePair<string, object>>();

            var settings = new SlideShow.Settings();

            try
            {
                XElement doc = null;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var jsonReader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, Encoding.UTF8, new System.Xml.XmlDictionaryReaderQuotas(), null))
                    {
                        doc = XElement.Load(jsonReader);
                    }
                }

                foreach (var ele in doc.Elements())
                {
                    switch (ele.Name.LocalName)
                    {
                        case c_keyRootPath:
                            settings.RootPath = ele.Value;
                            break;

                        case c_keyPaths:
                            foreach (var item in ele.Elements("item"))
                            {
                                settings.Paths.Add(item.Value);
                            }
                            break;

                        case c_keyAdvanceTime:
                            {
                                int intValue;
                                if (int.TryParse(ele.Value, out intValue))
                                    settings.AdvanceTime = intValue;
                            }
                            break;

                        case c_keyFadeTime:
                            {
                                int intValue;
                                if (int.TryParse(ele.Value, out intValue))
                                    settings.FadeTime = intValue;
                            }
                            break;

                        case c_keyShowMetadata:
                            {
                                int intValue;
                                if (ele.Value.Equals(c_true, StringComparison.OrdinalIgnoreCase))
                                    settings.ShowMetadata = true;
                                else if (ele.Value.Equals(c_false, StringComparison.OrdinalIgnoreCase))
                                    settings.ShowMetadata = false;
                                else if (int.TryParse(ele.Value, out intValue))
                                    settings.ShowMetadata = (intValue != 0);
                            }
                            break;
                    }
                }
            }
            catch (Exception err)
            {
                // Suppress any exception and just return blank configuration.
                // The only common form is FileNotFound.
                System.Diagnostics.Debug.Assert(err is FileNotFoundException);
            }

            return settings;
        }

        public static void Save(SlideShow.Settings settings)
        {
            // Merge existing settings
            {
                var existing = Load();
                if (string.IsNullOrEmpty(settings.RootPath))
                    settings.RootPath = existing.RootPath;
                if (settings.Paths.Count == 0)
                {
                    foreach(var p in existing.Paths)
                    {
                        settings.Paths.Add(p);
                    }
                }
                if (!settings.AdvanceTime.HasValue)
                    settings.AdvanceTime = existing.AdvanceTime;
                if (!settings.FadeTime.HasValue)
                    settings.FadeTime = existing.FadeTime;
                if (!settings.ShowMetadata.HasValue)
                    settings.ShowMetadata = existing.ShowMetadata;
            }

            string path = Path.Combine(s_configPath, c_settingsFilename);

            using (var writer = new StreamWriter(path, false, s_utf8))
            {
                writer.WriteLine("{");

                bool first = true;

                if (!string.IsNullOrEmpty(settings.RootPath))
                {
                    first = false;
                    writer.Write($"  \"{c_keyRootPath}\": \"{JsonEncode(settings.RootPath)}\"");
                }

                if (settings.Paths.Count > 0)
                {
                    if (!first) writer.WriteLine(",");
                    first = false;
                    writer.WriteLine($"  \"{c_keyPaths}\": [");
                    bool first2 = true;
                    foreach(var val in settings.Paths)
                    {
                        if (!first2) writer.WriteLine(",");
                        first2 = false;
                        writer.Write($"    \"{JsonEncode(val)}\"");
                    }
                    writer.WriteLine();
                    writer.Write("  ]");
                }

                if (settings.AdvanceTime.HasValue)
                {
                    if (!first) writer.WriteLine(",");
                    first = false;
                    writer.Write($"  \"{c_keyAdvanceTime}\": \"{JsonEncode(settings.AdvanceTime.ToString())}\"");
                }

                if (settings.FadeTime.HasValue)
                {
                    if (!first) writer.WriteLine(",");
                    first = false;
                    writer.Write($"  \"{c_keyFadeTime}\": \"{JsonEncode(settings.FadeTime.ToString())}\"");
                }

                if (settings.ShowMetadata.HasValue)
                {
                    if (!first) writer.WriteLine(",");
                    first = false;
                    writer.Write($"  \"{c_keyShowMetadata}\": \"{JsonEncode(settings.ShowMetadata.Value ? c_true : c_false)}\"");
                }

                writer.WriteLine();
                writer.WriteLine("}");
            }
        }

        static string JsonEncode(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public static string LoadBookmark()
        {
            string path = Path.Combine(s_configPath, c_bookmarkFilename);

            try
            {
                using (var reader = new StreamReader(path, Encoding.UTF8, true))
                {
                    return reader.ReadToEnd().Trim();
                }
            }
            catch (Exception err)
            {
                // Supporess any exception and just return blank configuration.
                // The only common form is FileNotFound.
                System.Diagnostics.Debug.Assert(err is FileNotFoundException);
            }

            return null;
        }

        public static void SaveBookmark(string bookmark)
        {
            string path = Path.Combine(s_configPath, c_bookmarkFilename);

            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.Write(bookmark);
            }
        }
    }

}
