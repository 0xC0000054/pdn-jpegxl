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
    enum class SetProfileFromEncodingStatus
    {
        Ok,
        Error,
        UnsupportedColorEncoding
    };

    SetProfileFromEncodingStatus SetKnownColorProfileFromEncoding(DecoderCallbacks* callbacks, KnownColorProfile profile)
    {
        return callbacks->setKnownColorProfile(profile)
            ? SetProfileFromEncodingStatus::Ok
            : SetProfileFromEncodingStatus::Error;
    }

    SetProfileFromEncodingStatus SetProfileFromColorEncoding(
        DecoderCallbacks* callbacks,
        const JxlColorEncoding& colorEncoding)
    {
        if (colorEncoding.white_point == JXL_WHITE_POINT_D65)
        {
            if (colorEncoding.transfer_function == JXL_TRANSFER_FUNCTION_LINEAR)
            {
                if (colorEncoding.color_space == JXL_COLOR_SPACE_RGB)
                {
                    if (colorEncoding.primaries == JXL_PRIMARIES_SRGB)
                    {
                        return SetKnownColorProfileFromEncoding(callbacks, KnownColorProfile::LinearSrgb);
                    }
                }
                else if (colorEncoding.color_space == JXL_COLOR_SPACE_GRAY)
                {
                    return SetKnownColorProfileFromEncoding(callbacks, KnownColorProfile::LinearGray);
                }
            }
            else if (colorEncoding.transfer_function == JXL_TRANSFER_FUNCTION_SRGB)
            {
                if (colorEncoding.color_space == JXL_COLOR_SPACE_RGB)
                {
                    if (colorEncoding.primaries == JXL_PRIMARIES_SRGB)
                    {
                        return SetKnownColorProfileFromEncoding(callbacks, KnownColorProfile::Srgb);
                    }
                }
                else if (colorEncoding.color_space == JXL_COLOR_SPACE_GRAY)
                {
                    return SetKnownColorProfileFromEncoding(callbacks, KnownColorProfile::GraySrgbTRC);
                }
            }
        }

        return SetProfileFromEncodingStatus::UnsupportedColorEncoding;
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
            JXL_DEC_FRAME |
            JXL_DEC_FULL_IMAGE |
            JXL_DEC_BOX |
            JXL_DEC_BOX_COMPLETE) != JXL_DEC_SUCCESS)
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
        std::vector<uint8_t> imageOutBuffer;
        std::vector<uint8_t> iccProfileBuffer;

        std::vector<uint8_t> boxMetadataBuffer;
        size_t boxMetadataBufferOffset = 0;
        constexpr size_t boxMetadataChunkSize = 65536;
        bool foundExifBox = false;
        bool readingExifBox = false;
        bool readingXmpBox = false;
        DecoderImageFormat decoderImageFormat{};
        std::vector<char> layerNameBuffer;

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
            else if (status == JXL_DEC_BOX_COMPLETE)
            {
                if (readingExifBox)
                {
                    readingExifBox = false;

                    size_t remaining = JxlDecoderReleaseBoxBuffer(dec.get());

                    if (!callbacks->setExif(
                        boxMetadataBuffer.data(),
                        boxMetadataBuffer.size() - remaining))
                    {
                        return DecoderStatus::CreateMetadataError;
                    }
                }
                else if (readingXmpBox)
                {
                    readingXmpBox = false;

                    size_t remaining = JxlDecoderReleaseBoxBuffer(dec.get());

                    if (!callbacks->setXmp(
                        boxMetadataBuffer.data(),
                        boxMetadataBuffer.size() - remaining))
                    {
                        return DecoderStatus::CreateMetadataError;
                    }
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
                const bool hasTransparency = basicInfo.alpha_bits != 0;

                if (width > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()) ||
                    height > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()))
                {
                    return DecoderStatus::ImageDimensionExceedsInt32;
                }

                if (colorChannelCount != 1 && colorChannelCount != 3 ||
                    extraChannelCount > 1 ||
                    extraChannelCount == 1 && !hasTransparency)
                {
                    // The format is not gray or RGB with an optional alpha channel.
                    return DecoderStatus::UnsupportedChannelFormat;
                }

                format.num_channels = colorChannelCount + (hasTransparency ? 1 : 0);
                decoderImageFormat = colorChannelCount == 1 ? DecoderImageFormat::Gray : DecoderImageFormat::Rgb;

                callbacks->setBasicInfo(width, height, decoderImageFormat, hasTransparency);
            }
            else if (status == JXL_DEC_COLOR_ENCODING)
            {
                if (!basicInfo.uses_original_profile)
                {
                    // For XYB encoded images we tell libjxl to convert the output image to sRGB or sGray.
                    JxlColorEncoding colorEncoding{};
                    if (decoderImageFormat == DecoderImageFormat::Gray)
                    {
                        colorEncoding.color_space = JXL_COLOR_SPACE_GRAY;
                    }
                    else
                    {
                        colorEncoding.color_space = JXL_COLOR_SPACE_RGB;
                    }
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

                SetProfileFromEncodingStatus encodedProfileStatus = SetProfileFromEncodingStatus::UnsupportedColorEncoding;

                JxlColorEncoding colorEncoding{};

                if (JxlDecoderGetColorAsEncodedProfile(
                    dec.get(),
                    JXL_COLOR_PROFILE_TARGET_DATA,
                    &colorEncoding) == JXL_DEC_SUCCESS)
                {
                    encodedProfileStatus = SetProfileFromColorEncoding(callbacks, colorEncoding);

                    if (encodedProfileStatus == SetProfileFromEncodingStatus::Error)
                    {
                        return DecoderStatus::CreateMetadataError;
                    }
                }

                if (encodedProfileStatus == SetProfileFromEncodingStatus::UnsupportedColorEncoding)
                {
                    size_t iccProfileSize = 0;

                    if (JxlDecoderGetICCProfileSize(
                        dec.get(),
                        JXL_COLOR_PROFILE_TARGET_DATA,
                        &iccProfileSize) == JXL_DEC_SUCCESS)
                    {
                        if (iccProfileSize > 0)
                        {
                            iccProfileBuffer.resize(iccProfileSize);

                            if (JxlDecoderGetColorAsICCProfile(
                                dec.get(),
                                JXL_COLOR_PROFILE_TARGET_DATA,
                                iccProfileBuffer.data(),
                                iccProfileSize) != JXL_DEC_SUCCESS)
                            {
                                return DecoderStatus::MetadataError;
                            }

                            if (!callbacks->setIccProfile(
                                iccProfileBuffer.data(),
                                iccProfileBuffer.size()))
                            {
                                return DecoderStatus::CreateMetadataError;
                            }
                        }
                    }
                }
            }
            else if (status == JXL_DEC_FRAME)
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

                char* layerNamePtr = nullptr;
                uint32_t layerNameLengthInBytes = 0;

                if (frameHeader.name_length > 0)
                {
                    layerNameBuffer.resize(static_cast<size_t>(frameHeader.name_length) + 1);

                    if (JxlDecoderGetFrameName(
                        dec.get(),
                        layerNameBuffer.data(),
                        layerNameBuffer.size()) != JXL_DEC_SUCCESS)
                    {
                        layerNameBuffer.clear();
                    }
                }
                else
                {
                    layerNameBuffer.clear();
                }
            }
            else if (status == JXL_DEC_NEED_IMAGE_OUT_BUFFER)
            {
                if (imageOutBuffer.size() == 0)
                {
                    imageOutBuffer.resize(static_cast<size_t>(basicInfo.xsize) * basicInfo.ysize * format.num_channels);
                }

                if (JxlDecoderSetImageOutBuffer(
                    dec.get(),
                    &format,
                    imageOutBuffer.data(),
                    imageOutBuffer.size()) != JXL_DEC_SUCCESS)
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

                    char* layerNamePtr = nullptr;
                    size_t layerNameLengthInBytes = 0;

                    if (layerNameBuffer.size() > 0)
                    {
                        layerNamePtr = layerNameBuffer.data();
                        layerNameLengthInBytes = layerNameBuffer.size();
                    }

                    if (!callbacks->setLayerData(
                        imageOutBuffer.data(),
                        layerNamePtr,
                        layerNameLengthInBytes))
                    {
                        return DecoderStatus::CreateLayerError;
                    }
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


