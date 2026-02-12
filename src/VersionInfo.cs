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

using JpegXLFileTypePlugin.Interop;
using System;
using System.Diagnostics;

namespace JpegXLFileTypePlugin
{
    internal static class VersionInfo
    {
        private static readonly Lazy<string> libjxlVersion = new(GetLibJxlVersion);
        private static readonly Lazy<string> pluginVersion = new(GetPluginVersion);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string LibJxlVersion => libjxlVersion.Value;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string PluginVersion => pluginVersion.Value;

        private static string GetLibJxlVersion()
            => JpegXLNative.GetLibJxlVersion().ToString();

        private static string GetPluginVersion()
            => typeof(VersionInfo).Assembly.GetName().Version!.ToString();
    }
}
