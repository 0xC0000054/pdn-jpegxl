////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024, 2025, 2026 Nicholas Hayes
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

enum class ImageChannelRepresentation : int32_t
{
    Uint8 = 0,
    Uint16,
    Float16,
    Float32
};

typedef bool(__stdcall* ProgressProc)(int32_t progressPrecentage);

// The I/O Callbacks return a Windows HRESULT, we do not include Windows.h
// in this header to avoid naming conflicts with method names in other files.

typedef int32_t(__stdcall* WriteCallback)(const uint8_t* buffer, size_t sizeInBytes);
typedef int32_t(__stdcall* SeekCallback)(uint64_t position);

struct IOCallbacks
{
    WriteCallback Write;
    SeekCallback Seek;
};

struct ErrorInfo
{
    static const size_t maxErrorMessageLength = 255;

    char errorMessage[maxErrorMessageLength + 1];
};

void SetErrorMessage(ErrorInfo* errorInfo, const char* message);
void SetErrorMessageFormat(ErrorInfo* errorInfo, const char* format, ...);
