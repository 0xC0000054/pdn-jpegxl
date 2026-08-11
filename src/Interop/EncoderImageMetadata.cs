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

using PaintDotNet.Imaging;
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
        public readonly bool hasCicpColorInfo;
        public readonly byte cicpColorPrimaries;
        public readonly byte cicpTransferCharacteristics;
        public readonly byte cicpMatrixCoefficients;
        public readonly byte cicpVideoFullRangeFlag;

        public EncoderImageMetadata(byte[]? exifBytes, byte[]? iccProfileBytes, byte[]? xmpBytes, CicpColorSpace? cicpColorSpace)
        {
            exif = exifBytes;
            iccProfile = iccProfileBytes;
            xmp = xmpBytes;

            if (cicpColorSpace.HasValue)
            {
                CicpColorSpace cicp = cicpColorSpace.Value;
                hasCicpColorInfo = true;
                cicpColorPrimaries = (byte)cicp.ColorPrimaries;
                cicpTransferCharacteristics = (byte)cicp.TransferCharacteristics;
                cicpMatrixCoefficients = (byte)cicp.MatrixCoefficients;
                cicpVideoFullRangeFlag = (byte)cicp.VideoFullRangeFlag;
            }
        }
    }
}
