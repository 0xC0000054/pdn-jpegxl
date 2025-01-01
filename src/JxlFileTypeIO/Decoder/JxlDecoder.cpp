////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024, 2025 Nicholas Hayes
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
                    else if (colorEncoding.primaries == JXL_PRIMARIES_P3)
                    {
                        return SetKnownColorProfileFromEncoding(callbacks, KnownColorProfile::DisplayP3);
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

    bool ExtraChannelsAreSupported(
        const JxlDecoder* dec,
        const JxlBasicInfo& info,
        size_t& cmykBlackChannelIndex)
    {
        cmykBlackChannelIndex = std::numeric_limits<size_t>::max();

        const bool hasTransparency = info.alpha_bits != 0;
        const uint32_t extraChannelCount = info.num_extra_channels;
        bool foundFirstAlphaChannel = false;

        for (size_t i = 0; i < extraChannelCount; i++)
        {
            JxlExtraChannelInfo extraChannelInfo{};

            if (JxlDecoderGetExtraChannelInfo(dec, i, &extraChannelInfo) != JXL_DEC_SUCCESS)
            {
                break;
            }

            if (extraChannelInfo.type == JXL_CHANNEL_BLACK)
            {
                if (cmykBlackChannelIndex == std::numeric_limits<size_t>::max())
                {
                    cmykBlackChannelIndex = i;
                }
                else
                {
                    // Duplicate channel.
                    return false;
                }
            }
            else if (extraChannelInfo.type == JXL_CHANNEL_ALPHA)
            {
                if (hasTransparency && !foundFirstAlphaChannel)
                {
                    foundFirstAlphaChannel = true;
                }
                else
                {
                    // Auxiliary alpha channel.
                    return false;
                }
            }
        }

        return true;
    }

    bool SetCmykImageData(
        DecoderCallbacks* callbacks,
        size_t width,
        size_t height,
        bool hasTransparency,
        const std::vector<uint8_t>& cmya,
        const std::vector<uint8_t>& key,
        char* layerName,
        size_t layerNameLengthInBytes)
    {
        const size_t transparencyChannelCount = hasTransparency ? 1 : 0;
        const size_t cmyaChannelCount = 3 + transparencyChannelCount;
        const size_t totalChannelCount = 4 + transparencyChannelCount;

        std::vector<uint8_t> output(width * height * totalChannelCount);

        uint8_t* const outputScan0 = output.data();
        const uint8_t* cmyaScan0 = cmya.data();
        const uint8_t* keyScan0 = key.data();
        const size_t outputStride = width * totalChannelCount;
        const size_t cmyaStride = width * cmyaChannelCount;
        const size_t keyStride = width;

        for (size_t y = 0; y < height; y++)
        {
            const uint8_t* cmya = cmyaScan0 + (y * cmyaStride);
            const uint8_t* key = keyScan0 + (y * keyStride);
            uint8_t* dst = outputScan0 + (y * outputStride);

            for (size_t x = 0; x < width; x++)
            {
                // Jpeg XL stores CMYK images with 0 representing black/full ink.
                // https://discord.com/channels/794206087879852103/804324493420920833/1317698217273458738
                //
                // "The K channel of a CMYK image. If present, a CMYK ICC profile is also present,
                // and the RGB samples are to be interpreted as CMY, where 0 denotes full ink."
                //
                // WIC requires that 0 is white/no ink, so we have to invert the CMYK data.

                dst[0] = static_cast<uint8_t>(0xff - cmya[0]); // C
                dst[1] = static_cast<uint8_t>(0xff - cmya[1]); // M
                dst[2] = static_cast<uint8_t>(0xff - cmya[2]); // Y
                dst[3] = static_cast<uint8_t>(0xff - key[0]);  // K

                if (hasTransparency)
                {
                    dst[4] = cmya[3]; // A
                }

                dst += totalChannelCount;
                cmya += cmyaChannelCount;
                key++;
            }
        }

        return callbacks->setLayerData(outputScan0, layerName, layerNameLengthInBytes);
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
        size_t cmykBlackChannelIndex = std::numeric_limits<size_t>::max();
        std::vector<uint8_t> cmykBlackChannelBuffer;

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

                if (colorChannelCount != 1 && colorChannelCount != 3
                    || !ExtraChannelsAreSupported(dec.get(), basicInfo, cmykBlackChannelIndex))
                {
                    // The format is not CMYK, Gray, or RGB with optional transparency.
                    return DecoderStatus::UnsupportedChannelFormat;
                }

                format.num_channels = colorChannelCount + (hasTransparency ? 1 : 0);

                if (colorChannelCount == 1)
                {
                    decoderImageFormat = DecoderImageFormat::Gray;
                }
                else if (cmykBlackChannelIndex != std::numeric_limits<size_t>::max())
                {
                    decoderImageFormat = DecoderImageFormat::Cmyk;
                }
                else
                {
                    decoderImageFormat = DecoderImageFormat::Rgb;
                }

                callbacks->setBasicInfo(width, height, decoderImageFormat, hasTransparency);
            }
            else if (status == JXL_DEC_COLOR_ENCODING)
            {
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
                    SetErrorMessage(errorInfo, "JxlDecoderSetImageOutBuffer failed.");
                    return DecoderStatus::DecodeError;
                }

                if (decoderImageFormat == DecoderImageFormat::Cmyk)
                {
                    if (cmykBlackChannelBuffer.size() == 0)
                    {
                        cmykBlackChannelBuffer.resize(static_cast<size_t>(basicInfo.xsize) * basicInfo.ysize);
                    }

                    if (JxlDecoderSetExtraChannelBuffer(
                        dec.get(),
                        &format,
                        cmykBlackChannelBuffer.data(),
                        cmykBlackChannelBuffer.size(),
                        static_cast<uint32_t>(cmykBlackChannelIndex)) != JXL_DEC_SUCCESS)
                    {
                        SetErrorMessage(errorInfo, "JxlDecoderSetExtraChannelBuffer failed.");
                        return DecoderStatus::DecodeError;
                    }
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

                    if (decoderImageFormat == DecoderImageFormat::Cmyk)
                    {
                        if (!SetCmykImageData(
                            callbacks,
                            basicInfo.xsize,
                            basicInfo.ysize,
                            basicInfo.alpha_bits != 0,
                            imageOutBuffer,
                            cmykBlackChannelBuffer,
                            layerNamePtr,
                            layerNameLengthInBytes))
                        {
                            return DecoderStatus::CreateLayerError;
                        }
                    }
                    else
                    {
                        if (!callbacks->setLayerData(
                            imageOutBuffer.data(),
                            layerNamePtr,
                            layerNameLengthInBytes))
                        {
                            return DecoderStatus::CreateLayerError;
                        }
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


