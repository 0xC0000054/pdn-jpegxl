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

#include "JxlEncoder.h"
#include <jxl/encode_cxx.h>
#include <array>
#include <stdexcept>
#include <vector>
#include <jxl/resizable_parallel_runner.h>
#include <jxl/resizable_parallel_runner_cxx.h>

namespace
{
    enum class OutputPixelFormat
    {
        Gray,
        GrayAlpha,
        Rgb,
        Rgba
    };

    OutputPixelFormat GetOutputPixelFormat(const BitmapData* bitmap)
    {
        bool isGray = true;
        bool hasTransparency = false;

        const size_t width = static_cast<size_t>(bitmap->width);
        const size_t height = static_cast<size_t>(bitmap->height);
        const size_t stride = static_cast<size_t>(bitmap->stride);
        const uint8_t* scan0 = bitmap->scan0;

        for (size_t y = 0; y < height; y++)
        {
            const ColorBgra* ptr = reinterpret_cast<const ColorBgra*>(scan0 + (y * stride));

            for (size_t x = 0; x < width; x++)
            {
                if (!(ptr->r == ptr->g && ptr->g == ptr->b))
                {
                    isGray = false;
                }

                if (ptr->a < 255)
                {
                    hasTransparency = true;
                }

                ptr++;
            }
        }

        OutputPixelFormat format;

        if (isGray)
        {
            format = hasTransparency ? OutputPixelFormat::GrayAlpha : OutputPixelFormat::Gray;
        }
        else
        {
            format = hasTransparency ? OutputPixelFormat::Rgba : OutputPixelFormat::Rgb;
        }

        return format;
    }

