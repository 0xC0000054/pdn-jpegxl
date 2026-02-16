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

using PaintDotNet;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;

namespace JpegXLFileTypePlugin.Interop
{
    // Wraps an IBitmapSource around a RegionPtr. Note that this is very unsafe to do, as it
    // does not guarantee that the memory backed by the RegionPtr isn't freed before the
    // IBitmapSource is done with it. However, in the context of a Paint.NET FileType plugin,
    // the RegionPtrs it gives us will be valid for the lifetime of the Save or Load operation,
    // so this is safe to do as long as the IBitmapSource isn't used after the Save or Load
    // operation completes.
    internal sealed class RegionPtrBitmapSource<TPixel>
        : BitmapSourceBase<TPixel>
          where TPixel : unmanaged, INaturalPixelInfo
    {
        private readonly RegionPtr<TPixel> region;

        public RegionPtrBitmapSource(RegionPtr<TPixel> region)
            : base(region.Size)
        {
            this.region = region;
        }

        protected override void OnCopyPixels(RegionPtr<TPixel> dst, Point2Int32 srcOffset)
        {
            this.region.Slice(srcOffset, dst.Size).CopyTo(dst);
        }
    }
}
