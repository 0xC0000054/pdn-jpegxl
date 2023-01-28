////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class EncoderImageMetadata
    {
        public byte[]? exif;
        public byte[]? iccProfile;
        public byte[]? xmp;

        public EncoderImageMetadata(byte[]? exifBytes, byte[]? iccProfileBytes, byte[]? xmpBytes)
        {
            exif = exifBytes;
            iccProfile = iccProfileBytes;
            xmp = xmpBytes;
        }
    }
}
