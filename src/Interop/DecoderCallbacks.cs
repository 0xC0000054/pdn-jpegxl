////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024, 2025, 2026 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void SetBasicInfoDelegate(int canvasWidth,
                                                int canvasHeight,
                                                JpegXLColorSpace format,
                                                JpegXLImageChannelRepresentation channelRepresentation,
                                                [MarshalAs(UnmanagedType.U1) ]bool hasTransparency);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal unsafe delegate bool SetMetadataDelegate(byte* data, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal unsafe delegate bool SetKnownColorProfileDelegate(KnownColorProfile profile);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal unsafe delegate bool SetLayerDataDelegate(byte* pixels, byte* name, nuint nameLength);

    [StructLayout(LayoutKind.Sequential)]
    internal struct DecoderCallbacks
    {
        public nint setBasicInfo;
        public nint setIccProfile;
        public nint setKnownColorProfile;
        public nint setExif;
        public nint setXmp;
        public nint setLayerData;
    }
}
