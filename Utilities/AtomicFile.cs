using System.Diagnostics;

namespace Nimbus_Internet_Blocker.Utilities;

/// <summary>
/// Crash-safe file writes: content lands in a temp file first, then atomically
/// replaces the target, so a failure mid-write can never truncate the live file.
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/> atomically.
    /// Returns <see langword="false"/> on any failure; the previous file, if one
    /// existed, is left intact. Never throws.
    /// </summary>
    public static async Task<bool> WriteAllTextAtomicAsync(string path, string content)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, content);

            if (File.Exists(path))
                File.Replace(tmp, path, destinationBackupFileName: null);
            else
                File.Move(tmp, path);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AtomicFile.WriteAllTextAtomicAsync failed for {path}: {ex}");
            return false;
        }
    }
}
