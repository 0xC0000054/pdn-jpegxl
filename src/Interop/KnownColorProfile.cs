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
    // RGB color encodings are reported via the CICP color info; only the two gray encodings use this enum.
    // The values must stay in sync with the native KnownColorProfile enum.
    internal enum KnownColorProfile : int
    {
        LinearGray = 0,
        GraySrgbTRC,
    }
}
