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

using JpegXLFileTypePlugin.Properties;

namespace JpegXLFileTypePlugin.Localization
{
    internal sealed class BuiltinStringResourceManager : IJpegXLStringResourceManager
    {
        public string GetString(string name)
        {
            return Resources.ResourceManager.GetString(name)!;
        }
    }
}
