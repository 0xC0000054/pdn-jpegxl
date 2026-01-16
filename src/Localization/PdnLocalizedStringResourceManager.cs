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

using PaintDotNet.FileTypes.JpegXL;
using System;
using System.Linq;
using System.Collections.Generic;
using JpegXLFileTypePlugin.Properties;

namespace JpegXLFileTypePlugin.Localization
{
    internal sealed class PdnLocalizedStringResourceManager : IJpegXLStringResourceManager
    {
        private readonly IJpegXLFileTypeStrings strings;
        private static readonly IReadOnlyDictionary<string, JpegXLFileTypeStringNames> pdnLocalizedStringMap;

        static PdnLocalizedStringResourceManager()
        {
            // Use a dictionary to map the resource name to its JpegXLFileTypeStringNames value.
            // This avoids repeated calls to Enum.TryParse.
            // Adapted from https://stackoverflow.com/a/13677446
            pdnLocalizedStringMap = Enum.GetValues<JpegXLFileTypeStringNames>()
                                        .ToDictionary(kv => kv.ToString(), kv => kv, StringComparer.OrdinalIgnoreCase);
        }

        public PdnLocalizedStringResourceManager(IJpegXLFileTypeStrings strings)
        {
            this.strings = strings;
        }

        public string GetString(string name)
        {
            if (pdnLocalizedStringMap.TryGetValue(name, out JpegXLFileTypeStringNames value))
            {
                return strings?.TryGetString(value) ?? Resources.ResourceManager.GetString(name)!;
            }
            else
            {
                return Resources.ResourceManager.GetString(name)!;
            }
        }
    }
}
