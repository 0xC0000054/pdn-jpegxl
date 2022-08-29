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

struct BitmapData
{
    uint8_t* scan0;
    uint32_t width;
    uint32_t height;
    uint32_t stride;
};

struct ColorBgra
{
    uint8_t b;
    uint8_t g;
    uint8_t r;
    uint8_t a;
};

struct ErrorInfo
{
    static const size_t maxErrorMessageLength = 255;

    char errorMessage[maxErrorMessageLength + 1];
};

void SetErrorMessage(ErrorInfo* errorInfo, const char* message);
void SetErrorMessageFormat(ErrorInfo* errorInfo, const char* format, ...);
