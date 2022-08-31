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

#include "JxlDecoder.h"
#include "jxl/decode_cxx.h"
#include "jxl/resizable_parallel_runner_cxx.h"
#include <algorithm>
#include <stdexcept>
#include <vector>

struct DecoderContext
{
    JxlDecoderPtr dec;
    std::vector<uint8_t> pixelData;
    std::vector<uint8_t> exif;
    std::vector<uint8_t> xmp;
    JxlBasicInfo basicInfo;
    JxlPixelFormat format;

    DecoderContext()
        : dec(JxlDecoderCreate(nullptr)), pixelData(), exif(), xmp(), basicInfo{},
        format{4, JXL_TYPE_UINT8, JXL_NATIVE_ENDIAN, 0 }
    {
        if (!dec)
        {
            // Failed to create the decoder instance.
            throw std::bad_alloc();
        }
    }
};

DecoderContext* CreateDecoderContext()
{
    try
    {
        return new DecoderContext();
    }
    catch (...)
    {
        return nullptr;
    }
}

void DestroyDecoderContext(DecoderContext* context)
{
    if (context)
    {
        delete context;
        context = nullptr;
    }
}

DecoderStatus DecoderParseFile(
    DecoderContext* context,
    const uint8_t* data,
    size_t dataSize,
    DecoderImageInfo* imageInfo,
    ErrorInfo* errorInfo)
{
    if (!context || !data || !imageInfo)
    {
        return DecoderStatus::NullParameter;
    }

    try
    {
        auto runner = JxlResizableParallelRunnerMake(nullptr);

        if (JxlDecoderSubscribeEvents(
            context->dec.get(),
            JXL_DEC_BASIC_INFO |
            JXL_DEC_COLOR_ENCODING |
            JXL_DEC_FULL_IMAGE) != JXL_DEC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlDecoderSubscribeEvents failed.");
            return DecoderStatus::DecodeError;
        }

        if (JxlDecoderSetParallelRunner(
            context->dec.get(),
            JxlResizableParallelRunner,
            runner.get()) != JXL_DEC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlDecoderSetParallelRunner failed.");
            return DecoderStatus::DecodeError;
        }

        JxlDecoderSetInput(context->dec.get(), data, dataSize);

        bool firstFrameDecoded = false;
        imageInfo->iccProfileSize = 0;

        // TODO: Implement EXIF and XMP reading when libjxl v0.7.0 is released.

        while (true)
        {
            JxlDecoderStatus status = JxlDecoderProcessInput(context->dec.get());

            if (status == JXL_DEC_ERROR)
            {
                SetErrorMessage(errorInfo, "JxlDecoderProcessInput failed.");
                return DecoderStatus::DecodeError;
            }
            else if (status == JXL_DEC_BASIC_INFO)
            {
                if (JxlDecoderGetBasicInfo(context->dec.get(), &context->basicInfo) != JXL_DEC_SUCCESS)
                {
                    SetErrorMessage(errorInfo, "JxlDecoderGetBasicInfo failed.");
                    return DecoderStatus::DecodeError;
                }

                const JxlBasicInfo& basicInfo = context->basicInfo;

                if (basicInfo.have_animation)
                {
                    return DecoderStatus::HasAnimation;
                }

                const uint32_t width = basicInfo.xsize;
                const uint32_t height = basicInfo.ysize;
                const uint32_t colorChannelCount = basicInfo.num_color_channels;
                const uint32_t extraChannelCount = basicInfo.num_extra_channels;
                const bool hasAlphaChannel = basicInfo.alpha_bits != 0;

                if (width > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()) ||
                    height > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()))
                {
                    return DecoderStatus::ImageDimensionExceedsInt32;
                }

                if (colorChannelCount != 1 && colorChannelCount != 3 ||
                    extraChannelCount > 1 ||
                    extraChannelCount == 1 && !hasAlphaChannel)
                {
                    // The format is not gray or RGB with an optional alpha channel.
                    return DecoderStatus::UnsupportedChannelFormat;
                }

                context->format.num_channels = colorChannelCount + (hasAlphaChannel ? 1 : 0);
                imageInfo->width = static_cast<int32_t>(width);
                imageInfo->height = static_cast<int32_t>(height);
            }
            else if (status == JXL_DEC_COLOR_ENCODING)
            {
                if (JxlDecoderGetICCProfileSize(
                    context->dec.get(),
                    &context->format,
                    JXL_COLOR_PROFILE_TARGET_DATA,
                    &imageInfo->iccProfileSize) != JXL_DEC_SUCCESS)
                {
                    imageInfo->iccProfileSize = 0;
                }
            }
            else if (status == JXL_DEC_NEED_IMAGE_OUT_BUFFER)
            {
                if (firstFrameDecoded)
                {
                    return DecoderStatus::HasMultipleFrames;
                }

                size_t requiredBufferSize = 0;

                if (JxlDecoderImageOutBufferSize(
                    context->dec.get(),
                    &context->format,
                    &requiredBufferSize) != JXL_DEC_SUCCESS)
                {
                    SetErrorMessage(errorInfo, "JxlDecoderImageOutBufferSize failed.");
                    return DecoderStatus::DecodeError;
                }

                size_t expectedBufferSize = static_cast<size_t>(imageInfo->width) * static_cast<size_t>(imageInfo->height) * static_cast<size_t>(context->format.num_channels);

                if (requiredBufferSize != expectedBufferSize)
                {
                    SetErrorMessageFormat(errorInfo,
                        "JxlDecoderImageOutBufferSize value (%Iu) does not match the expected buffer size (%Iu).",
                        requiredBufferSize,
                        expectedBufferSize);
                    return DecoderStatus::DecodeError;
                }

                context->pixelData.resize(expectedBufferSize);

                if (JxlDecoderSetImageOutBuffer(
                    context->dec.get(),
                    &context->format,
                    context->pixelData.data(),
                    context->pixelData.size()) != JXL_DEC_SUCCESS)
                {
                    SetErrorMessage(errorInfo, "JxlDecoderSetImageOutBuffer failed.");
                    return DecoderStatus::DecodeError;
                }
            }
            else if (status == JXL_DEC_FULL_IMAGE)
            {
                if (!firstFrameDecoded)
                {
                    firstFrameDecoded = true;
                }
                else
                {
                    return DecoderStatus::HasMultipleFrames;
                }
            }
            else if (status == JXL_DEC_SUCCESS)
            {
                break;
            }
        }
    }
    catch (const std::bad_alloc&)
    {
        return DecoderStatus::OutOfMemory;
    }
    catch (...)
    {
        return DecoderStatus::DecodeError;
    }

    return DecoderStatus::Ok;
}

