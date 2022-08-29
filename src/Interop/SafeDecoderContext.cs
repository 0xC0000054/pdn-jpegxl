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

using Microsoft.Win32.SafeHandles;

namespace JpegXLFileTypePlugin.Interop
{
    internal abstract class SafeDecoderContext : SafeHandleZeroOrMinusOneIsInvalid
    {
        protected SafeDecoderContext(bool ownsHandle) : base(ownsHandle)
        {
        }
    }

    internal sealed class SafeDecoderContextX64 : SafeDecoderContext
    {
        public SafeDecoderContextX64() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            JpegXL_X64.DestroyDecoder(handle);
            return true;
        }
    }

    internal sealed class SafeDecoderContextArm64 : SafeDecoderContext
    {
        public SafeDecoderContextArm64() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            JpegXL_Arm64.DestroyDecoder(handle);
            return true;
        }
    }

    internal sealed class SafeDecoderContextX86 : SafeDecoderContext
    {
        public SafeDecoderContextX86() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            JpegXL_X86.DestroyDecoder(handle);
            return true;
        }
    }
}
