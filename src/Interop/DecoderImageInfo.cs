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
#pragma warning disable 0649

    [StructLayout(LayoutKind.Sequential)]
    internal sealed class DecoderImageInfo
    {
        public readonly int width;
        public readonly int height;
        public readonly nuint iccProfileSize;
    }

#pragma warning restore 0649
}
