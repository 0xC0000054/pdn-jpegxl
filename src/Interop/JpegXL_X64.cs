////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    internal static class JpegXL_X64
    {
        private const string DllName = "JpegXLFileTypeIO_X64.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern unsafe DecoderStatus LoadImage(DecoderCallbacks callbacks,
                                                              byte* data,
                                                              nuint dataSize,
                                                              ref ErrorInfo errorInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern EncoderStatus SaveImage([In] ref BitmapData bitmap,
                                                       EncoderOptions options,
                                                       [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(EncoderImageMetadataMarshaller))] EncoderImageMetadata? metadata,
                                                       ref ErrorInfo errorInfo,
                                                       [MarshalAs(UnmanagedType.FunctionPtr)] ProgressCallback? progressCallback,
                                                       [MarshalAs(UnmanagedType.FunctionPtr)] WriteDataCallback writeDataCallback);
    }
}
