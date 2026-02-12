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
    internal static partial class JpegXL_X64
    {
        private const string DllName = "JpegXLFileTypeIO_X64.dll";

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static partial uint GetLibJxlVersion();

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial DecoderStatus LoadImage(in DecoderCallbacks callbacks,
                                                               byte* data,
                                                               nuint dataSize,
                                                               ref ErrorInfo errorInfo);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static partial EncoderStatus SaveImage(in BitmapData bitmap,
                                                        in EncoderOptions options,
                                                        in EncoderImageMetadata metadata,
                                                        in IOCallbacks callbacks,
                                                        ref ErrorInfo errorInfo,
                                                        [MarshalAs(UnmanagedType.FunctionPtr)] ProgressCallback? progressCallback);
    }
}
