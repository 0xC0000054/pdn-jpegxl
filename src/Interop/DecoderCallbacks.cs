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
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class DecoderCallbacks
    {
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public readonly DecoderCreateLayerCallback createLayer;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public readonly DecoderCreateMetadataBufferCallback createMetadataBuffer;

        public DecoderCallbacks(DecoderCreateLayerCallback createLayer,
                                DecoderCreateMetadataBufferCallback createMetadataBuffer)
        {
            ArgumentNullException.ThrowIfNull(createLayer);
            ArgumentNullException.ThrowIfNull(createMetadataBuffer);

            this.createLayer = createLayer;
            this.createMetadataBuffer = createMetadataBuffer;
        }
    }
}
