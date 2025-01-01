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

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace JpegXLFileTypePlugin.Interop
{
    internal sealed partial class EncoderImageMetadata
    {
        [CustomMarshaller(typeof(EncoderImageMetadata), MarshalMode.ManagedToUnmanagedIn, typeof(Marshaller))]
        internal static unsafe class Marshaller
        {
            // This must be kept in sync with the EncoderImageMetadata structure in JxlEncoderTypes.h.
            [StructLayout(LayoutKind.Sequential)]
            public unsafe struct Native
            {
                public void* exif;
                public nuint exifSize;
                public void* iccProfile;
                public nuint iccProfileSize;
                public void* xmp;
                public nuint xmpSize;
            }

            public static Native ConvertToUnmanaged(EncoderImageMetadata managed)
            {
                Native native = new();

                if (managed.iccProfile.Length > 0)
                {
                    native.iccProfile = NativeMemory.Alloc((uint)managed.iccProfile.Length);
                    managed.iccProfile.Span.CopyTo(new Span<byte>((byte*)native.iccProfile, managed.iccProfile.Length));
                    native.iccProfileSize = (uint)managed.iccProfile.Length;
                }

                if (managed.exif.Length > 0)
                {
                    native.exif = NativeMemory.Alloc((uint)managed.exif.Length);
                    managed.exif.Span.CopyTo(new Span<byte>((byte*)native.exif, managed.exif.Length));
                    native.exifSize = (uint)managed.exif.Length;
                }

                if (managed.xmp.Length > 0)
                {
                    native.xmp = NativeMemory.Alloc((uint)managed.xmp.Length);
                    managed.xmp.Span.CopyTo(new Span<byte>((byte*)native.xmp, managed.xmp.Length));
                    native.xmpSize = (uint)managed.xmp.Length;
                }

                return native;
            }

            public static void Free(Native native)
            {
                if (native.iccProfile != null)
                {
                    NativeMemory.Free(native.iccProfile);
                }

                if (native.exif != null)
                {
                    NativeMemory.Free(native.exif);
                }

                if (native.xmp != null)
                {
                    NativeMemory.Free(native.xmp);
                }
            }
        }
    }
}
