﻿////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024, 2025 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

#include "JxlFileTypeIO.h"
#include "JxlDecoder.h"
#include "JxlEncoder.h"
#include "jxl/version.h"

uint32_t __stdcall GetLibJxlVersion()
{
    return JPEGXL_NUMERIC_VERSION;
}

DecoderStatus __stdcall LoadImage(
    DecoderCallbacks* callbacks,
    const uint8_t* data,
    size_t dataSize,
    ErrorInfo* errorInfo)
{
    return DecoderReadImage(callbacks, data, dataSize, errorInfo);
}

EncoderStatus __stdcall SaveImage(
    const BitmapData* bitmap,
    const EncoderOptions* options,
    const EncoderImageMetadata* metadata,
    IOCallbacks* callbacks,
    ErrorInfo* errorInfo,
    ProgressProc progressCallback)
{
    return EncoderWriteImage(bitmap, options, metadata, callbacks, errorInfo, progressCallback);
}
