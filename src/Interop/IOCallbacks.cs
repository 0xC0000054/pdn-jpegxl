////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal unsafe delegate int WriteDelegate(byte* buffer, nuint count);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int SeekDelegate(ulong offset);

    [StructLayout(LayoutKind.Sequential)]
    internal struct IOCallbacks
    {
        public nint Write;
        public nint Seek;
    }
}
