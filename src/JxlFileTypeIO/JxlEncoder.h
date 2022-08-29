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
#include "JxlEncoderTypes.h"

EncoderStatus EncoderWriteImage(
    const BitmapData* bitmap,
    const EncoderOptions* options,
    const EncoderImageMetadata* metadata,
    ErrorInfo* errorInfo,
    ProgressProc progressCallback,
    WriteDataProc writeDataCallback);
