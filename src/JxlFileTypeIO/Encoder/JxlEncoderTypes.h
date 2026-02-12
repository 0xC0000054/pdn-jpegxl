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

#pragma once

#include <stdint.h>

enum class EncoderStatus : int32_t
{
    Ok,
    NullParameter,
    OutOfMemory,
    UserCanceled,
    EncodeError,
    WriteError
};

struct EncoderOptions
{
    float distance;
    int32_t effort;
    bool lossless;
};

struct EncoderImageMetadata
{
    uint8_t* exif;
    size_t exifSize;
    uint8_t* iccProfile;
    size_t iccProfileSize;
    uint8_t* xmp;
    size_t xmpSize;
};
