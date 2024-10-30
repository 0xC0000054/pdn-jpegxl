////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct DecoderCallbacks
    {
        public readonly nint createLayer;
        public readonly nint createMetadataBuffer;

        public DecoderCallbacks(DecoderCreateLayerCallback createLayer,
                                DecoderCreateMetadataBufferCallback createMetadataBuffer)
        {
            ArgumentNullException.ThrowIfNull(createLayer);
            ArgumentNullException.ThrowIfNull(createMetadataBuffer);

            this.createLayer = Marshal.GetFunctionPointerForDelegate(createLayer);
            this.createMetadataBuffer = Marshal.GetFunctionPointerForDelegate(createMetadataBuffer);
        }
    }
}
