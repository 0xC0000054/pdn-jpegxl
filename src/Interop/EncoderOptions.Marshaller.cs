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
    internal sealed partial class EncoderOptions
    {
        [CustomMarshaller(typeof(EncoderOptions), MarshalMode.ManagedToUnmanagedIn, typeof(Marshaller))]
        internal static unsafe class Marshaller
        {
            public struct Native
            {
                public float distance;
                public int effort;
                public byte lossless;
            }

            public static Native ConvertToUnmanaged(EncoderOptions managed)
            {
                return new()
                {
                    distance = managed.distance,
                    effort = managed.effort,
                    lossless = (byte)(managed.lossless ? 1 : 0)
                };
            }
        }
    }
}
