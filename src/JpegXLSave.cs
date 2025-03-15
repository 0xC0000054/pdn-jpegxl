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
                                       int effort)
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

            EncoderOptions options = new(quality, lossless, effort);
            EncoderImageMetadata metadata = CreateImageMetadata(input);

            JpegXLNative.SaveImage(scratchSurface, options, metadata, progressCallback, output);
        }

        private static EncoderImageMetadata CreateImageMetadata(Document input)
        {
            byte[]? exifBytes = null;
            byte[]? iccProfileBytes = null;
            byte[]? xmpBytes = null;

            ExifColorSpace exifColorSpace = ExifColorSpace.Srgb;

            IColorContext? colorContext = input.GetColorContext();

            if (colorContext != null)
            {
                // We do not set an ICC profile for sRGB images as JpegXL can signal that
                // using its built-in color space encoding, and sRGB is the default for
                // images without an ICC profile.
                if (colorContext.Type != ColorContextType.ExifColorSpace
                    || colorContext.ExifColorSpace != PaintDotNet.Imaging.ExifColorSpace.Srgb)
                {
                    iccProfileBytes = colorContext.GetProfileBytes().ToArray();

                    if (iccProfileBytes.Length > 0)
                    {
                        exifColorSpace = ExifColorSpace.Uncalibrated;
                    }
                }
            }

            Dictionary<ExifPropertyPath, ExifValue>? propertyItems = GetExifMetadataFromDocument(input);

            if (propertyItems != null)
            {
                propertyItems.Remove(ExifPropertyKeys.Image.InterColorProfile.Path);

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

            return new EncoderImageMetadata(exifBytes, iccProfileBytes, xmpBytes);
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
