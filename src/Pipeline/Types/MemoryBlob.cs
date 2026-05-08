using System.Security.Cryptography;
using Shiron.Lib.Pipeline.Caching;

namespace Shiron.Lib.Pipeline.Types;

public class MemoryBlob : IBlob, IImageBlob, ICacheHashable {
    private byte[] _data = [];
    private string? _contentHash;

    public byte[] Data {
        get => _data;
        set {
            _data = value;
            _contentHash = null;
        }
    }

    public string GetCacheHash() {
        return _contentHash ??= ComputeHash(_data);
    }

    private static string ComputeHash(byte[] data) {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }
}
