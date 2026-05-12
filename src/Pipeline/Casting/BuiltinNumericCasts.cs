using System.Numerics;

namespace Shiron.Lib.Pipeline.Casting;

public static class BuiltinNumericCasts {
    public static void RegisterAll(CastRegistry registry) {
        RegisterWidening(registry);
        RegisterNarrowing(registry);
    }

    private static void RegisterWidening(CastRegistry registry) {
        // byte → wider types
        registry.Register<byte, short>(TypeCast.Lossless, v => (short) v);
        registry.Register<byte, int>(TypeCast.Lossless, v => (int) v);
        registry.Register<byte, long>(TypeCast.Lossless, v => (long) v);
        registry.Register<byte, float>(TypeCast.Lossy, v => (float) v);
        registry.Register<byte, double>(TypeCast.Lossless, v => (double) v);
        registry.Register<byte, decimal>(TypeCast.Lossless, v => (decimal) v);

        // short → wider types
        registry.Register<short, int>(TypeCast.Lossless, v => (int) v);
        registry.Register<short, long>(TypeCast.Lossless, v => (long) v);
        registry.Register<short, float>(TypeCast.Lossy, v => (float) v);
        registry.Register<short, double>(TypeCast.Lossless, v => (double) v);
        registry.Register<short, decimal>(TypeCast.Lossless, v => (decimal) v);

        // int → wider types
        registry.Register<int, long>(TypeCast.Lossless, v => (long) v);
        registry.Register<int, float>(TypeCast.Lossy, v => (float) v);
        registry.Register<int, double>(TypeCast.Lossless, v => (double) v);
        registry.Register<int, decimal>(TypeCast.Lossless, v => (decimal) v);

        // long → wider types
        registry.Register<long, float>(TypeCast.Lossy, v => (float) v);
        registry.Register<long, double>(TypeCast.Lossy, v => (double) v);
        registry.Register<long, decimal>(TypeCast.Lossless, v => (decimal) v);

        // float → wider types
        registry.Register<float, double>(TypeCast.Lossless, v => (double) v);

        // sbyte → wider types
        registry.Register<sbyte, short>(TypeCast.Lossless, v => (short) v);
        registry.Register<sbyte, int>(TypeCast.Lossless, v => (int) v);
        registry.Register<sbyte, long>(TypeCast.Lossless, v => (long) v);
        registry.Register<sbyte, float>(TypeCast.Lossy, v => (float) v);
        registry.Register<sbyte, double>(TypeCast.Lossless, v => (double) v);
        registry.Register<sbyte, decimal>(TypeCast.Lossless, v => (decimal) v);

        // ushort → wider types
        registry.Register<ushort, int>(TypeCast.Lossless, v => (int) v);
        registry.Register<ushort, long>(TypeCast.Lossless, v => (long) v);
        registry.Register<ushort, float>(TypeCast.Lossy, v => (float) v);
        registry.Register<ushort, double>(TypeCast.Lossless, v => (double) v);
        registry.Register<ushort, decimal>(TypeCast.Lossless, v => (decimal) v);

        // uint → wider types
        registry.Register<uint, long>(TypeCast.Lossless, v => (long) v);
        registry.Register<uint, float>(TypeCast.Lossy, v => (float) v);
        registry.Register<uint, double>(TypeCast.Lossless, v => (double) v);
        registry.Register<uint, decimal>(TypeCast.Lossless, v => (decimal) v);

        // ulong → wider types
        registry.Register<ulong, float>(TypeCast.Lossy, v => (float) v);
        registry.Register<ulong, double>(TypeCast.Lossy, v => (double) v);
        registry.Register<ulong, decimal>(TypeCast.Lossless, v => (decimal) v);
    }

