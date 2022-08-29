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

namespace JpegXLFileTypePlugin.Interop
{
    internal enum DecoderStatus : int
    {
        Ok,
        NullParameter,
        InvalidParameter,
        BufferTooSmall,
        OutOfMemory,
        HasAnimation,
        HasMultipleFrames,
        ImageDimensionExceedsInt32,
        UnsupportedChannelFormat,
        DecodeError,
        MetadataError
    }
}
