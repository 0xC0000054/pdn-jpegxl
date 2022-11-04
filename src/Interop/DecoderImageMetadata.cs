﻿////////////////////////////////////////////////////////////////////////
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

using PaintDotNet;
using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    internal sealed class DecoderImageMetadata : Disposable
    {
        private byte[]? iccProfileBytes;
        private GCHandle iccProfileBytesHandle;
        private byte[]? exifBytes;
        private GCHandle exifBytesHandle;
        private byte[]? xmpBytes;
        private GCHandle xmpBytesHandle;

        public ExceptionDispatchInfo? ExceptionInfo { get; private set; }

        public IntPtr CreateMetadataBufferCallback(MetadataType type, nuint size)
        {
            IntPtr bufferPtr = IntPtr.Zero;

            try
            {
                if (size > 0 && size <= int.MaxValue)
                {
                    if (type == MetadataType.IccProfile)
                    {
                        iccProfileBytes = new byte[size];
                        iccProfileBytesHandle = GCHandle.Alloc(iccProfileBytes, GCHandleType.Pinned);
                        bufferPtr = iccProfileBytesHandle.AddrOfPinnedObject();
                    }
                    else if (type == MetadataType.Exif)
                    {
                        exifBytes = new byte[size];
                        exifBytesHandle = GCHandle.Alloc(exifBytes, GCHandleType.Pinned);
                        bufferPtr = exifBytesHandle.AddrOfPinnedObject();
                    }
                    else if (type == MetadataType.Xmp)
                    {
                        xmpBytes = new byte[size];
                        xmpBytesHandle = GCHandle.Alloc(xmpBytes, GCHandleType.Pinned);
                        bufferPtr = xmpBytesHandle.AddrOfPinnedObject();
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                return IntPtr.Zero;
            }

            return bufferPtr;
        }

        public byte[]? TryGetExifBytes() => exifBytes;

        public byte[]? TryGetIccProfileBytes() => iccProfileBytes;

        public byte[]? TryGetXmpBytes() => xmpBytes;

        protected override void Dispose(bool disposing)
        {
            if (iccProfileBytesHandle.IsAllocated)
            {
                iccProfileBytesHandle.Free();
            }

            base.Dispose(disposing);
        }
    }
}
