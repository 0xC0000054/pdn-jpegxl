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

#include "DecoderContext.h"
#include "jxl/resizable_parallel_runner.h"
#include <stdexcept>

DecoderContext::DecoderContext(const uint8_t* imageDataBuffer, size_t imageDataBufferSize)
    : imageData(imageDataBuffer),
      imageDataSize(imageDataBufferSize),
      dec(JxlDecoderMake(nullptr)),
      basicInfo{},
      pixelFormat{ 4, JXL_TYPE_UINT8, JXL_NATIVE_ENDIAN, 0 },
      decoderImageFormat(DecoderImageFormat::Gray),
      cmykBlackChannelIndex(std::numeric_limits<uint32_t>::max())
{
    if (!dec)
    {
        throw std::runtime_error("Failed to create the decoder object.");
    }

    SetDecoderInput();
}

JxlDecoder* DecoderContext::GetDecoder() const
{
    return dec.get();
}

const JxlBasicInfo& DecoderContext::GetBasicInfo() const
{
    return basicInfo;
}

JxlBasicInfo* DecoderContext::GetBasicInfoPtr()
{
    return &basicInfo;
}

JxlPixelFormat& DecoderContext::GetPixelFormat()
{
    return pixelFormat;
}

const JxlPixelFormat& DecoderContext::GetPixelFormat() const
{
    return pixelFormat;
}

const JxlPixelFormat* DecoderContext::GetPixelFormatPtr() const
{
    return &pixelFormat;
}

DecoderImageFormat DecoderContext::GetDecoderImageFormat() const
{
    return decoderImageFormat;
}

void DecoderContext::SetDecoderImageFormat(DecoderImageFormat format)
{
    decoderImageFormat = format;
}

ImageChannelRepresentation DecoderContext::GetImageChannelRepresentation() const
{
    return imageChannelRepresentation;
}

void DecoderContext::SetImageChannelRepresentation(ImageChannelRepresentation representation)
{
    imageChannelRepresentation = representation;
}

uint32_t DecoderContext::GetCmykBlackChannelIndex() const
{
    return cmykBlackChannelIndex;
}

void DecoderContext::SetCmykBlackChannelIndex(uint32_t index)
{
    cmykBlackChannelIndex = index;
}

void DecoderContext::SetResizableParallelRunner() const
{
    if (!runner)
    {
        runner = JxlResizableParallelRunnerMake(nullptr);

        if (!runner)
        {
            throw std::runtime_error("JxlResizableParallelRunnerMake failed.");
        }

        const size_t suggestedThreads = JxlResizableParallelRunnerSuggestThreads(basicInfo.xsize, basicInfo.ysize);

        JxlResizableParallelRunnerSetThreads(runner.get(), suggestedThreads);

        if (JxlDecoderSetParallelRunner(
            dec.get(),
            JxlResizableParallelRunner,
            runner.get()) != JXL_DEC_SUCCESS)
        {
            throw std::runtime_error("JxlDecoderSetParallelRunner failed.");
        }
    }
}

void DecoderContext::ResetDecoder()
{
    JxlDecoderReleaseInput(dec.get());
    JxlDecoderReset(dec.get());
    runner.reset();
    SetDecoderInput();
}

void DecoderContext::SetDecoderInput()
{
    if (JxlDecoderSetInput(dec.get(), imageData, imageDataSize) != JXL_DEC_SUCCESS)
    {
        throw std::runtime_error("JxlDecoderSetInput failed.");
    }
    JxlDecoderCloseInput(dec.get());
}
