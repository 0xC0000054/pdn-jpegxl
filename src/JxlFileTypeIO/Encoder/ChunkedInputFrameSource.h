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
#include "jxl/encode.h"
#include <vector>

class ChunkedInputFrameSource
{
public:
    ChunkedInputFrameSource(const BitmapData* layerData, JxlPixelFormat colorPixelFormat);

    JxlChunkedFrameInputSource ToJxlChunkedFrameInputSource();

private:
    static void GetColorChannelsPixelFormatStatic(void* opaque, JxlPixelFormat* pixelFormat);
    static const void* GetColorChannelDataAtStatic(
        void* opaque,
        size_t xpos,
        size_t ypos,
        size_t xsize,
        size_t ysize,
        size_t* row_offset);
    static void GetExtraChannelsPixelFormatStatic(
        void* opaque,
        size_t ecIndex,
        JxlPixelFormat* pixelFormat);
    static const void* GetExtraChannelDataAtStatic(
        void* opaque,
        size_t ecIndex,
        size_t xpos,
        size_t ypos,
        size_t xsize,
        size_t ysize,
        size_t* row_offset);
    static void ReleaseBufferStatic(void* opaque, const void* buffer);

    void GetColorChannelsPixelFormat(JxlPixelFormat* pixelFormat) const;
    const void* GetColorChannelDataAt(
        size_t xpos,
        size_t ypos,
        size_t xsize,
        size_t ysize,
        size_t* row_offset);
    void GetExtraChannelsPixelFormat(size_t ecIndex, JxlPixelFormat* pixelFormat) const;
    const void* GetExtraChannelDataAt(
        size_t ecIndex,
        size_t xpos,
        size_t ypos,
        size_t xsize,
        size_t ysize,
        size_t* row_offset);
    void ReleaseBuffer(const void* buffer);
    void ResizeBuffer(size_t stride, size_t height);

    const BitmapData* layerData;
    std::vector<uint8_t> buffer;
    JxlPixelFormat colorChannelFormat;
    JxlPixelFormat extraChannelFormat;
};

