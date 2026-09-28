using System.Runtime.InteropServices;

namespace SignalHouse.Core.Imaging.RawDecoding;

/// <summary>
/// P/Invoke bindings for LibRaw's "simple" C API -- the small, stable subset
/// of functions LibRaw documents specifically so language bindings can open
/// a RAW file, decode it, and pull out a plain RGB buffer without needing to
/// understand LibRaw's much larger internal `libraw_data_t` struct.
///
/// *** EXTERNAL DEPENDENCY THIS PROJECT DOES NOT INCLUDE ***
/// This binds against the native LibRaw shared library (libraw.dll on
/// Windows, liblibraw.so on Linux, liblibraw.dylib on macOS), which is not
/// bundled here. Add it to SignalHouse.App by either:
///   1. Adding a NuGet package that bundles LibRaw's native binaries (search
///      NuGet for "LibRaw" -- several community packages exist that ship
///      prebuilt binaries for win-x64/linux-x64/osx), or
///   2. Building LibRaw yourself (https://www.libraw.org) and copying the
///      resulting shared library next to SignalHouse.App's output.
/// This workspace has no network access to verify an exact package name or
/// version, so confirm one against what's currently on NuGet before you
/// build. Whichever you pick, the exported function names must match the
/// ones DllImport'd below -- LibRaw's C API has used these names and this
/// calling convention since the library introduced its "simple" bindings
/// layer, so this should not need changes on your end.
/// </summary>
internal static class LibRawNative
{
    // The runtime will map this base name to libraw.dll / liblibraw.so /
    // liblibraw.dylib automatically depending on platform.
    private const string LibRawDll = "libraw";

    [DllImport(LibRawDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr libraw_init(uint flags);

    [DllImport(LibRawDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int libraw_open_file(IntPtr libRawData, [MarshalAs(UnmanagedType.LPStr)] string fileName);

    [DllImport(LibRawDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int libraw_unpack(IntPtr libRawData);

    [DllImport(LibRawDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int libraw_dcraw_process(IntPtr libRawData);

    [DllImport(LibRawDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr libraw_dcraw_make_mem_image(IntPtr libRawData, out int errorCode);

    [DllImport(LibRawDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void libraw_dcraw_clear_mem(IntPtr processedImage);

    [DllImport(LibRawDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void libraw_close(IntPtr libRawData);

    [DllImport(LibRawDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr libraw_strerror(int errorCode);

    internal static string GetError(int code)
    {
        var ptr = libraw_strerror(code);
        return ptr == IntPtr.Zero ? $"LibRaw error {code}" : Marshal.PtrToStringAnsi(ptr) ?? $"LibRaw error {code}";
    }

    /// <summary>
    /// Mirrors the header of LibRaw's public `libraw_processed_image_t`
    /// struct, which LibRaw keeps stable across versions specifically so
    /// bindings like this one can rely on its layout. The pixel bytes
    /// described by <see cref="DataSize"/> are NOT part of this managed
    /// struct -- they begin immediately after it in unmanaged memory, at
    /// <c>imagePointer + LibRawProcessedImage.DataOffset</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct LibRawProcessedImage
    {
        public int Type;       // enum LibRaw_image_formats; 1 = LIBRAW_IMAGE_BITMAP (what dcraw_make_mem_image produces)
        public ushort Height;
        public ushort Width;
        public ushort Colors;  // 3 for RGB
        public ushort Bits;    // 8 or 16 bits per channel
        public uint DataSize;

        public static readonly int DataOffset = Marshal.SizeOf<LibRawProcessedImage>();
    }
}
