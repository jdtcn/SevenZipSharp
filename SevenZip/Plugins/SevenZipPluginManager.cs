using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SevenZip.Plugins
{
    // TODO: OutArchive support
    internal static class SevenZipPluginManager
    {
        private const uint kArchiveFormatStartId = 1000;
        private const string kPluginsDirectory = "Formats";

        private static readonly object SyncRoot = new object();
        private static List<SevenZipPlugin> _plugins;
        private static Dictionary<InArchiveFormat, SevenZipPluginFormat> _formats;

        private static void LoadPlugins()
        {
            if (_plugins != null)
                return;

            lock (SyncRoot)
            {
                if (_plugins != null)
                    return;

                _plugins = new List<SevenZipPlugin>();
                _formats = new Dictionary<InArchiveFormat, SevenZipPluginFormat>();

                var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
                var formatsLocation = Path.Combine(currentDirectory, kPluginsDirectory);
                if (!Directory.Exists(formatsLocation))
                    return;

                var inArchiveFormat = kArchiveFormatStartId;
                foreach (var pluginFilePath in Directory.GetFiles(formatsLocation, "*", SearchOption.TopDirectoryOnly))
                {
                    if (!SevenZipPlugin.TryLoadPlugin(pluginFilePath, out var plugin))
                        continue;

                    _plugins.Add(plugin);

                    foreach (var pluginFormat in plugin.GetFormats())
                    {
                        _formats.Add((InArchiveFormat)inArchiveFormat++, pluginFormat);
                    }
                }
            }
        }

        public static bool TryFindPluginFormat(string actualSignature, out InArchiveFormat formatId)
        {
            LoadPlugins();

            foreach (var formatPair in _formats)
            {
                if (formatPair.Value.MatchSignature(actualSignature))
                {
                    formatId = formatPair.Key;
                    return true;
                }
            }

            formatId = default;
            return false;
        }

        public static IntPtr GetPluginPointer(Enum formatEnum)
        {
            if (!(formatEnum is InArchiveFormat inArchiveFormat))
                throw new NotSupportedException("Only in archives are supported"); // TODO: OutArchive support

            LoadPlugins();

            if (!_formats.TryGetValue(inArchiveFormat, out var format))
                throw new InvalidOperationException("Specified format is not supported by loaded 7z plugins");

            return format.Handle;
        }

        public static Guid GetInArchiveFormatClassId(InArchiveFormat inArchiveFormat)
        {
            LoadPlugins();

            if (!_formats.TryGetValue(inArchiveFormat, out var format))
                throw new InvalidOperationException("Specified format is not supported by loaded 7z plugins");

            return format.ClassId;
        }

        public static void FreeLibrary()
        {
            if (_plugins == null)
                return;

            lock (SyncRoot)
            {
                if (_plugins == null)
                    return;

                foreach (var plugin in _plugins)
                    plugin.Dispose();

                _plugins = null;
                _formats = null;
            }
        }
    }
}
