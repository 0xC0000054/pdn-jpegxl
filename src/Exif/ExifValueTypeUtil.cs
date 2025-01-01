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

namespace JpegXLFileTypePlugin.Exif
{
    internal static class ExifValueTypeUtil
    {
        /// <summary>
        /// Gets the size in bytes of a <see cref="TagDataType"/> value.
        /// </summary>
        /// <param name="type">The tag type.</param>
        /// <returns>
        /// The size of the value in bytes.
        /// </returns>
        public static int GetSizeInBytes(ExifValueType type)
        {
            return type switch
            {
                ExifValueType.Byte or ExifValueType.Ascii or ExifValueType.Undefined or (ExifValueType)6 => 1,
                ExifValueType.Short or ExifValueType.SShort => 2,
                ExifValueType.Long or ExifValueType.SLong or ExifValueType.Float or (ExifValueType)13 => 4,
                ExifValueType.Rational or ExifValueType.SRational or ExifValueType.Double => 8,
                _ => 0,
            };
        }

        /// <summary>
        /// Determines whether the values fit in the offset field.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="count">The count.</param>
        /// <returns>
        /// <see langword="true"/> if the values fit in the offset field; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool ValueFitsInOffsetField(ExifValueType type, uint count)
        {
            return type switch
            {
                ExifValueType.Byte or ExifValueType.Ascii or ExifValueType.Undefined or (ExifValueType)6 => count <= 4,
                ExifValueType.Short or ExifValueType.SShort => count <= 2,
                ExifValueType.Long or ExifValueType.SLong or ExifValueType.Float or (ExifValueType)13 => count <= 1,
                _ => false,
            };
        }
    }
}
