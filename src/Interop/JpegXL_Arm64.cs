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

using System;
using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    internal static class JpegXL_Arm64
    {
        private const string DllName = "JpegXLFileType_ARM64.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern SafeDecoderContextArm64 CreateDecoder();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern void DestroyDecoder(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern unsafe DecoderStatus DecodeFile(SafeDecoderContext context,
                                                              byte* data,
                                                              nuint dataSize,
                                                              [In, Out] DecoderImageInfo info,
                                                              ref ErrorInfo errorInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern unsafe DecoderStatus GetIccProfileData(SafeDecoderContext context,
                                                                     byte* data,
                                                                     nuint dataSize);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern void CopyDecodedPixelsToSurface(SafeDecoderContext context, [In] ref BitmapData bitmap);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern EncoderStatus SaveImage([In] ref BitmapData bitmap,
                                                       EncoderOptions options,
                                                       [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(EncoderImageMetadataMarshaller))] EncoderImageMetadata? metadata,
                                                       ref ErrorInfo errorInfo,
                                                       [MarshalAs(UnmanagedType.FunctionPtr)] ProgressCallback? progressCallback,
                                                       [MarshalAs(UnmanagedType.FunctionPtr)] WriteDataCallback writeDataCallback);

    }
}
