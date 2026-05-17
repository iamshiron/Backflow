namespace Shiron.Lib.Pipeline.Casting;

/// <summary>Classification of a type conversion's safety.</summary>
public enum TypeCast {
    /// <summary>No cast available.</summary>
    None,
    /// <summary>No data loss (e.g., <c>int</c> → <c>long</c>).</summary>
    Lossless,
    /// <summary>Potential data loss (e.g., <c>long</c> → <c>int</c>, <c>float</c> → <c>int</c>).</summary>
    Lossy,
}
