////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using JpegXLFileTypePlugin.Properties;
using PaintDotNet;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using System.Collections.Generic;
using System.IO;

namespace JpegXLFileTypePlugin
{
    internal sealed class JpegXLFileType : PropertyBasedFileType
    {
        public JpegXLFileType(IFileTypeHost host)
            : base("JPEG XL", new FileTypeOptions
            {
                LoadExtensions = new string[] { ".jxl" },
                SaveExtensions = new string[] { ".jxl" },
                SupportsCancellation = true,
                SupportsLayers = false
            })
        {
        }

        private enum PropertyNames
        {
            Quality,
            Lossless,
            EncoderSpeed,
            GitHubLink
        }

        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            List<Property> props = new()
            {
                new Int32Property(PropertyNames.Quality, 90, 0, 100),
                new BooleanProperty(PropertyNames.Lossless, false),
                new Int32Property(PropertyNames.EncoderSpeed, 7, 1, 9),
            };

            List<PropertyCollectionRule> rules = new()
            {
                new ReadOnlyBoundToBooleanRule(PropertyNames.Quality, PropertyNames.Lossless, inverse: false)
            };

            return new PropertyCollection(props, rules);
        }

        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo info = CreateDefaultSaveConfigUI(props);

            PropertyControlInfo qualityPCI = info.FindControlForPropertyName(PropertyNames.Quality);
            qualityPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = Resources.Quality_DisplayName;
            qualityPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = string.Empty;

            PropertyControlInfo losslessPCI = info.FindControlForPropertyName(PropertyNames.Lossless);
            losslessPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            losslessPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = Resources.Lossless_Description;

            PropertyControlInfo encoderSpeedPCI = info.FindControlForPropertyName(PropertyNames.EncoderSpeed);
            encoderSpeedPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = Resources.EncoderSpeed_DisplayName;
            encoderSpeedPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = Resources.EncoderSpeed_Description;

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
            int quality = token.GetProperty<Int32Property>(PropertyNames.Quality).Value;
            bool lossless = token.GetProperty<BooleanProperty>(PropertyNames.Lossless).Value;
            int speed = token.GetProperty<Int32Property>(PropertyNames.EncoderSpeed).Value;

            JpegXLSave.Save(input, output, scratchSurface, progressCallback, quality, lossless, speed);
        }
    }
}
