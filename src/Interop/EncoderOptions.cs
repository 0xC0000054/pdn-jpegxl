////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023 Nicholas Hayes
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
        public readonly int speed;
        public readonly bool lossless;

        public EncoderOptions(int quality, bool lossless, int encoderSpeed)
        {
            // Lossless encoding implies a distance value of 0.0, anything higher than that
            // will use lossy encoding.
            distance = lossless ? 0.0f : QualityToDistanceLookupTable.GetValue(quality);
            speed = encoderSpeed;
            this.lossless = lossless;
        }
    }
}
