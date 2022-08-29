////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class EncoderOptions
    {
        public readonly float distance;
        public readonly int speed;
        [MarshalAs(UnmanagedType.U1)]
        public readonly bool lossless;

        public EncoderOptions(int quality, bool lossless, int encoderSpeed)
        {
            // Lossless encoding implies a distance value of 0.0, anything higher than that
            // will use lossy encoding.
            distance = lossless ? 0.0f : QualityToDistanceLookupTable.GetValue(quality);
            // We use an encoder speed range where 9 is the fastest and 1 is the slowest.
            // libjxl uses an encoder speed range where 1 is the fastest and 9 is the slowest.
            speed = 10 - encoderSpeed;
            this.lossless = lossless;
        }
    }
}
