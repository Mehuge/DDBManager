using System;
using System.IO;

namespace DDBManager.Models
{
    public class BackupEntry
    {
        public char Type { get; set; }
        public string FileMode { get; set; } = string.Empty;
        public DateTime CTime { get; set; }
        public DateTime MTime { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;

        public static BackupEntry? FromManifestLine(string line)
        {
            try
            {
                // V2 Format: Type Mode CTime MTime - Size Hash Variant Path
                // Parts: [0]Type [1]Mode [2]CTime [3]MTime [4]- [5]Size [6]Hash [7]Variant [8]Path
                var parts = line.Split(' ', 9, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 9) return null;

                string rawPath = parts[8].Trim('"').Replace('/', '\\');

                // Skip the root entry (where path is empty)
                if (string.IsNullOrEmpty(rawPath)) return null;

                return new BackupEntry
                {
                    Type = parts[0][0],
                    FileMode = parts[1],
                    CTime = DateTime.Parse(parts[2]),
                    MTime = DateTime.Parse(parts[3]),
                    Size = long.Parse(parts[5]),
                    Hash = parts[6],
                    Path = rawPath
                };
            }
            catch { return null; }
        }

        public string GetPhysicalStorePath(string storeRoot)
        {
            if (Hash.Length < 4) return string.Empty;
            string p1 = Hash.Substring(0, 2);
            string p2 = Hash.Substring(2, 2);
            string p3 = Hash.Substring(4);
            return System.IO.Path.Combine(storeRoot, "files.db", p1, p2, $"{p3}.{Size}");
        }
    }
}