DecoderStatus DecoderGetIccProfileData(
    DecoderContext* context,
    uint8_t* buffer,
    size_t bufferSize)
{
    if (!buffer)
    {
        return DecoderStatus::NullParameter;
    }

    return JxlDecoderGetColorAsICCProfile(
        context->dec.get(),
        &context->format,
        JXL_COLOR_PROFILE_TARGET_DATA,
        buffer,
        bufferSize) == JXL_DEC_SUCCESS ? DecoderStatus::Ok : DecoderStatus::MetadataError;
}

void DecoderCopyPixelsToSurface(DecoderContext* context, BitmapData* bitmap)
{
    const size_t width = static_cast<size_t>(bitmap->width);
    const size_t height = static_cast<size_t>(bitmap->height);
    const size_t destStride = static_cast<size_t>(bitmap->stride);
    uint8_t* destScan0 = bitmap->scan0;

    const uint8_t* srcScan0 = context->pixelData.data();
    const uint32_t srcChannelCount = context->format.num_channels;
    const size_t srcStride = width * static_cast<size_t>(srcChannelCount);

    for (size_t y = 0; y < height; y++)
    {
        const uint8_t* src = srcScan0 + (y * srcStride);
        ColorBgra* dest = reinterpret_cast<ColorBgra*>(destScan0 + (y * destStride));

        for (size_t x = 0; x < width; x++)
        {
            switch (srcChannelCount)
            {
            case 1: // Gray
                dest->r = dest->g = dest->b = src[0];
                dest->a = 255;
                break;
            case 2: // Gray + Alpha
                dest->r = dest->g = dest->b = src[0];
                dest->a = src[1];
                break;
            case 3: // RGB
                dest->r = src[0];
                dest->g = src[1];
                dest->b = src[2];
                dest->a = 255;
                break;
            case 4: // RGBA
                dest->r = src[0];
                dest->g = src[1];
                dest->b = src[2];
                dest->a = src[3];
                break;
            }

            src += srcChannelCount;
            dest++;
        }
    }
}

