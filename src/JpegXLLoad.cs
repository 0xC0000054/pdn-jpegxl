﻿////////////////////////////////////////////////////////////////////////
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

using JpegXLFileTypePlugin.Exif;
using JpegXLFileTypePlugin.Interop;
using PaintDotNet;
using PaintDotNet.Imaging;
using System;
using System.Collections.Generic;
using System.IO;

namespace JpegXLFileTypePlugin
{
    internal static class JpegXLLoad
    {
        public static Document Load(Stream input)
        {
            Document doc;

            byte[] data = new byte[input.Length];
            input.ReadExactly(data, 0, data.Length);

            using (DecoderLayerData layerData = new())
            using (DecoderImageMetadata imageMetadata = new())
            {
                JpegXLNative.LoadImage(data, layerData, imageMetadata);

                BitmapLayer? layer = layerData.Layer ?? throw new InvalidOperationException("The layer is null.");
                doc = new Document(layer.Width, layer.Height);

                AddMetadataToDocument(imageMetadata, doc);

                doc.Layers.Add(layer);
                layerData.OwnsLayer = false;
            }

            return doc;
        }

        private static unsafe void AddMetadataToDocument(DecoderImageMetadata imageMetadata, Document document)
        {
            byte[]? iccProfileBytes = imageMetadata.TryGetIccProfileBytes();

            if (iccProfileBytes != null)
            {
                ExifPropertyKey interColorProfile = ExifPropertyKeys.Image.InterColorProfile;

                document.Metadata.AddExifPropertyItem(interColorProfile.Path.Section,
                                                      interColorProfile.Path.TagID,
                                                      new ExifValue(ExifValueType.Undefined,
                                                                    iccProfileBytes));
            }

            byte[]? exifBytes = imageMetadata.TryGetExifBytes();

            if (exifBytes != null)
            {
                ExifValueCollection? exifValues = ExifParser.Parse(exifBytes);

                if (exifValues != null)
                {
                    exifValues.Remove(ExifPropertyKeys.Image.InterColorProfile.Path);
                    // JPEG XL does not use the EXIF data for rotation.
                    exifValues.Remove(ExifPropertyKeys.Image.Orientation.Path);

                    foreach (KeyValuePair<ExifPropertyPath, ExifValue> item in exifValues)
                    {
                        ExifPropertyPath path = item.Key;

                        document.Metadata.AddExifPropertyItem(path.Section, path.TagID, item.Value);
                    }
                }
            }

            IReadOnlyList<byte[]> xmlMetadata = imageMetadata.GetXmlMetadata();

            foreach (byte[] xmlMetadataItem in xmlMetadata)
            {
                XmpPacket? xmpPacket = XmpPacket.TryParse(xmlMetadataItem);

                if (xmpPacket != null)
                {
                    document.Metadata.SetXmpPacket(xmpPacket);
                    break;
                }
            }
        }
    }
}
