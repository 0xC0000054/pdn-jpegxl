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
    internal static class HResult
    {
        public const int S_OK = 0;
        public const int E_POINTER = unchecked((int)0x80004003);
        public const int E_ABORT = unchecked((int)0x80004004);
        public const int SeekError = unchecked((int)0x80070019); // HRESULT_FROM_WIN32(ERROR_SEEK)
    }
}
