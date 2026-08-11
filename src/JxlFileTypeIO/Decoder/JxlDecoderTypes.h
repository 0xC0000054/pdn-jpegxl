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

#include "Common.h"

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
    MetadataError,
    InvalidFileSignature,
};

enum class DecoderImageFormat : int32_t
{
    Gray = 0,
    Rgb,
    Cmyk
};

// RGB color encodings are reported via setCicpColorInfo; only the two gray encodings use this enum. (Gray
// images are loaded as RGB because WIC has poor gray-to-RGB support.)
enum class KnownColorProfile : int32_t
{
    LinearGray = 0,
    GraySrgbTRC,
};

// The result of the setCicpColorInfo callback.
// The values must stay in sync with the managed SetCicpColorInfoResult enum.
enum class SetCicpColorInfoResult : int32_t
{
    Ok = 0,
    // The managed layer cannot represent these code points; fall back to the ICC profile.
    Unsupported,
    Error,
};

typedef void(__stdcall* DecoderSetBasicInfo)(
    int32_t width,
    int32_t height,
    DecoderImageFormat format,
    ImageChannelRepresentation channelFormat,
    bool hasTransparency);
typedef bool(__stdcall* DecoderSetMetadata)(uint8_t* data, size_t length);
typedef bool(__stdcall* DecoderSetKnownColorProfile)(KnownColorProfile profile);
// Reports the image's color information as CICP code points (ITU-T H.273), plus the HDR intensity target
// (the peak luminance in nits, from JxlBasicInfo.intensity_target). Used for RGB color encodings that map to
// a CICP color space; gray and non-mappable encodings use setKnownColorProfile / setIccProfile instead.
typedef SetCicpColorInfoResult(__stdcall* DecoderSetCicpColorInfo)(
    uint8_t colorPrimaries,
    uint8_t transferCharacteristics,
    uint8_t matrixCoefficients,
    uint8_t videoFullRangeFlag,
    float intensityTargetNits);
typedef bool(__stdcall* DecoderSetLayerData)(uint8_t* pixels, char* name, size_t nameLength);

struct DecoderCallbacks
{
    DecoderSetBasicInfo setBasicInfo;
    DecoderSetMetadata setIccProfile;
    DecoderSetKnownColorProfile setKnownColorProfile;
    DecoderSetCicpColorInfo setCicpColorInfo;
    DecoderSetMetadata setExif;
    DecoderSetMetadata setXmp;
    DecoderSetLayerData setLayerData;
};
