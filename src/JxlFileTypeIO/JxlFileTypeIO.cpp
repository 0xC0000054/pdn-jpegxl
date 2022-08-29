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

DecoderContext* __stdcall CreateDecoder()
{
    return CreateDecoderContext();
}

void __stdcall DestroyDecoder(DecoderContext* context)
{
    DestroyDecoderContext(context);
}

DecoderStatus __stdcall DecodeFile(
    DecoderContext* context,
    const uint8_t* data,
    size_t dataSize,
    DecoderImageInfo* imageInfo,
    ErrorInfo* errorInfo)
{
    return DecoderParseFile(context, data, dataSize, imageInfo, errorInfo);
}

DecoderStatus __stdcall GetIccProfileData(DecoderContext* context, uint8_t* buffer, size_t bufferSize)
{
    return DecoderGetIccProfileData(context, buffer, bufferSize);
}

void __stdcall CopyDecodedPixelsToSurface(DecoderContext* context, BitmapData* bitmap)
{
    DecoderCopyPixelsToSurface(context, bitmap);
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