    std::vector<uint8_t> CreateJxlImageBuffer(const BitmapData* bitmap, OutputPixelFormat format)
    {
        const size_t width = static_cast<size_t>(bitmap->width);
        const size_t height = static_cast<size_t>(bitmap->height);
        const size_t srcStride = static_cast<size_t>(bitmap->stride);
        const uint8_t* srcScan0 = bitmap->scan0;

        size_t destChannelCount = 3;
        switch (format)
        {
        case OutputPixelFormat::Gray:
            destChannelCount = 1;
            break;
        case OutputPixelFormat::GrayAlpha:
            destChannelCount = 2;
            break;
        case OutputPixelFormat::Rgb:
            destChannelCount = 3;
            break;
        case OutputPixelFormat::Rgba:
            destChannelCount = 4;
            break;
        }

        const size_t destStride = width * destChannelCount;

        std::vector<uint8_t> jxlImagePixels;
        jxlImagePixels.resize(width * height * destChannelCount);

        uint8_t* destScan0 = jxlImagePixels.data();

        for (size_t y = 0; y < height; y++)
        {
            const ColorBgra* src = reinterpret_cast<const ColorBgra*>(srcScan0 + (y * srcStride));
            uint8_t* dest = destScan0 + (y * destStride);

            for (size_t x = 0; x < width; x++)
            {
                switch (format)
                {
                case OutputPixelFormat::Gray:
                    dest[0] = src->r;
                    break;
                case OutputPixelFormat::GrayAlpha:
                    dest[0] = src->r;
                    dest[1] = src->a;
                    break;
                case OutputPixelFormat::Rgb:
                    dest[0] = src->r;
                    dest[1] = src->g;
                    dest[2] = src->b;
                    break;
                case OutputPixelFormat::Rgba:
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

        return jxlImagePixels;
    }

    bool ReportProgress(ProgressProc progressProc, int32_t progressPercentage)
    {
        bool shouldContinue = true;

        if (progressProc)
        {
            shouldContinue = progressProc(progressPercentage);
        }

        return shouldContinue;
    }
}

EncoderStatus EncoderWriteImage(
    const BitmapData* bitmap,
    const EncoderOptions* options,
    const EncoderImageMetadata* metadata,
    ErrorInfo* errorInfo,
    ProgressProc progressCallback,
    WriteDataProc writeDataCallback)
{
    if (!bitmap || !options || !writeDataCallback)
    {
        return EncoderStatus::NullParameter;
    }

    try
    {
        if (!ReportProgress(progressCallback, 0))
        {
            return EncoderStatus::UserCancelled;
        }

        const OutputPixelFormat outputPixelFormat = GetOutputPixelFormat(bitmap);
        const std::vector<uint8_t> outputPixels = CreateJxlImageBuffer(bitmap, outputPixelFormat);

        if (!ReportProgress(progressCallback, 5))
        {
            return EncoderStatus::UserCancelled;
        }

        auto runner = JxlResizableParallelRunnerMake(nullptr);

        JxlResizableParallelRunnerSetThreads(
            runner.get(),
            JxlResizableParallelRunnerSuggestThreads(bitmap->width, bitmap->height));

        auto enc = JxlEncoderMake(nullptr);

        if (JxlEncoderSetParallelRunner(
            enc.get(),
            JxlResizableParallelRunner,
            runner.get()) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderSetParallelRunner failed.");
            return EncoderStatus::EncodeError;
        }

        if (!ReportProgress(progressCallback, 10))
        {
            return EncoderStatus::UserCancelled;
        }

        if (JxlEncoderUseBoxes(enc.get()) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderUseBoxes failed.");
            return EncoderStatus::EncodeError;
        }

        JxlBasicInfo basicInfo;
        JxlEncoderInitBasicInfo(&basicInfo);

        basicInfo.xsize = bitmap->width;
        basicInfo.ysize = bitmap->height;
        basicInfo.bits_per_sample = 8;
        basicInfo.exponent_bits_per_sample = 0;
        basicInfo.uses_original_profile = options->lossless;
        basicInfo.alpha_exponent_bits = 0;
        basicInfo.alpha_premultiplied = false;

        JxlPixelFormat format{};
        format.data_type = JXL_TYPE_UINT8;
        format.endianness = JXL_NATIVE_ENDIAN;

        switch (outputPixelFormat)
        {
        case OutputPixelFormat::Gray:
            basicInfo.num_color_channels = 1;
            basicInfo.num_extra_channels = 0;
            basicInfo.alpha_bits = 0;
            format.num_channels = 1;
            break;
        case OutputPixelFormat::GrayAlpha:
            basicInfo.num_color_channels = 1;
            basicInfo.num_extra_channels = 1;
            basicInfo.alpha_bits = basicInfo.bits_per_sample;
            format.num_channels = 2;
            break;
        case OutputPixelFormat::Rgb:
            basicInfo.num_color_channels = 3;
            basicInfo.num_extra_channels = 0;
            basicInfo.alpha_bits = 0;
            format.num_channels = 3;
            break;
        case OutputPixelFormat::Rgba:
            basicInfo.num_color_channels = 3;
            basicInfo.num_extra_channels = 1;
            basicInfo.alpha_bits = basicInfo.bits_per_sample;
            format.num_channels = 4;
            break;
        }

        if (!ReportProgress(progressCallback, 15))
        {
            return EncoderStatus::UserCancelled;
        }

        if (JxlEncoderSetBasicInfo(enc.get(), &basicInfo) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderSetBasicInfo failed.");
            return EncoderStatus::EncodeError;
        }

        if (!ReportProgress(progressCallback, 20))
        {
            return EncoderStatus::UserCancelled;
        }

        if (metadata)
        {
            if (metadata->iccProfileSize > 0)
            {
                if (JxlEncoderSetICCProfile(
                    enc.get(),
                    metadata->iccProfile,
                    metadata->iccProfileSize) != JXL_ENC_SUCCESS)
                {
                    SetErrorMessage(errorInfo, "JxlEncoderSetICCProfile failed.");
                    return EncoderStatus::EncodeError;
                }
            }
            else
            {
                JxlColorEncoding colorEncoding{};
                bool isGray = outputPixelFormat == OutputPixelFormat::Gray || outputPixelFormat == OutputPixelFormat::GrayAlpha;

                JxlColorEncodingSetToSRGB(&colorEncoding, isGray);
                colorEncoding.rendering_intent = JXL_RENDERING_INTENT_PERCEPTUAL;

                if (JxlEncoderSetColorEncoding(enc.get(), &colorEncoding) != JXL_ENC_SUCCESS)
                {
                    SetErrorMessage(errorInfo, "JxlEncoderSetColorEncoding failed.");
                    return EncoderStatus::EncodeError;
                }
            }

            if (metadata->exifSize > 0 || metadata->xmpSize > 0)
            {
                if (metadata->exifSize > 0)
                {
                    if (JxlEncoderAddBox(
                        enc.get(),
                        "Exif",
                        metadata->exif,
                        metadata->exifSize,
                        JXL_FALSE) != JXL_ENC_SUCCESS)
                    {
                        SetErrorMessage(errorInfo, "JxlEncoderAddBox failed.");
                        return EncoderStatus::EncodeError;
                    }
                }

                if (metadata->xmpSize > 0)
                {
                    if (JxlEncoderAddBox(
                        enc.get(),
                        "xml ",
                        metadata->xmp,
                        metadata->xmpSize,
                        JXL_FALSE) != JXL_ENC_SUCCESS)
                    {
                        SetErrorMessage(errorInfo, "JxlEncoderAddBox failed.");
                        return EncoderStatus::EncodeError;
                    }
                }
            }
        }

        if (!ReportProgress(progressCallback, 25))
        {
            return EncoderStatus::UserCancelled;
        }

        JxlEncoderFrameSettings* encoderOptions = JxlEncoderFrameSettingsCreate(enc.get(), nullptr);

        if (JxlEncoderSetFrameDistance(encoderOptions, options->distance) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderOptionsSetDistance failed.");
            return EncoderStatus::EncodeError;
        }

        if (JxlEncoderSetFrameLossless(encoderOptions, options->lossless) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderOptionsSetLossless failed.");
            return EncoderStatus::EncodeError;
        }

        if (JxlEncoderFrameSettingsSetOption(encoderOptions, JXL_ENC_FRAME_SETTING_EFFORT, options->speed) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderOptionsSetEffort failed.");
            return EncoderStatus::EncodeError;
        }

        if (!ReportProgress(progressCallback, 30))
        {
            return EncoderStatus::UserCancelled;
        }

        if (JxlEncoderAddImageFrame(encoderOptions, &format, outputPixels.data(), outputPixels.size()) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderAddImageFrame failed.");
            return EncoderStatus::EncodeError;
        }

        if (!ReportProgress(progressCallback, 50))
        {
            return EncoderStatus::UserCancelled;
        }

        JxlEncoderCloseInput(enc.get());

        // The libjxl process output loop reserves the 60% to 90% range of the progress percentage.
        // If the process output loop takes more than 10 iterations the progress bar will stop at 90% but the
        // progress callback will still be called to allow for cancellation.
        int progressPercentageDone = 60;
        int progressSegment = 0;
        constexpr int maxProgressSegments = 10;
        constexpr int progressPercentageStep = 3;

        std::vector<uint8_t> compressed;
        compressed.resize(262144);
        uint8_t* next_out = compressed.data();
        size_t avail_out = compressed.size() - (next_out - compressed.data());

        JxlEncoderStatus processOutputStatus = JXL_ENC_NEED_MORE_OUTPUT;

        while (processOutputStatus == JXL_ENC_NEED_MORE_OUTPUT)
        {
            if (!ReportProgress(progressCallback, progressPercentageDone))
            {
                return EncoderStatus::UserCancelled;
            }

            if (progressSegment < maxProgressSegments)
            {
                progressSegment++;
                progressPercentageDone += progressPercentageStep;
            }

            processOutputStatus = JxlEncoderProcessOutput(enc.get(), &next_out, &avail_out);

            if (processOutputStatus == JXL_ENC_NEED_MORE_OUTPUT)
            {
                size_t offset = next_out - compressed.data();
                compressed.resize(compressed.size() * 2);
                next_out = compressed.data() + offset;
                avail_out = compressed.size() - offset;
            }
        }

        if (processOutputStatus != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderProcessOutput failed.");
            return EncoderStatus::EncodeError;
        }

        if (!ReportProgress(progressCallback, 95))
        {
            return EncoderStatus::UserCancelled;
        }

        const size_t compressedDataSize = next_out - compressed.data();

        if (!writeDataCallback(compressed.data(), compressedDataSize))
        {
            return EncoderStatus::WriteError;
        }
    }
    catch (const std::bad_alloc&)
    {
        return EncoderStatus::OutOfMemory;
    }
    catch (...)
    {
        return EncoderStatus::EncodeError;
    }

    return EncoderStatus::Ok;
}
