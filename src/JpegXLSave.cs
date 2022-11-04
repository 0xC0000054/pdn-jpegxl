﻿////////////////////////////////////////////////////////////////////////
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

using JpegXLFileTypePlugin.Exif;
using JpegXLFileTypePlugin.Interop;
using PaintDotNet;
using PaintDotNet.Collections;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ExifColorSpace = JpegXLFileTypePlugin.Exif.ExifColorSpace;

namespace JpegXLFileTypePlugin
{
    internal static class JpegXLSave
    {
        public static unsafe void Save(Document input,
                                       Stream output,
                                       Surface scratchSurface,
                                       ProgressEventHandler progressEventHandler,
                                       int quality,
                                       bool lossless,
                                       int speed)
        {
            scratchSurface.Clear();
            input.CreateRenderer().Render(scratchSurface);

            ProgressCallback? progressCallback = null;

            if (progressEventHandler != null)
            {
                progressCallback = new ProgressCallback(delegate (int progress)
                {
                    try
                    {
                        progressEventHandler.Invoke(null, new ProgressEventArgs(progress, true));
                        return true;
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }
                });
            }

            EncoderOptions options = new(quality, lossless, speed);
            EncoderImageMetadata? metadata = CreateImageMetadata(input);

            JpegXLNative.SaveImage(scratchSurface, options, metadata, progressCallback, output);
        }

        private static EncoderImageMetadata? CreateImageMetadata(Document input)
        {
            byte[]? exifBytes = null;
            byte[]? iccProfileBytes = null;
            byte[]? xmpBytes = null;

            Dictionary<ExifPropertyPath, ExifValue>? propertyItems = GetExifMetadataFromDocument(input);

            if (propertyItems != null)
            {
                ExifColorSpace exifColorSpace = ExifColorSpace.Srgb;

                if (propertyItems.TryGetValue(ExifPropertyKeys.Photo.ColorSpace.Path, out ExifValue? value))
                {
                    propertyItems.Remove(ExifPropertyKeys.Photo.ColorSpace.Path);

                    if (MetadataHelpers.TryDecodeShort(value, out ushort colorSpace))
                    {
                        exifColorSpace = (ExifColorSpace)colorSpace;
                    }
                }

                if (iccProfileBytes != null)
                {
                    exifColorSpace = ExifColorSpace.Uncalibrated;
                }
                else
                {
                    ExifPropertyPath iccProfileKey = ExifPropertyKeys.Image.InterColorProfile.Path;

                    if (propertyItems.TryGetValue(iccProfileKey, out ExifValue? iccProfileItem))
                    {
                        iccProfileBytes = iccProfileItem.Data.ToArrayEx();
                        propertyItems.Remove(iccProfileKey);
                        exifColorSpace = ExifColorSpace.Uncalibrated;
                    }
                }

                if (iccProfileBytes != null)
                {
                    // Remove the InteroperabilityIndex and related tags, these tags should
                    // not be written if the image has an ICC color profile.
                    propertyItems.Remove(ExifPropertyKeys.Interop.InteroperabilityIndex.Path);
                    propertyItems.Remove(ExifPropertyKeys.Interop.InteroperabilityVersion.Path);
                }

                exifBytes = new ExifWriter(input, propertyItems, exifColorSpace).CreateExifBlob();
            }

            XmpPacket? xmpPacket = input.Metadata.TryGetXmpPacket();

            if (xmpPacket != null)
            {
                string xmpPacketAsString = xmpPacket.ToString(XmpPacketWrapperType.ReadOnly);

                xmpBytes = Encoding.UTF8.GetBytes(xmpPacketAsString);
            }

            EncoderImageMetadata? metadata = null;

            if (exifBytes != null || iccProfileBytes != null || xmpBytes != null)
            {
                metadata = new EncoderImageMetadata(exifBytes, iccProfileBytes, xmpBytes);
            }

            return metadata;
        }

        private static Dictionary<ExifPropertyPath, ExifValue>? GetExifMetadataFromDocument(Document doc)
        {
            Dictionary<ExifPropertyPath, ExifValue>? items = null;

            ExifPropertyItem[] exifProperties = doc.Metadata.GetExifPropertyItems();

            if (exifProperties.Length > 0)
            {
                items = new Dictionary<ExifPropertyPath, ExifValue>(exifProperties.Length);

                foreach (ExifPropertyItem property in exifProperties)
                {
                    items.TryAdd(property.Path, property.Value);
                }
            }

            return items;
        }
    }
}
