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
    internal static class QualityToDistanceLookupTable
    {
        private static readonly float[] lookupTable = BuildQualityToDistanceLookupTable();

        public static float GetValue(int quality)
        {
            return lookupTable[Math.Clamp(quality, 0, 100)];
        }

        private static float[] BuildQualityToDistanceLookupTable()
        {
            float[] table = new float[101];

            for (int i = 0; i < table.Length; i++)
            {
                // Map our quality settings to the range used by libjxl
                //
                // We use a quality value range where 0 is the lowest and 100 is the highest
                // libjxl uses a distance value range where 15.0f is the lowest and 0.0f is the highest

                // The following code was adapted from the libjxl GIMP plugin.
                // https://github.com/libjxl/libjxl/blob/8001738dc9cd8dc6fa24cf75fefd08f909b2ac3c/plugins/gimp/file-jxl-save.cc#L543

                float distance;

                if (i >= 30)
                {
                    // This code will produce a lossy encoding value of 0.1 when the quality is 100.
                    // The lossless encoding value 0.0 will be set when lossless mode is enabled.
                    distance = 0.1f + ((100 - i) * 0.09f);
                }
                else
                {
                    // Values less than or equal to 8 always exceed 15.
                    if (i <= 8)
                    {
                        distance = 15.0f;
                    }
                    else
                    {
                        distance = 6.4f + MathF.Pow(2.5f, (30 - i) / 5.0f) / 6.25f;
                    }
                }

                table[i] = distance;
            }

            return table;
        }
    }
}
