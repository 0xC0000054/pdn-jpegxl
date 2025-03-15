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

#include "JxlEncoder.h"
#include "OutputProcessor.h"
#include "PixelFormatConversion.h"
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

    OutputPixelFormat GetOutputPixelFormat(const BitmapData* bitmap, bool hasICCProfile)
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

        // Don't auto-convert images with an ICC profile to gray scale.
        // The image's profile is RGB, and RGB profiles should not be used with a gray scale image.
        if (isGray && !hasICCProfile)
        {
            format = hasTransparency ? OutputPixelFormat::GrayAlpha : OutputPixelFormat::Gray;
        }
        else
        {
            format = hasTransparency ? OutputPixelFormat::Rgba : OutputPixelFormat::Rgb;
        }

        return format;
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

    EncoderStatus AddFrame(
        const BitmapData* bitmap,
        const JxlBasicInfo& basicInfo,
        const JxlEncoderFrameSettings* frameSettings,
        const OutputProcessor& outputProcessor,
        ErrorInfo* errorInfo)
    {
        const uint32_t numberOfChannels = basicInfo.num_color_channels + basicInfo.num_extra_channels;

        std::vector<uint8_t> frameBuffer;
        frameBuffer.resize(static_cast<size_t>(basicInfo.xsize) * basicInfo.ysize * numberOfChannels);

        switch (numberOfChannels)
        {
        case 1:
            PixelFormatConversion::BgraToGray(bitmap, frameBuffer.data());
            break;
        case 2:
            PixelFormatConversion::BgraToGrayAlpha(bitmap, frameBuffer.data());
            break;
        case 3:
            PixelFormatConversion::BgraToRgb(bitmap, frameBuffer.data());
            break;
        case 4:
            PixelFormatConversion::BgraToRgba(bitmap, frameBuffer.data());
            break;
        default:
            return EncoderStatus::EncodeError;
        }

        JxlPixelFormat format{};
        format.num_channels = numberOfChannels;
        format.data_type = JXL_TYPE_UINT8;
        format.endianness = JXL_NATIVE_ENDIAN;

        EncoderStatus status = EncoderStatus::Ok;

        if (JxlEncoderAddImageFrame(
            frameSettings,
            &format,
            frameBuffer.data(),
            frameBuffer.size()) != JXL_ENC_SUCCESS)
        {
            status = outputProcessor.GetWriteStatus();

            if (status == EncoderStatus::Ok)
            {
                SetErrorMessage(errorInfo, "JxlEncoderAddImageFrame failed.");
                status = EncoderStatus::EncodeError;
            }
        }

        return status;
    }
}

EncoderStatus EncoderWriteImage(
    const BitmapData* bitmap,
    const EncoderOptions* options,
    const EncoderImageMetadata* metadata,
    IOCallbacks* callbacks,
    ErrorInfo* errorInfo,
    ProgressProc progressCallback)
{
    if (!bitmap || !options || !callbacks || !metadata)
    {
        return EncoderStatus::NullParameter;
    }

    try
    {
        if (!ReportProgress(progressCallback, 0))
        {
            return EncoderStatus::UserCanceled;
        }

        const OutputPixelFormat outputPixelFormat = GetOutputPixelFormat(bitmap, metadata->iccProfileSize > 0);

        if (!ReportProgress(progressCallback, 5))
        {
            return EncoderStatus::UserCanceled;
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

        OutputProcessor outputProcessor(callbacks);

        if (JxlEncoderSetOutputProcessor(
            enc.get(),
            outputProcessor.ToJxlOutputProcessor()) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderSetOutputProcessor failed.");
            return EncoderStatus::EncodeError;
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

        switch (outputPixelFormat)
        {
        case OutputPixelFormat::Gray:
            basicInfo.num_color_channels = 1;
            basicInfo.num_extra_channels = 0;
            basicInfo.alpha_bits = 0;
            break;
        case OutputPixelFormat::GrayAlpha:
            basicInfo.num_color_channels = 1;
            basicInfo.num_extra_channels = 1;
            basicInfo.alpha_bits = basicInfo.bits_per_sample;
            break;
        case OutputPixelFormat::Rgb:
            basicInfo.num_color_channels = 3;
            basicInfo.num_extra_channels = 0;
            basicInfo.alpha_bits = 0;
            break;
        case OutputPixelFormat::Rgba:
            basicInfo.num_color_channels = 3;
            basicInfo.num_extra_channels = 1;
            basicInfo.alpha_bits = basicInfo.bits_per_sample;
            break;
        }

        if (!ReportProgress(progressCallback, 15))
        {
            return EncoderStatus::UserCanceled;
        }

        if (JxlEncoderSetBasicInfo(enc.get(), &basicInfo) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderSetBasicInfo failed.");
            return EncoderStatus::EncodeError;
        }

        if (!ReportProgress(progressCallback, 20))
        {
            return EncoderStatus::UserCanceled;
        }

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

        if (!ReportProgress(progressCallback, 25))
        {
            return EncoderStatus::UserCanceled;
        }

        JxlEncoderFrameSettings* frameSettings = JxlEncoderFrameSettingsCreate(enc.get(), nullptr);

        if (JxlEncoderSetFrameDistance(frameSettings, options->distance) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderOptionsSetDistance failed.");
            return EncoderStatus::EncodeError;
        }

        if (JxlEncoderSetFrameLossless(frameSettings, options->lossless) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderOptionsSetLossless failed.");
            return EncoderStatus::EncodeError;
        }

        if (JxlEncoderFrameSettingsSetOption(frameSettings, JXL_ENC_FRAME_SETTING_EFFORT, options->effort) != JXL_ENC_SUCCESS)
        {
            SetErrorMessage(errorInfo, "JxlEncoderOptionsSetEffort failed.");
            return EncoderStatus::EncodeError;
        }

        // The libjxl process output loop reserves the 40% to 90% range of the progress percentage.
        // If the process output loop takes more than 10 iterations the progress bar will stop at 90% but the
        // progress callback will still be called to allow for cancellation.
        outputProcessor.InitializeProgressReporting(
            progressCallback,
            30,
            90,
            5);

        EncoderStatus status = AddFrame(bitmap, basicInfo, frameSettings, outputProcessor, errorInfo);

        if (status != EncoderStatus::Ok)
        {
            return status;
        }

        JxlEncoderCloseInput(enc.get());

        status = outputProcessor.GetWriteStatus();

        if (status != EncoderStatus::Ok)
        {
            return status;
        }

        if (!ReportProgress(progressCallback, 95))
        {
            return EncoderStatus::UserCanceled;
        }

        if (JxlEncoderFlushInput(enc.get()) != JXL_ENC_SUCCESS)
        {
            status = outputProcessor.GetWriteStatus();

            if (status != EncoderStatus::Ok)
            {
                return status;
            }
            else
            {
                SetErrorMessage(errorInfo, "JxlEncoderFlushInput failed.");
                return EncoderStatus::EncodeError;
            }
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
