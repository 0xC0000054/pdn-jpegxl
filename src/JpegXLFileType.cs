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

using JpegXLFileTypePlugin.Localization;
using PaintDotNet;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using System;
using System.Collections.Generic;
using System.IO;

namespace JpegXLFileTypePlugin
{
    internal sealed class JpegXLFileType : PropertyBasedFileType
    {
        private static readonly string[] FileExtensions = [".jxl"];

        private readonly IJpegXLStringResourceManager strings;

        public JpegXLFileType(IFileTypeHost host)
            : base("JPEG XL", new FileTypeOptions
            {
                LoadExtensions = FileExtensions,
                SaveExtensions = FileExtensions,
                SupportsCancellation = true,
                SupportsLayers = false
            })
        {
            if (host != null)
            {
                strings = new PdnLocalizedStringResourceManager(host.Services.GetService<PaintDotNet.JpegXL.IJpegXLFileTypeStrings>()!);
            }
            else
            {
                strings = new BuiltinStringResourceManager();
            }
        }

        private enum PropertyNames
        {
            Quality,
            Lossless,
            EncoderSpeed,
            ForumLink,
            GitHubLink,
            PluginVersion,
            LibJxlVersion
        }

        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            List<Property> props =
            [
                new Int32Property(PropertyNames.Quality, 90, 0, 100),
                new BooleanProperty(PropertyNames.Lossless, false),
                new Int32Property(PropertyNames.EncoderSpeed, 7, 1, 9),
                new UriProperty(PropertyNames.ForumLink, new Uri("https://forums.getpaint.net/topic/120716-jpeg-xl-filetype")),
                new UriProperty (PropertyNames.GitHubLink, new Uri("https://github.com/0xC0000054/pdn-jpegxl")),
                new StringProperty(PropertyNames.PluginVersion),
                new StringProperty(PropertyNames.LibJxlVersion)
            ];

            List<PropertyCollectionRule> rules =
            [
                new ReadOnlyBoundToBooleanRule(PropertyNames.Quality, PropertyNames.Lossless, inverse: false)
            ];

            return new PropertyCollection(props, rules);
        }

        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo info = CreateDefaultSaveConfigUI(props);

            PropertyControlInfo qualityPCI = info.FindControlForPropertyName(PropertyNames.Quality)!;
            qualityPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = GetString("Quality_DisplayName");
            qualityPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = string.Empty;

            PropertyControlInfo losslessPCI = info.FindControlForPropertyName(PropertyNames.Lossless)!;
            losslessPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            losslessPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = GetString("Lossless_Description");

            PropertyControlInfo encoderSpeedPCI = info.FindControlForPropertyName(PropertyNames.EncoderSpeed)!;
            encoderSpeedPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = GetString("EncoderSpeed_DisplayName");
            encoderSpeedPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = GetString("EncoderSpeed_Description");

            PropertyControlInfo forumLinkPCI = info.FindControlForPropertyName(PropertyNames.ForumLink)!;
            forumLinkPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = GetString("ForumLink_DisplayName");
            forumLinkPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = GetString("ForumLink_Description");

            PropertyControlInfo githubLinkPCI = info.FindControlForPropertyName(PropertyNames.GitHubLink)!;
            githubLinkPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            githubLinkPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = "GitHub"; // GitHub is a brand name that should not be localized.

            PropertyControlInfo pluginVersionPCI = info.FindControlForPropertyName(PropertyNames.PluginVersion)!;
            pluginVersionPCI.ControlType.Value = PropertyControlType.Label;
            pluginVersionPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            pluginVersionPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = "JpegXLFileType v" + VersionInfo.PluginVersion;

            PropertyControlInfo libwebpVersionPCI = info.FindControlForPropertyName(PropertyNames.LibJxlVersion)!;
            libwebpVersionPCI.ControlType.Value = PropertyControlType.Label;
            libwebpVersionPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            libwebpVersionPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = "libjxl v" + VersionInfo.LibJxlVersion;

            return info;
        }

        protected override Document OnLoad(Stream input)
        {
            return JpegXLLoad.Load(input);
        }

        protected override void OnSaveT(Document input,
                                        Stream output,
                                        PropertyBasedSaveConfigToken token,
                                        Surface scratchSurface,
                                        ProgressEventHandler progressCallback)
        {
            int quality = token.GetProperty<Int32Property>(PropertyNames.Quality)!.Value;
            bool lossless = token.GetProperty<BooleanProperty>(PropertyNames.Lossless)!.Value;
            int speed = token.GetProperty<Int32Property>(PropertyNames.EncoderSpeed)!.Value;

            JpegXLSave.Save(input, output, scratchSurface, progressCallback, quality, lossless, speed);
        }

        private string GetString(string name)
        {
            return strings.GetString(name);
        }
    }
}
