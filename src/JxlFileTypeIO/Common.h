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

// "ColourPrimaries" code points (H.273 Table 2). Reserved/unassigned: 0, 3, 13-21, 23-255.
enum class CicpColorPrimaries : uint8_t
{
    Bt709 = 1,       // BT.709-6; also sRGB/sYCC and IEC 61966-2-4 (D65)
    Unspecified = 2, // Determined by the application or by other means
    Bt470M = 4,      // BT.470-6 System M (historical); NTSC 1953; FCC Title 47
    Bt470Bg = 5,     // BT.470-6 System B, G; BT.601-7 625; BT.1700 625 PAL/SECAM
    Smpte170M = 6,   // BT.601-7 525; BT.1700 NTSC; SMPTE ST 170
    Smpte240M = 7,   // SMPTE ST 240 (historical); same primaries as Smpte170M
    GenericFilm = 8, // Generic film (colour filters using Illuminant C)
    Bt2020 = 9,      // BT.2020-2 and BT.2100-2 (wide gamut; D65)
    Smpte428 = 10,   // SMPTE ST 428-1 (CIE 1931 XYZ; "E" white point)
    Smpte431 = 11,   // SMPTE RP 431-2 (DCI P3, theater; DCI white point)
    Smpte432 = 12,   // SMPTE EG 432-1 (Display P3; D65)
    Ebu3213 = 22     // EBU Tech. 3213-E (historical); JEDEC P22 phosphors
};

// "TransferCharacteristics" code points (H.273 Table 3). Reserved/unassigned: 0, 3, 19-255.
enum class CicpTransferCharacteristics : uint8_t
{
    Bt709 = 1,            // BT.709-6 (same function as Bt601, Bt2020TenBit, Bt2020TwelveBit)
    Unspecified = 2,      // Determined by the application or by other means
    Gamma22 = 4,          // BT.470-6 System M (historical); assumed display gamma 2.2
    Gamma28 = 5,          // BT.470-6 System B, G (historical); assumed display gamma 2.8
    Bt601 = 6,            // BT.601-7 525 or 625; BT.1700; SMPTE ST 170
    Smpte240M = 7,        // SMPTE ST 240 (historical)
    Linear = 8,           // Linear transfer characteristics
    Log100 = 9,           // Logarithmic (100:1 range)
    Log100Sqrt10 = 10,    // Logarithmic (100 * Sqrt(10) : 1 range)
    Iec6196624 = 11,      // IEC 61966-2-4 (xvYCC)
    Bt1361 = 12,          // BT.1361-0 extended colour gamut system (historical)
    Srgb = 13,            // IEC 61966-2-1 (sRGB or sYCC)
    Bt2020TenBit = 14,    // BT.2020-2 10-bit system (same function as Bt709)
    Bt2020TwelveBit = 15, // BT.2020-2 12-bit system (same function as Bt709)
    SmpteSt2084PQ = 16,   // SMPTE ST 2084; BT.2100-2 perceptual quantization (PQ)
    SmpteSt428 = 17,      // SMPTE ST 428-1
    AribStdB67Hlg = 18    // ARIB STD-B67; BT.2100-2 hybrid log-gamma (HLG)
};

// "MatrixCoefficients" code points (H.273 Table 4). Reserved/unassigned: 3, 15-255.
enum class CicpMatrixCoefficients : uint8_t
{
    Identity = 0,                // RGB/GBR/XYZ, no matrixing; also sRGB and SMPTE ST 428-1
    Bt709 = 1,                   // BT.709-6
    Unspecified = 2,             // Determined by the application or by other means
    Fcc = 4,                     // US FCC Title 47 CFR 73.682 (a) (20)
    Bt470Bg = 5,                 // BT.470-6 System B, G; BT.601-7 625; BT.1700 625 PAL/SECAM
    Smpte170M = 6,               // BT.601-7 525; BT.1700 NTSC; SMPTE ST 170
    Smpte240M = 7,               // SMPTE ST 240 (historical)
    YCgCo = 8,                   // YCgCo
    Bt2020Ncl = 9,               // BT.2020-2 non-constant luminance; BT.2100-2 Y'CbCr
    Bt2020Cl = 10,               // BT.2020-2 constant luminance
    SmpteSt2085 = 11,            // SMPTE ST 2085 (Y'D'zD'x)
    ChromaticityDerivedNcl = 12, // Chromaticity-derived non-constant luminance
    ChromaticityDerivedCl = 13,  // Chromaticity-derived constant luminance
    ICtCp = 14                   // BT.2100-2 ICtCp
};

// "VideoFullRangeFlag" (H.273). Indicates the black level and range of the signal.
enum class CicpVideoFullRangeFlag : uint8_t
{
    Narrow = 0, // Narrow ("limited"/"studio swing") range, e.g. 16-235 for 8-bit luma
    Full = 1    // Full range: the signal occupies the full numeric range of the encoding
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
