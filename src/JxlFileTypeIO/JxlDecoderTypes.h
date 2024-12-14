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

#pragma once

#include <stdint.h>

enum class DecoderStatus : int32_t
{
    Ok,
    NullParameter,
    InvalidParameter,
    OutOfMemory,
    HasAnimation,
    HasMultipleFrames,
    ImageDimensionExceedsInt32,
    UnsupportedChannelFormat,
    CreateLayerError,
    CreateMetadataError,
    DecodeError,
    MetadataError
};

enum class DecoderImageFormat : int32_t
{
    Gray = 0,
    Rgb
};

enum class KnownColorProfile : int32_t
{
    Srgb = 0,
    LinearSrgb,
    LinearGray,
    GraySrgbTRC,
};

typedef void(__stdcall* DecoderSetBasicInfo)(
    int32_t width,
    int32_t height,
    DecoderImageFormat format,
    bool hasTransparency);
typedef bool(__stdcall* DecoderSetMetadata)(uint8_t* data, size_t length);
typedef bool(__stdcall* DecoderSetKnownColorProfile)(KnownColorProfile profile);
typedef bool(__stdcall* DecoderSetLayerData)(uint8_t* pixels, char* name, size_t nameLength);

struct DecoderCallbacks
{
    DecoderSetBasicInfo setBasicInfo;
    DecoderSetMetadata setIccProfile;
    DecoderSetKnownColorProfile setKnownColorProfile;
    DecoderSetMetadata setExif;
    DecoderSetMetadata setXmp;
    DecoderSetLayerData setLayerData;
};
