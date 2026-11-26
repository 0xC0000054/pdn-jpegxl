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

namespace JpegXLFileTypePlugin.Interop
{
    // The result of the SetCicpColorInfo callback.
    // The values must stay in sync with the native SetCicpColorInfoResult enum.
    internal enum SetCicpColorInfoResult : int
    {
        Ok = 0,
        // The code points cannot be represented; the native decoder falls back to the ICC profile.
        Unsupported,
        // An error occurred, see DecoderImage.ExceptionInfo.
        Error,
    }
}
