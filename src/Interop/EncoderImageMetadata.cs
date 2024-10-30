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
using System.Runtime.InteropServices.Marshalling;

namespace JpegXLFileTypePlugin.Interop
{
    [NativeMarshalling(typeof(Marshaller))]
    internal sealed partial class EncoderImageMetadata
    {
        public readonly ReadOnlyMemory<byte> exif;
        public readonly ReadOnlyMemory<byte> iccProfile;
        public readonly ReadOnlyMemory<byte> xmp;

        public EncoderImageMetadata(byte[]? exifBytes, byte[]? iccProfileBytes, byte[]? xmpBytes)
        {
            exif = exifBytes;
            iccProfile = iccProfileBytes;
            xmp = xmpBytes;
        }
    }
}
