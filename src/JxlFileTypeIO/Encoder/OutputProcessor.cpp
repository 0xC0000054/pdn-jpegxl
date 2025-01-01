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

#include "OutputProcessor.h"
#include "Windows.h"

static constexpr size_t maxBufferSize = 65536;

OutputProcessor::OutputProcessor(IOCallbacks* callbacks)
    : callbacks(callbacks),
      status(EncoderStatus::Ok),
      progressCallback(nullptr),
      progressPercentage(0),
      maxProgressPercentage(0),
      progressStep(0)
{
}

EncoderStatus OutputProcessor::GetWriteStatus() const
{
    return status;
}

void OutputProcessor::InitializeProgressReporting(
    ProgressProc progressCallback,
    int32_t initialProgressPercentage,
    int32_t maxProgressPercentage,
    int32_t progressStep)
{
    this->progressCallback = progressCallback;
    this->progressPercentage = initialProgressPercentage;
    this->maxProgressPercentage = maxProgressPercentage;
    this->progressStep = progressStep;
}

JxlEncoderOutputProcessor OutputProcessor::ToJxlOutputProcessor()
{
    JxlEncoderOutputProcessor processor{};
    processor.opaque = this;
    processor.get_buffer = GetBufferStatic;
    processor.release_buffer = ReleaseBufferStatic;
    processor.seek = SeekStatic;
    processor.set_finalized_position = SetFinalizedPositionStatic;

    return processor;
}

void* OutputProcessor::GetBufferStatic(void* opaque, size_t* size)
{
    return static_cast<OutputProcessor*>(opaque)->GetBuffer(size);
}

void OutputProcessor::ReleaseBufferStatic(void* opaque, size_t writtenBytes)
{
    static_cast<OutputProcessor*>(opaque)->ReleaseBuffer(writtenBytes);
}

void OutputProcessor::SeekStatic(void* opaque, uint64_t position)
{
    static_cast<OutputProcessor*>(opaque)->Seek(position);
}

void OutputProcessor::SetFinalizedPositionStatic(void* opaque, uint64_t finalizedPosition)
{
    static_cast<OutputProcessor*>(opaque)->SetFinalizedPosition(finalizedPosition);
}

void* OutputProcessor::GetBuffer(size_t* size)
{
    if (status != EncoderStatus::Ok || !ReportProgress())
    {
        // Returning a null pointer and a size of 0 will tell the library
        // to stop processing and return an error.
        *size = 0;
        return nullptr;
    }

    *size = min(maxBufferSize, *size);

    if (buffer.size() < *size)
    {
        buffer.resize(*size);
    }

    return buffer.data();
}

void OutputProcessor::ReleaseBuffer(size_t writtenBytes)
{
    SetWriteStatusIfFailed(callbacks->Write(buffer.data(), writtenBytes));
    buffer.clear();
}

void OutputProcessor::Seek(uint64_t position)
{
    SetWriteStatusIfFailed(callbacks->Seek(position));
}

void OutputProcessor::SetFinalizedPosition(uint64_t finalizedPosition)
{
}

bool OutputProcessor::ReportProgress()
{
    bool result = true;

    if (progressCallback)
    {
        if (progressPercentage < maxProgressPercentage)
        {
            progressPercentage += progressStep;
        }

        result = progressCallback(progressPercentage);

        if (!result)
        {
            status = EncoderStatus::UserCanceled;
        }
    }

    return result;
}

void OutputProcessor::SetWriteStatusIfFailed(int hr)
{
    if (FAILED(hr))
    {
        switch (hr)
        {
        case E_ABORT:
            status = EncoderStatus::UserCanceled;
            break;
        case E_OUTOFMEMORY:
            status = EncoderStatus::OutOfMemory;
            break;
        default:
            status = EncoderStatus::WriteError;
            break;
        }
    }
}
