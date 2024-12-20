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
#include "Common.h"

namespace PixelFormatConversion
{
    void BgraToGray(const BitmapData* bitmap, uint8_t* gray);
    void BgraToGrayAlpha(const BitmapData* bitmap, uint8_t* gray);
    void BgraToRgb(const BitmapData* bitmap, uint8_t* rgb);
    void BgraToRgba(const BitmapData* bitmap, uint8_t* rgba);
}
