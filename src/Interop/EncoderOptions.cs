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

using System.Runtime.InteropServices.Marshalling;

namespace JpegXLFileTypePlugin.Interop
{
    [NativeMarshalling(typeof(Marshaller))]
    internal sealed partial class EncoderOptions
    {
        public readonly float distance;
        public readonly int effort;
        public readonly bool lossless;

        public EncoderOptions(int quality, bool lossless, int effort)
        {
            // Lossless encoding implies a distance value of 0.0, anything higher than that
            // will use lossy encoding.
            distance = lossless ? 0.0f : QualityToDistanceLookupTable.GetValue(quality);
            this.effort = effort;
            this.lossless = lossless;
        }
    }
}