    private static void RegisterNarrowing(CastRegistry registry) {
        // All narrowing numeric casts are lossy
        // byte → narrower: nothing (byte is smallest unsigned)
        // short → narrower
        registry.Register<short, byte>(TypeCast.Lossy, v => (byte) v);
        registry.Register<short, sbyte>(TypeCast.Lossy, v => (sbyte) v);

        // int → narrower
        registry.Register<int, byte>(TypeCast.Lossy, v => (byte) v);
        registry.Register<int, sbyte>(TypeCast.Lossy, v => (sbyte) v);
        registry.Register<int, short>(TypeCast.Lossy, v => (short) v);
        registry.Register<int, ushort>(TypeCast.Lossy, v => (ushort) v);
        registry.Register<int, uint>(TypeCast.Lossy, v => (uint) v);

        // long → narrower
        registry.Register<long, byte>(TypeCast.Lossy, v => (byte) v);
        registry.Register<long, sbyte>(TypeCast.Lossy, v => (sbyte) v);
        registry.Register<long, short>(TypeCast.Lossy, v => (short) v);
        registry.Register<long, ushort>(TypeCast.Lossy, v => (ushort) v);
        registry.Register<long, int>(TypeCast.Lossy, v => (int) v);
        registry.Register<long, uint>(TypeCast.Lossy, v => (uint) v);
        registry.Register<long, ulong>(TypeCast.Lossy, v => (ulong) v);

        // float → narrower
        registry.Register<float, byte>(TypeCast.Lossy, v => (byte) v);
        registry.Register<float, sbyte>(TypeCast.Lossy, v => (sbyte) v);
        registry.Register<float, short>(TypeCast.Lossy, v => (short) v);
        registry.Register<float, ushort>(TypeCast.Lossy, v => (ushort) v);
        registry.Register<float, int>(TypeCast.Lossy, v => (int) v);
        registry.Register<float, uint>(TypeCast.Lossy, v => (uint) v);
        registry.Register<float, long>(TypeCast.Lossy, v => (long) v);
        registry.Register<float, ulong>(TypeCast.Lossy, v => (ulong) v);

        // double → narrower
        registry.Register<double, byte>(TypeCast.Lossy, v => (byte) v);
        registry.Register<double, sbyte>(TypeCast.Lossy, v => (sbyte) v);
        registry.Register<double, short>(TypeCast.Lossy, v => (short) v);
        registry.Register<double, ushort>(TypeCast.Lossy, v => (ushort) v);
        registry.Register<double, int>(TypeCast.Lossy, v => (int) v);
        registry.Register<double, uint>(TypeCast.Lossy, v => (uint) v);
        registry.Register<double, long>(TypeCast.Lossy, v => (long) v);
        registry.Register<double, ulong>(TypeCast.Lossy, v => (ulong) v);
        registry.Register<double, float>(TypeCast.Lossy, v => (float) v);

        // decimal → all narrower
        registry.Register<decimal, byte>(TypeCast.Lossy, v => (byte) v);
        registry.Register<decimal, sbyte>(TypeCast.Lossy, v => (sbyte) v);
        registry.Register<decimal, short>(TypeCast.Lossy, v => (short) v);
        registry.Register<decimal, ushort>(TypeCast.Lossy, v => (ushort) v);
        registry.Register<decimal, int>(TypeCast.Lossy, v => (int) v);
        registry.Register<decimal, uint>(TypeCast.Lossy, v => (uint) v);
        registry.Register<decimal, long>(TypeCast.Lossy, v => (long) v);
        registry.Register<decimal, ulong>(TypeCast.Lossy, v => (ulong) v);
        registry.Register<decimal, float>(TypeCast.Lossy, v => (float) v);
        registry.Register<decimal, double>(TypeCast.Lossy, v => (double) v);

        // sbyte → narrower: nothing (sbyte is smallest signed)
        // ushort → narrower
        registry.Register<ushort, byte>(TypeCast.Lossy, v => (byte) v);
        registry.Register<ushort, sbyte>(TypeCast.Lossy, v => (sbyte) v);
        registry.Register<ushort, short>(TypeCast.Lossy, v => (short) v);

        // uint → narrower
        registry.Register<uint, byte>(TypeCast.Lossy, v => (byte) v);
        registry.Register<uint, sbyte>(TypeCast.Lossy, v => (sbyte) v);
        registry.Register<uint, short>(TypeCast.Lossy, v => (short) v);
        registry.Register<uint, ushort>(TypeCast.Lossy, v => (ushort) v);
        registry.Register<uint, int>(TypeCast.Lossy, v => (int) v);

        // ulong → narrower
        registry.Register<ulong, byte>(TypeCast.Lossy, v => (byte) v);
        registry.Register<ulong, sbyte>(TypeCast.Lossy, v => (sbyte) v);
        registry.Register<ulong, short>(TypeCast.Lossy, v => (short) v);
        registry.Register<ulong, ushort>(TypeCast.Lossy, v => (ushort) v);
        registry.Register<ulong, int>(TypeCast.Lossy, v => (int) v);
        registry.Register<ulong, uint>(TypeCast.Lossy, v => (uint) v);
        registry.Register<ulong, long>(TypeCast.Lossy, v => (long) v);
    }
}
