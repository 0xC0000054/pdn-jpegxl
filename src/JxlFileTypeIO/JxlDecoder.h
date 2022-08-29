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

DecoderContext* CreateDecoderContext();

void DestroyDecoderContext(DecoderContext* context);

DecoderStatus DecoderParseFile(
    DecoderContext* context,
    const uint8_t* data,
    size_t dataSize,
    DecoderImageInfo* imageInfo,
    ErrorInfo* errorInfo);

DecoderStatus DecoderGetIccProfileData(
    DecoderContext* context,
    uint8_t* buffer,
    size_t bufferSize);

void DecoderCopyPixelsToSurface(
    DecoderContext* context,
    BitmapData* bitmap);
