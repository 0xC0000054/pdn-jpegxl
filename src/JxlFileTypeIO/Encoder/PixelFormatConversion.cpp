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

#include "PixelFormatConversion.h"
#include "Common.h"

void PixelFormatConversion::BgraToGray(const BitmapData* bitmap, uint8_t* destScan0)
{
    const size_t width = static_cast<size_t>(bitmap->width);
    const size_t height = static_cast<size_t>(bitmap->height);
    const size_t srcStride = static_cast<size_t>(bitmap->stride);
    const uint8_t* srcScan0 = bitmap->scan0;

    const size_t destStride = width;

    for (size_t y = 0; y < height; y++)
    {
        const ColorBgra* src = reinterpret_cast<const ColorBgra*>(srcScan0 + (y * srcStride));
        uint8_t* dest = destScan0 + (y * destStride);

        for (size_t x = 0; x < width; x++)
        {
            // For gray we only need to take one color channel.

            *dest = src->b;

            src++;
            dest++;
        }
    }
}

void PixelFormatConversion::BgraToGrayAlpha(const BitmapData* bitmap, uint8_t* destScan0)
{
    const size_t width = static_cast<size_t>(bitmap->width);
    const size_t height = static_cast<size_t>(bitmap->height);
    const size_t srcStride = static_cast<size_t>(bitmap->stride);
    const uint8_t* srcScan0 = bitmap->scan0;

    const size_t destStride = width * 2;

    for (size_t y = 0; y < height; y++)
    {
        const ColorBgra* src = reinterpret_cast<const ColorBgra*>(srcScan0 + (y * srcStride));
        uint8_t* dest = destScan0 + (y * destStride);

        for (size_t x = 0; x < width; x++)
        {
            // For gray we only need to take one color channel.

            dest[0] = src->b;
            dest[1] = src->a;

            src++;
            dest += 2;
        }
    }
}

void PixelFormatConversion::BgraToRgb(const BitmapData* bitmap, uint8_t* destScan0)
{
    const size_t width = static_cast<size_t>(bitmap->width);
    const size_t height = static_cast<size_t>(bitmap->height);
    const size_t srcStride = static_cast<size_t>(bitmap->stride);
    const uint8_t* srcScan0 = bitmap->scan0;

    const size_t destStride = width * 3;

    for (size_t y = 0; y < height; y++)
    {
        const ColorBgra* src = reinterpret_cast<const ColorBgra*>(srcScan0 + (y * srcStride));
        uint8_t* dest = destScan0 + (y * destStride);

        for (size_t x = 0; x < width; x++)
        {
            dest[0] = src->r;
            dest[1] = src->g;
            dest[2] = src->b;

            src++;
            dest += 3;
        }
    }
}

void PixelFormatConversion::BgraToRgba(const BitmapData* bitmap, uint8_t* destScan0)
{
    const size_t width = static_cast<size_t>(bitmap->width);
    const size_t height = static_cast<size_t>(bitmap->height);
    const size_t srcStride = static_cast<size_t>(bitmap->stride);
    const uint8_t* srcScan0 = bitmap->scan0;

    const size_t destStride = width * 4;

    for (size_t y = 0; y < height; y++)
    {
        const uint32_t* bgra = reinterpret_cast<const uint32_t*>(srcScan0 + (y * srcStride));
        uint32_t* rgba = reinterpret_cast<uint32_t*>(destScan0 + (y * destStride));

        for (size_t x = 0; x < width; x++)
        {
            uint32_t xyzw = *bgra;

            *rgba = ((xyzw & 0x000000ff) << 16) // Move x to the z position.
                | ((xyzw & 0x00ff0000) >> 16)   // Move z to the x position.
                | (xyzw & 0xff00ff00);          // Keep y and w in the same positions.

            bgra++;
            rgba++;
        }
    }
}
