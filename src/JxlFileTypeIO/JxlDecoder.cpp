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

#include "JxlDecoder.h"
#include "jxl/decode_cxx.h"
#include "jxl/resizable_parallel_runner_cxx.h"
#include <algorithm>
#include <stdexcept>
#include <vector>

namespace
{
    struct ImageOutState
    {
        BitmapData outLayerData;
        uint32_t numberOfChannels;
    };

    void ImageOutCallback(void* opaque, size_t x, size_t y, size_t num_pixels, const void* pixels)
    {
        ImageOutState* state = static_cast<ImageOutState*>(opaque);

        uint8_t* destScan0 = state->outLayerData.scan0;
        const size_t destStride = static_cast<size_t>(state->outLayerData.stride);

        const uint32_t srcChannelCount = state->numberOfChannels;

        const uint8_t* src = static_cast<const uint8_t*>(pixels);
        ColorBgra* dest = reinterpret_cast<ColorBgra*>(destScan0 + (y * destStride) + (x * sizeof(ColorBgra)));

        for (size_t i = 0; i < num_pixels; i++)
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

    DecoderStatus SetMetadata(
        DecoderCallbacks* callbacks,
        MetadataType type,
        const uint8_t* data,
        size_t dataSize)
    {
        void* outBuffer = callbacks->createMetadataBuffer(type, dataSize);

        if (outBuffer)
        {
            memcpy_s(outBuffer, dataSize, data, dataSize);
        }
        else
        {
            return DecoderStatus::CreateMetadataBufferError;
        }

        return DecoderStatus::Ok;
    }
}

DecoderStatus DecoderReadImage(
    DecoderCallbacks* callbacks,
    const uint8_t* data,
    size_t dataSize,
    ErrorInfo* errorInfo)
{
    if (!callbacks || !data)
    {
        return DecoderStatus::NullParameter;
    }

    try
    {
        auto runner = JxlResizableParallelRunnerMake(nullptr);

        auto dec = JxlDecoderMake(nullptr);

        if (JxlDecoderSubscribeEvents(
            dec.get(),
            JXL_DEC_BASIC_INFO |
            JXL_DEC_COLOR_ENCODING |
            JXL_DEC_FULL_IMAGE |
            JXL_DEC_BOX) != JXL_DEC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlDecoderSubscribeEvents failed.");
            return DecoderStatus::DecodeError;
        }

        if (JxlDecoderSetParallelRunner(
            dec.get(),
            JxlResizableParallelRunner,
            runner.get()) != JXL_DEC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlDecoderSetParallelRunner failed.");
            return DecoderStatus::DecodeError;
        }

        bool decompressBox = true;

        if (JxlDecoderSetDecompressBoxes(dec.get(), JXL_TRUE) != JXL_DEC_SUCCESS)
        {
            decompressBox = false;
        }

        if (JxlDecoderSetUnpremultiplyAlpha(dec.get(), JXL_TRUE) != JXL_DEC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlDecoderSetUnpremultiplyAlpha failed.");
            return DecoderStatus::DecodeError;
        }

        JxlDecoderSetInput(dec.get(), data, dataSize);
        JxlDecoderCloseInput(dec.get());

        bool firstFrameDecoded = false;
        JxlBasicInfo basicInfo{};
        JxlPixelFormat format{ 4, JXL_TYPE_UINT8, JXL_NATIVE_ENDIAN, 0 };
        ImageOutState imageOutState{};

        std::vector<uint8_t> boxMetadataBuffer;
        size_t boxMetadataBufferOffset = 0;
        constexpr size_t boxMetadataChunkSize = 65536;
        bool foundExifBox = false;
        bool readingExifBox = false;
        bool readingXmpBox = false;

        while (true)
        {
            JxlDecoderStatus status = JxlDecoderProcessInput(dec.get());

            if (status == JXL_DEC_ERROR)
            {
                SetErrorMessage(errorInfo, "JxlDecoderProcessInput failed.");
                return DecoderStatus::DecodeError;
            }
            else if (status == JXL_DEC_BOX)
            {
                if (readingExifBox)
                {
                    readingExifBox = false;

                    size_t remaining = JxlDecoderReleaseBoxBuffer(dec.get());

                    DecoderStatus error = SetMetadata(
                        callbacks,
                        MetadataType::Exif,
                        boxMetadataBuffer.data(),
                        boxMetadataBuffer.size() - remaining);

                    if (error != DecoderStatus::Ok)
                    {
                        return error;
                    }
                }
                else if (readingXmpBox)
                {
                    readingXmpBox = false;

                    size_t remaining = JxlDecoderReleaseBoxBuffer(dec.get());

                    DecoderStatus error = SetMetadata(
                        callbacks,
                        MetadataType::Xmp,
                        boxMetadataBuffer.data(),
                        boxMetadataBuffer.size() - remaining);

                    if (error != DecoderStatus::Ok)
                    {
                        return error;
                    }
                }

                JxlBoxType type;

                if (JxlDecoderGetBoxType(dec.get(), type, decompressBox) != JXL_DEC_SUCCESS)
                {
                    SetErrorMessage(errorInfo, "JxlDecoderGetBoxType failed.");
                    return DecoderStatus::DecodeError;
                }

                if (memcmp(type, "Exif", 4) == 0)
                {
                    if (!foundExifBox)
                    {
                        foundExifBox = true;
                        readingExifBox = true;

                        if (boxMetadataBuffer.size() < boxMetadataChunkSize)
                        {
                            boxMetadataBuffer.resize(boxMetadataChunkSize);
                        }
                        boxMetadataBufferOffset = 0;

                        if (JxlDecoderSetBoxBuffer(
                            dec.get(),
                            boxMetadataBuffer.data(),
                            boxMetadataBuffer.size()) != JXL_DEC_SUCCESS)
                        {
                            SetErrorMessage(errorInfo, "JxlDecoderSetBoxBuffer failed.");
                            return DecoderStatus::DecodeError;
                        }
                    }
                }
                else if (memcmp(type, "xml ", 4) == 0)
                {
                    readingXmpBox = true;

                    if (boxMetadataBuffer.size() < boxMetadataChunkSize)
                    {
                        boxMetadataBuffer.resize(boxMetadataChunkSize);
                    }
                    boxMetadataBufferOffset = 0;

                    if (JxlDecoderSetBoxBuffer(
                        dec.get(),
                        boxMetadataBuffer.data(),
                        boxMetadataBuffer.size()) != JXL_DEC_SUCCESS)
                    {
                        SetErrorMessage(errorInfo, "JxlDecoderSetBoxBuffer failed.");
                        return DecoderStatus::DecodeError;
                    }
                }
            }
            else if (status == JXL_DEC_BOX_NEED_MORE_OUTPUT)
            {
                size_t remaining = JxlDecoderReleaseBoxBuffer(dec.get());

                boxMetadataBufferOffset += boxMetadataChunkSize - remaining;
                boxMetadataBuffer.resize(boxMetadataBuffer.size() + boxMetadataChunkSize);

                if (JxlDecoderSetBoxBuffer(
                    dec.get(),
                    boxMetadataBuffer.data() + boxMetadataBufferOffset,
                    boxMetadataBuffer.size() - boxMetadataBufferOffset) != JXL_DEC_SUCCESS)
                {
                    SetErrorMessage(errorInfo, "JxlDecoderSetBoxBuffer failed.");
                    return DecoderStatus::DecodeError;
                }
            }
            else if (status == JXL_DEC_BASIC_INFO)
            {
                if (JxlDecoderGetBasicInfo(dec.get(), &basicInfo) != JXL_DEC_SUCCESS)
                {
                    SetErrorMessage(errorInfo, "JxlDecoderGetBasicInfo failed.");
                    return DecoderStatus::DecodeError;
                }

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

                format.num_channels = colorChannelCount + (hasAlphaChannel ? 1 : 0);
                imageOutState.numberOfChannels = format.num_channels;
            }
            else if (status == JXL_DEC_COLOR_ENCODING)
            {
                if (!basicInfo.uses_original_profile)
                {
                    // For XYB encoded images we tell libjxl to convert the output image to sRGB.
                    JxlColorEncoding colorEncoding{};
                    colorEncoding.color_space = JXL_COLOR_SPACE_RGB;
                    colorEncoding.white_point = JXL_WHITE_POINT_D65;
                    colorEncoding.primaries = JXL_PRIMARIES_SRGB;
                    colorEncoding.transfer_function = JXL_TRANSFER_FUNCTION_SRGB;
                    colorEncoding.rendering_intent = JXL_RENDERING_INTENT_PERCEPTUAL;

                    if (JxlDecoderSetPreferredColorProfile(dec.get(), &colorEncoding) != JXL_DEC_SUCCESS)
                    {
                        SetErrorMessage(errorInfo, "JxlDecoderSetPreferredColorProfile failed.");
                        return DecoderStatus::DecodeError;
                    }
                }

                size_t iccProfileSize = 0;

                if (JxlDecoderGetICCProfileSize(
                    dec.get(),
                    JXL_COLOR_PROFILE_TARGET_DATA,
                    &iccProfileSize) == JXL_DEC_SUCCESS)
                {
                    if (iccProfileSize > 0)
                    {
                        uint8_t* buffer = callbacks->createMetadataBuffer(MetadataType::IccProfile, iccProfileSize);

                        if (buffer)
                        {
                            if (JxlDecoderGetColorAsICCProfile(
                                dec.get(),
                                JXL_COLOR_PROFILE_TARGET_DATA,
                                buffer,
                                iccProfileSize) != JXL_DEC_SUCCESS)
                            {
                                return DecoderStatus::MetadataError;
                            }
                        }
                        else
                        {
                            return DecoderStatus::CreateMetadataBufferError;
                        }
                    }
                }
            }
            else if (status == JXL_DEC_NEED_IMAGE_OUT_BUFFER)
            {
                if (firstFrameDecoded)
                {
                    return DecoderStatus::HasMultipleFrames;
                }

                JxlFrameHeader frameHeader{};

                if (JxlDecoderGetFrameHeader(dec.get(), &frameHeader) != JXL_DEC_SUCCESS)
                {
                    SetErrorMessage(errorInfo, "JxlDecoderGetFrameHeader failed.");
                    return DecoderStatus::DecodeError;
                }

                std::vector<char> layerNameBuffer;
                char* layerNamePtr = nullptr;
                uint32_t layerNameLengthInBytes = 0;

                if (frameHeader.name_length > 0)
                {
                    layerNameBuffer.resize(static_cast<size_t>(frameHeader.name_length) + 1);

                    if (JxlDecoderGetFrameName(
                        dec.get(),
                        layerNameBuffer.data(),
                        layerNameBuffer.size()) == JXL_DEC_SUCCESS)
                    {
                        layerNamePtr = layerNameBuffer.data();
                        layerNameLengthInBytes = frameHeader.name_length;
                    }
                }

                if (!callbacks->createLayer(
                    static_cast<int32_t>(basicInfo.xsize),
                    static_cast<int32_t>(basicInfo.ysize),
                    layerNamePtr,
                    layerNameLengthInBytes,
                    &imageOutState.outLayerData))
                {
                    return DecoderStatus::CreateLayerError;
                }

                if (JxlDecoderSetImageOutCallback(
                    dec.get(),
                    &format,
                    &ImageOutCallback,
                    &imageOutState) != JXL_DEC_SUCCESS)
                {
                    SetErrorMessage(errorInfo, "JxlDecoderSetImageOutCallback failed.");
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
            else if (status == JXL_DEC_NEED_MORE_INPUT)
            {
                SetErrorMessage(errorInfo, "JxlDecoderProcessInput needs more input, but it already received the entire image.");
                return DecoderStatus::DecodeError;
            }
            else if (status == JXL_DEC_SUCCESS)
            {
                if (readingExifBox)
                {
                    readingExifBox = false;

                    size_t remaining = JxlDecoderReleaseBoxBuffer(dec.get());

                    DecoderStatus error = SetMetadata(
                        callbacks,
                        MetadataType::Exif,
                        boxMetadataBuffer.data(),
                        boxMetadataBuffer.size() - remaining);

                    if (error != DecoderStatus::Ok)
                    {
                        return error;
                    }
                }
                else if (readingXmpBox)
                {
                    readingXmpBox = false;

                    size_t remaining = JxlDecoderReleaseBoxBuffer(dec.get());

                    DecoderStatus error = SetMetadata(
                        callbacks,
                        MetadataType::Xmp,
                        boxMetadataBuffer.data(),
                        boxMetadataBuffer.size() - remaining);

                    if (error != DecoderStatus::Ok)
                    {
                        return error;
                    }
                }
                else
                {
                    break;
                }
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


