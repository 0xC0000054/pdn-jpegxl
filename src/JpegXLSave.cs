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

using JpegXLFileTypePlugin.Interop;
using PaintDotNet;
using PaintDotNet.Collections;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.IO;
using System.Linq;

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
            byte[]? iccProfileBytes = null;

            ExifPropertyItem[] exifPropertyItems = input.Metadata.GetExifPropertyItems();

            if (exifPropertyItems != null)
            {
                ExifPropertyItem? iccProfileItem = exifPropertyItems.Where(p => p.Path == ExifPropertyKeys.Image.InterColorProfile.Path).FirstOrDefault();

                if (iccProfileItem != null)
                {
                    iccProfileBytes = iccProfileItem.Value.Data.ToArrayEx();
                }
            }

            if (iccProfileBytes != null)
            {
                return new EncoderImageMetadata(iccProfileBytes);
            }

            return null;
        }
    }
}
