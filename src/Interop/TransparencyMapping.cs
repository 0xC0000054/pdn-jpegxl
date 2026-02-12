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

namespace JpegXLFileTypePlugin.Interop
{
    internal static class TransparencyMapping
    {
        internal static byte ToEightBit(ushort value)
        {
            return UInt16ToUInt8LookupTable.GetValue(value);
        }

        internal static byte ToEightBit(Half value)
        {
            return (byte)(Half.Clamp(value, Half.Zero, Half.One) * 255);
        }

        internal static byte ToEightBit(float value)
        {
            return (byte)(Math.Clamp(value, 0f, 1f) * 255f);
        }

        private static class UInt16ToUInt8LookupTable
        {
            private static readonly byte[] lookupTable = BuildLookupTable();

            internal static byte GetValue(ushort input)
            {
                return lookupTable[input];
            }

            private static byte[] BuildLookupTable()
            {
                byte[] table = new byte[65536];

                for (int i = 0; i < table.Length; i++)
                {
                    // 65535 / 255 is 257, so we don't
                    // need to use floating-point math.
                    table[i] = (byte)(i / 257);
                }

                return table;
            }
        }
    }
}
