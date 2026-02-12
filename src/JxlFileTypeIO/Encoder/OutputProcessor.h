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
#include "Common.h"
#include "JxlEncoderTypes.h"
#include "jxl/encode.h"
#include <vector>

class OutputProcessor
{
public:
    OutputProcessor(IOCallbacks* callbacks);

    EncoderStatus GetWriteStatus() const;
    void InitializeProgressReporting(
        ProgressProc progressCallback,
        int32_t initialProgressPercentage,
        int32_t maxProgressPercentage,
        int32_t progressStep);
    JxlEncoderOutputProcessor ToJxlOutputProcessor();

private:
    static void* GetBufferStatic(void* opaque, size_t* size);
    static void ReleaseBufferStatic(void* opaque, size_t writtenBytes);
    static void SeekStatic(void* opaque, uint64_t position);
    static void SetFinalizedPositionStatic(void* opaque, uint64_t finalizedPosition);

    void* GetBuffer(size_t* size);
    void ReleaseBuffer(size_t writtenBytes);
    void Seek(uint64_t position);
    void SetFinalizedPosition(uint64_t finalizedPosition);

    bool ReportProgress();
    void SetWriteStatusIfFailed(int hr);

    IOCallbacks* callbacks;
    std::vector<uint8_t> buffer;
    EncoderStatus status;
    ProgressProc progressCallback;
    int32_t progressPercentage;
    int32_t maxProgressPercentage;
    int32_t progressStep;
};

