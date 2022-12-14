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

using System;
using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    internal sealed class EncoderImageMetadataMarshaller : ICustomMarshaler
    {
        // This must be kept in sync with the EncoderImageMetadata structure in JxlEncoderTypes.h.
        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct NativeEncoderImageMetadata
        {
            public void* exif;
            public nuint exifSize;
            public void* iccProfile;
            public nuint iccProfileSize;
            public void* xmp;
            public nuint xmpSize;
        }

        private static readonly int NativeMetadataSize = Marshal.SizeOf<NativeEncoderImageMetadata>();
        private static readonly EncoderImageMetadataMarshaller instance = new();

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Style",
            "IDE0060:Remove unused parameter",
            Justification = "The cookie parameter is required by the ICustomMarshaler API.")]
        public static ICustomMarshaler GetInstance(string cookie)
        {
            return instance;
        }

        private EncoderImageMetadataMarshaller()
        {
        }

        public void CleanUpManagedData(object ManagedObj)
        {
        }

        public unsafe void CleanUpNativeData(IntPtr pNativeData)
        {
            if (pNativeData != IntPtr.Zero)
            {
                NativeEncoderImageMetadata* metadata = (NativeEncoderImageMetadata*)pNativeData;

                if (metadata->iccProfile != null)
                {
                    NativeMemory.Free(metadata->iccProfile);
                }

                NativeMemory.Free(metadata);
            }
        }

        public int GetNativeDataSize()
        {
            return NativeMetadataSize;
        }

        public unsafe IntPtr MarshalManagedToNative(object ManagedObj)
        {
            if (ManagedObj == null)
            {
                return IntPtr.Zero;
            }

            EncoderImageMetadata metadata = (EncoderImageMetadata)ManagedObj;

            void* nativeStructure = NativeMemory.Alloc((nuint)NativeMetadataSize);

            NativeEncoderImageMetadata* nativeMetadata = (NativeEncoderImageMetadata*)nativeStructure;

            if (metadata.exif != null && metadata.exif.Length > 0)
            {
                nativeMetadata->exif = NativeMemory.Alloc((nuint)metadata.exif.Length);
                Marshal.Copy(metadata.exif, 0, new IntPtr(nativeMetadata->exif), metadata.exif.Length);
                nativeMetadata->exifSize = (nuint)metadata.exif.Length;
            }
            else
            {
                nativeMetadata->exif = null;
                nativeMetadata->exifSize = 0;
            }

            if (metadata.iccProfile != null && metadata.iccProfile.Length > 0)
            {
                nativeMetadata->iccProfile = NativeMemory.Alloc((nuint)metadata.iccProfile.Length);
                Marshal.Copy(metadata.iccProfile, 0, new IntPtr(nativeMetadata->iccProfile), metadata.iccProfile.Length);
                nativeMetadata->iccProfileSize = (nuint)metadata.iccProfile.Length;
            }
            else
            {
                nativeMetadata->iccProfile = null;
                nativeMetadata->iccProfileSize = 0;
            }

            if (metadata.xmp != null && metadata.xmp.Length > 0)
            {
                nativeMetadata->xmp = NativeMemory.Alloc((nuint)metadata.xmp.Length);
                Marshal.Copy(metadata.xmp, 0, new IntPtr(nativeMetadata->xmp), metadata.xmp.Length);
                nativeMetadata->xmpSize = (nuint)metadata.xmp.Length;
            }
            else
            {
                nativeMetadata->xmp = null;
                nativeMetadata->xmpSize = 0;
            }

            return new IntPtr(nativeStructure);
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
        }
    }
}
