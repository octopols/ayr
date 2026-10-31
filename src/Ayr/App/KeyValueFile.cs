using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ayr.App
{
    public sealed class KeyValueFile
    {
        private const char Separator = '=';

        private readonly string _path;

        public KeyValueFile(string path)
        {
            _path = path;
        }

        public Dictionary<string, string> Read()
        {
            var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_path))
            {
                return entries;
            }
            foreach (string line in File.ReadAllLines(_path))
            {
                int separator = line.LastIndexOf(Separator);
                if (separator > 0)
                {
                    entries[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
                }
            }
            return entries;
        }

        public void Write(IDictionary<string, string> entries)
        {
            File.WriteAllLines(_path, entries.Select(entry => entry.Key + Separator + entry.Value));
        }
    }
}
