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

#include "ChunkedInputFrameSource.h"

ChunkedInputFrameSource::ChunkedInputFrameSource(const BitmapData* layerData, JxlPixelFormat colorPixelFormat)
    : layerData(layerData),
      colorChannelFormat(colorPixelFormat),
      extraChannelFormat{1, JXL_TYPE_UINT8, JXL_NATIVE_ENDIAN}
{
}

JxlChunkedFrameInputSource ChunkedInputFrameSource::ToJxlChunkedFrameInputSource()
{
    JxlChunkedFrameInputSource source{};
    source.opaque = this;
    source.get_color_channels_pixel_format = GetColorChannelsPixelFormatStatic;
    source.get_color_channel_data_at = GetColorChannelDataAtStatic;
    source.get_extra_channel_pixel_format = GetExtraChannelsPixelFormatStatic;
    source.get_extra_channel_data_at = GetExtraChannelDataAtStatic;
    source.release_buffer = ReleaseBufferStatic;

    return source;
}

void ChunkedInputFrameSource::GetColorChannelsPixelFormatStatic(void* opaque, JxlPixelFormat* pixelFormat)
{
    static_cast<ChunkedInputFrameSource*>(opaque)->GetColorChannelsPixelFormat(pixelFormat);
}

const void* ChunkedInputFrameSource::GetColorChannelDataAtStatic(
    void* opaque,
    size_t xpos,
    size_t ypos,
    size_t xsize,
    size_t ysize,
    size_t* row_offset)
{
    return static_cast<ChunkedInputFrameSource*>(opaque)->GetColorChannelDataAt(
        xpos,
        ypos,
        xsize,
        ysize,
        row_offset);
}

void ChunkedInputFrameSource::GetExtraChannelsPixelFormatStatic(void* opaque, size_t ecIndex, JxlPixelFormat* pixelFormat)
{
    static_cast<ChunkedInputFrameSource*>(opaque)->GetExtraChannelsPixelFormat(ecIndex, pixelFormat);
}

const void* ChunkedInputFrameSource::GetExtraChannelDataAtStatic(
    void* opaque,
    size_t ecIndex,
    size_t xpos,
    size_t ypos,
    size_t xsize,
    size_t ysize,
    size_t* row_offset)
{
    return static_cast<ChunkedInputFrameSource*>(opaque)->GetExtraChannelDataAt(
        ecIndex,
        xpos,
        ypos,
        xsize,
        ysize,
        row_offset);
}

void ChunkedInputFrameSource::ReleaseBufferStatic(void* opaque, const void* buffer)
{
    static_cast<ChunkedInputFrameSource*>(opaque)->ReleaseBuffer(buffer);
}

void ChunkedInputFrameSource::GetColorChannelsPixelFormat(JxlPixelFormat* pixelFormat) const
{
    *pixelFormat = colorChannelFormat;
}

const void* ChunkedInputFrameSource::GetColorChannelDataAt(
    size_t xpos,
    size_t ypos,
    size_t xsize,
    size_t ysize,
    size_t* row_offset)
{
    const size_t left = xpos;
    const size_t top = ypos;
    const size_t right = left + xsize;
    const size_t bottom = top + ysize;

    const size_t destChannelCount = colorChannelFormat.num_channels;
    const size_t destStride = xsize * destChannelCount;

    ResizeBuffer(destStride, ysize);

    const uint8_t* srcScan0 = layerData->scan0;
    const size_t srcStride = layerData->stride;
    uint8_t* destScan0 = buffer.data();


    for (size_t y = top; y < bottom; y++)
    {
        const ColorBgra* src = reinterpret_cast<const ColorBgra*>(srcScan0 + (y * srcStride) + (left * sizeof(ColorBgra)));
        uint8_t* dest = destScan0 + (y * destStride);

        for (size_t x = left; x < right; x++)
        {
            switch (destChannelCount)
            {
            case 1: // Gray
                dest[0] = src->r;
                break;
            case 2: // Gray + Alpha
                dest[0] = src->r;
                dest[1] = src->a;
                break;
            case 3: // RGB
                dest[0] = src->r;
                dest[1] = src->g;
                dest[2] = src->b;
                break;
            case 4: // RGBA
                dest[0] = src->r;
                dest[1] = src->g;
                dest[2] = src->b;
                dest[3] = src->a;
                break;
            }

            src++;
            dest += destChannelCount;
        }
    }

    *row_offset = destStride;
    return destScan0;
}

void ChunkedInputFrameSource::GetExtraChannelsPixelFormat(size_t ecIndex, JxlPixelFormat* pixelFormat) const
{
    *pixelFormat = extraChannelFormat;
}

const void* ChunkedInputFrameSource::GetExtraChannelDataAt(
    size_t ecIndex,
    size_t xpos,
    size_t ypos,
    size_t xsize,
    size_t ysize,
    size_t* row_offset)
{
    const size_t left = xpos;
    const size_t top = ypos;
    const size_t right = left + xsize;
    const size_t bottom = top + ysize;
    const size_t destStride = xsize;

    ResizeBuffer(destStride, ysize);

    const uint8_t* srcScan0 = layerData->scan0;
    const size_t srcStride = layerData->stride;
    uint8_t* destScan0 = buffer.data();

    for (size_t y = top; y < bottom; y++)
    {
        const ColorBgra* src = reinterpret_cast<const ColorBgra*>(srcScan0 + (y * srcStride) + (left * sizeof(ColorBgra)));
        uint8_t* dest = destScan0 + (y * destStride);

        for (size_t x = left; x < right; x++)
        {
            *dest = src->a;

            src++;
            dest++;
        }
    }

    *row_offset = destStride;
    return destScan0;
}

void ChunkedInputFrameSource::ReleaseBuffer(const void* buffer)
{
    // No-op.
}

void ChunkedInputFrameSource::ResizeBuffer(size_t stride, size_t height)
{
    size_t length = stride * height;

    if (buffer.size() < length)
    {
        buffer.resize(length);
    }
}

