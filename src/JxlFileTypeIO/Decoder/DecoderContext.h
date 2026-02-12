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

#pragma once
#include "jxl/decode_cxx.h"
#include "jxl/resizable_parallel_runner_cxx.h"
#include "JxlDecoderTypes.h"

class DecoderContext
{
public:
    DecoderContext(const uint8_t* imageDataBuffer, size_t imageDataBufferSize);

    JxlDecoder* GetDecoder() const;

    const JxlBasicInfo& GetBasicInfo() const;
    JxlBasicInfo* GetBasicInfoPtr();

    JxlPixelFormat& GetPixelFormat();
    const JxlPixelFormat& GetPixelFormat() const;
    const JxlPixelFormat* GetPixelFormatPtr() const;

    DecoderImageFormat GetDecoderImageFormat() const;
    void SetDecoderImageFormat(DecoderImageFormat format);

    ImageChannelRepresentation GetImageChannelRepresentation() const;
    void SetImageChannelRepresentation(ImageChannelRepresentation representation);

    uint32_t GetCmykBlackChannelIndex() const;
    void SetCmykBlackChannelIndex(uint32_t index);

    void SetResizableParallelRunner() const;

    void ResetDecoder();

private:
    void SetDecoderInput();

    JxlDecoderPtr dec;
    mutable JxlResizableParallelRunnerPtr runner;
    const uint8_t* imageData;
    size_t imageDataSize;
    DecoderImageFormat decoderImageFormat;
    ImageChannelRepresentation imageChannelRepresentation;
    uint32_t cmykBlackChannelIndex;
    JxlBasicInfo basicInfo;
    JxlPixelFormat pixelFormat;
};
