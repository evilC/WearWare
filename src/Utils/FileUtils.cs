using System.IO;

namespace WearWare.Utils
{
    public static class FileUtils
    {
        public static async Task CopyFileAsync(string sourcePath, string destPath, int bufferSize = 81920)
        {
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
            using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
            await sourceStream.CopyToAsync(destStream, bufferSize).ConfigureAwait(false);
            await destStream.FlushAsync().ConfigureAwait(false);
        }
    }
}
