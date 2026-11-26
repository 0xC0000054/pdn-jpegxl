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
    // Wraps an IBitmapSource around a RegionPtr. Note that this is inherently unsafe: this type
    // does not and cannot guarantee that the memory backed by the RegionPtr remains valid for
    // as long as the IBitmapSource is in use. It is the caller's responsibility to ensure that
    // the RegionPtr's backing store stays alive and is not freed or reused for the entire
    // lifetime of the IBitmapSource. In the context of a Paint.NET FileType plugin, this
    // requirement is typically satisfied by only using the IBitmapSource within the lifetime of
    // the corresponding Save or Load operation.
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
