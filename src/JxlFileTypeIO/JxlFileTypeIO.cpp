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

#include "JxlFileTypeIO.h"
#include "JxlDecoder.h"
#include "JxlEncoder.h"

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
    ErrorInfo* errorInfo,
    ProgressProc progressCallback,
    WriteDataProc writeDataCallback)
{
    return EncoderWriteImage(bitmap, options, metadata, errorInfo, progressCallback, writeDataCallback);
}
