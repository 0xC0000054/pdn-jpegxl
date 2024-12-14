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

using PaintDotNet.Collections;
using System;
using System.Collections.Generic;

namespace JpegXLFileTypePlugin.Exif
{
    internal static class ReadOnlyListExtensions
    {
        internal static T[] AsArrayOrToArray<T>(this IReadOnlyList<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            T[]? asArray = items as T[];

            if (asArray is not null)
            {
                return asArray;
            }
            else
            {
                return items.ToArrayEx();
            }
        }
    }
}
