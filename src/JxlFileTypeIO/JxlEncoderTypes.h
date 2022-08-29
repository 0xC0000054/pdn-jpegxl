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

typedef bool(__stdcall* ProgressProc)(int32_t progressPrecentage);

typedef bool(__stdcall* WriteDataProc)(const uint8_t* buffer, size_t bufferSize);

enum class EncoderStatus : int32_t
{
    Ok,
    NullParameter,
    OutOfMemory,
    UserCancelled,
    EncodeError,
    WriteError
};

struct EncoderOptions
{
    float distance;
    int32_t speed;
    bool lossless;
};

struct EncoderImageMetadata
{
    uint8_t* iccProfile;
    size_t iccProfileSize;
};
