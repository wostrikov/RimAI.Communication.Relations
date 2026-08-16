using System;
using System.IO;

namespace Ustas.RimAI.Communication.Relations.Util
{
    /// <summary>
    /// Atomic file write operations. Uses File.Replace on existing targets (atomic on NTFS)
    /// with a fallback to copy+delete, avoiding TOCTOU races inherent to File.Delete + File.Move.
    /// </summary>
    public static class AtomicFileWriter
    {
        public static void WriteAllText(string path, string content)
        {
            string tempPath = path + ".tmp";

            try
            {
                File.WriteAllText(tempPath, content);

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    }
                    catch
                    {
                        File.Copy(tempPath, path, overwrite: true);
                        File.Delete(tempPath);
                    }
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }
    }
}
