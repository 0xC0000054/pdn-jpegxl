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
                public byte hasCicpColorInfo;
                public byte cicpColorPrimaries;
                public byte cicpTransferCharacteristics;
                public byte cicpMatrixCoefficients;
                public byte cicpVideoFullRangeFlag;
            }

            public static Native ConvertToUnmanaged(EncoderImageMetadata managed)
            {
                Native native = new();

                native.hasCicpColorInfo = managed.hasCicpColorInfo ? (byte)1 : (byte)0;
                native.cicpColorPrimaries = managed.cicpColorPrimaries;
                native.cicpTransferCharacteristics = managed.cicpTransferCharacteristics;
                native.cicpMatrixCoefficients = managed.cicpMatrixCoefficients;
                native.cicpVideoFullRangeFlag = managed.cicpVideoFullRangeFlag;

                try
                {
                    native.iccProfile = AllocAndCopy(managed.iccProfile, out native.iccProfileSize);
                    native.exif = AllocAndCopy(managed.exif, out native.exifSize);
                    native.xmp = AllocAndCopy(managed.xmp, out native.xmpSize);
                }
                catch (Exception)
                {
                    // The generated stub only calls Free after ConvertToUnmanaged returns successfully,
                    // so any earlier allocations would leak if a later Alloc throws.
                    Free(native);
                    throw;
                }

                return native;
            }

            private static void* AllocAndCopy(ReadOnlyMemory<byte> data, out nuint size)
            {
                if (data.Length == 0)
                {
                    size = 0;
                    return null;
                }

                void* block = NativeMemory.Alloc((uint)data.Length);
                data.Span.CopyTo(new Span<byte>((byte*)block, data.Length));
                size = (uint)data.Length;
                return block;
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
