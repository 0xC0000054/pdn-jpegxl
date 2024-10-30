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
    CreateMetadataBufferError,
    DecodeError,
    MetadataError
};

// Creates a layer and populates the outLayerData parameter with the layer information.
// Returns true if successful, or false if an error occurred when creating the layer.
typedef bool(__stdcall* DecoderCreateLayerCallback)(
    int32_t width,
    int32_t height,
    char* name,
    uint32_t nameLength,
    BitmapData* outLayerData);
// Creates a buffer to store the specified meta data, and returns a pointer to it.
// Returns a pointer to the allocated data buffer, or NULL if an error occurred.
typedef uint8_t*(__stdcall* DecoderCreateMetadataBufferCallback)(MetadataType type, size_t bufferSize);

struct DecoderCallbacks
{
    DecoderCreateLayerCallback createLayer;
    DecoderCreateMetadataBufferCallback createMetadataBuffer;
};
