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

using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace JpegXLFileTypePlugin
{
    // This class is named BitmapUtil2 to avoid a naming conflict with the Paint.NET BitmapUtil class.
    internal static class BitmapUtil2
    {
        /// <summary>
        /// Provides lock rectangles for <see cref="IBitmap.Lock(RectInt32, BitmapLockOptions)"/>
        /// that are under the 4 GB size limit of the <see cref="IBitmapLock"/> API.
        /// </summary>
        /// <param name="source">The image to lock.</param>
        /// <returns>A sequence of lock rectangles.</returns>
        internal static IEnumerable<RectInt32> EnumerateLockRects(IBitmap source)
        {
            SizeInt32 bitmapSize = source.Size;

            int fullBitmapWidth = bitmapSize.Width;
            int fullBitmapHeight = bitmapSize.Height;

            int copyHeight = GetLargeCopyHeight(fullBitmapWidth, source.PixelFormat);

            for (int y = 0; y < fullBitmapHeight; y += copyHeight)
            {
                yield return RectInt32.FromEdges(0,
                                                 y,
                                                 fullBitmapWidth,
                                                 Math.Min(y + copyHeight, fullBitmapHeight));
            }
        }

        private static int GetLargeCopyHeight(int width, PixelFormat pixelFormat)
        {
            int bitsPerPixel = pixelFormat.GetBitsPerPixel();

            int minStride = GetMinStrideChecked(width, bitsPerPixel);

            return GetLargeCopyHeight(minStride);
        }

        // This is an internal method in the Paint.NET BitmapUtil class.
        // https://github.com/0xC0000054/pdn-jpegxl/issues/7#issuecomment-2678924298
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetLargeCopyHeight(int stride)
        {
            // Need a copy height that is <4GB, due to CopyPixels()'s bufferSize being a 32-bit uint.
            // Choose a copy height of: 1) at least 1 row, 2) as big as possible, 3) no chance to overflow
            // y while looping through the rows.
            // Just using int.MaxValue would either immediately overflow y, or necessitate using a 64-bit
            // int and extra casts. The copy height doesn't really need to be super big, as the loop
            // overhead is basically zero once it's at thousands or millions anyway.
            // So we use:
            return Math.Max(1, ((1 << 30) / 4) / stride); // 256MB/stride
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetMinStrideChecked(int width, int bitsPerPixel)
        {
            return checked((int)((((long)width * bitsPerPixel) + 7) >> 3));
        }
    }
}
