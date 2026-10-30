using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Shiron.Backflow.Caching;

internal static class PersistentFile {
    internal static void CheckPath(string path) {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current)) {
            try {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"Symbolic links are not supported in storage paths: {current}");
            } catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
        }
    }

    internal static async ValueTask<FileStream> AcquireAsync(string path, TimeSpan timeout, CancellationToken ct = default) {
        var timer = Stopwatch.StartNew();
        while (true) {
            ct.ThrowIfCancellationRequested();
            CheckPath(path);
            try {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            } catch (IOException) when (timer.Elapsed < timeout) {
                await Task.Delay(TimeSpan.FromMilliseconds(10), ct).ConfigureAwait(false);
            }
        }
    }

    internal static async Task WriteAsync(string path, ReadOnlyMemory<byte> data,
        Action<string, string>? beforePublish = null, CancellationToken ct = default) {
        CheckPath(path);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                8192, FileOptions.Asynchronous | FileOptions.WriteThrough)) {
                await stream.WriteAsync(data, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            ct.ThrowIfCancellationRequested();
            beforePublish?.Invoke(temp, path);
            CheckPath(path);
            File.Move(temp, path, overwrite: true);
            SyncDirectory(Path.GetDirectoryName(path)!);
        } finally {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    internal static void RecoverTemps(string path) {
        var prefix = Path.GetFileName(path) + ".tmp-";
        foreach (var temp in Directory.EnumerateFiles(Path.GetDirectoryName(path)!)) {
            if (!Path.GetFileName(temp).StartsWith(prefix, StringComparison.Ordinal)) continue;
            CheckPath(temp);
            File.Delete(temp);
        }
    }

    internal static void SyncDirectory(string path) {
        if (!OperatingSystem.IsLinux()) return;
        var descriptor = Open(path, 0);
        if (descriptor < 0) throw new IOException("Cannot open storage directory for synchronization.", new Win32Exception(Marshal.GetLastPInvokeError()));
        try {
            if (Fsync(descriptor) != 0)
                throw new IOException("Cannot synchronize storage directory.", new Win32Exception(Marshal.GetLastPInvokeError()));
        } finally { Close(descriptor); }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int Fsync(int descriptor);
    [DllImport("libc", EntryPoint = "close")]
    private static extern int Close(int descriptor);
}
