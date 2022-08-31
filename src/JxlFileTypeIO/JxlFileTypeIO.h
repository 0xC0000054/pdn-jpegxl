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

#include "Common.h"
#include "JxlDecoderTypes.h"
#include "JxlEncoderTypes.h"

#ifdef JXLFILETYPEIO_EXPORTS
#define JXLFILETYPEIO_API __declspec(dllexport)
#else
#define JXLFILETYPEIO_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

JXLFILETYPEIO_API DecoderStatus __stdcall LoadImage(
    DecoderCallbacks* callbacks,
    const uint8_t* data,
    size_t dataSize,
    ErrorInfo* errorInfo);

JXLFILETYPEIO_API EncoderStatus __stdcall SaveImage(
    const BitmapData* bitmap,
    const EncoderOptions* options,
    const EncoderImageMetadata* metadata,
    ErrorInfo* errorInfo,
    ProgressProc progressCallback,
    WriteDataProc writeDataCallback);

#ifdef __cplusplus
}
#endif
