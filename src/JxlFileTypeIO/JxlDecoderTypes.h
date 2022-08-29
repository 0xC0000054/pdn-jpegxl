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

#pragma once

#include <stdint.h>

struct DecoderContext;

enum class DecoderStatus : int32_t
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
};

struct DecoderImageInfo
{
    int32_t width;
    int32_t height;
    size_t iccProfileSize;
};
