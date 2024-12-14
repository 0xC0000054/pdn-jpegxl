////////////////////////////////////////////////////////////////////////
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
using PaintDotNet.Rendering;
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

            using (IImagingFactory imagingFactory = ImagingFactory.CreateRef())
            using (DecoderImage decoderImage = new(imagingFactory))
            {
                JpegXLNative.LoadImage(data, decoderImage);

                doc = new Document(decoderImage.Width, decoderImage.Height);

                SetDocumentColorProfile(decoderImage, doc, imagingFactory);

                AddBackgroundLayer(decoderImage, doc, imagingFactory);

                ExifValueCollection? exifValues = decoderImage.TryGetExif();

                if (exifValues != null)
                {
                    foreach (KeyValuePair<ExifPropertyPath, ExifValue> item in exifValues)
                    {
                        ExifPropertyPath path = item.Key;

                        doc.Metadata.AddExifPropertyItem(path.Section, path.TagID, item.Value);
                    }
                }

                XmpPacket? xmpPacket = decoderImage.GetXmp();

                if (xmpPacket != null)
                {
                    doc.Metadata.SetXmpPacket(xmpPacket);
                }
            }

            return doc;
        }


        private static void AddBackgroundLayer(DecoderImage decoderImage, Document doc, IImagingFactory imagingFactory)
        {
            DecoderLayerData layerData = decoderImage.LayerData ?? throw new FormatException("The layer data was null.");
            JpegXLImageFormat format = decoderImage.Format;

            BitmapLayer bitmapLayer = new(decoderImage.Width, decoderImage.Height);
            Surface surface = bitmapLayer.Surface;

            if (format == JpegXLImageFormat.Rgb)
            {
                SetLayerColorDataFromRgbImage(layerData.Color, surface);
            }
            else
            {
                SetLayerColorDataFromConvertedImage(layerData.Color,
                                                    decoderImage.TryGetColorContext(),
                                                    format,
                                                    surface,
                                                    imagingFactory);
            }

            if (layerData.Transparency != null)
            {
                SetLayerTransparency(layerData.Transparency!, surface);
            }
            else
            {
                PixelKernels.SetAlphaChannel(surface.AsRegionPtr().Cast<ColorBgra32>(), ColorAlpha8.Opaque);
            }

            doc.Layers.Add(bitmapLayer);
        }

        private static void SetDocumentColorProfile(DecoderImage decoderImage, Document doc, IImagingFactory imagingFactory)
        {
            JpegXLImageFormat format = decoderImage.Format;

            if (format == JpegXLImageFormat.Rgb)
            {
                IColorContext? colorContext = decoderImage.TryGetColorContext();

                if (colorContext != null)
                {
                    doc.SetColorContext(colorContext);
                }
            }
            else if (format == JpegXLImageFormat.Gray)
            {
                using (IColorContext colorContext = imagingFactory.CreateColorContext(PaintDotNet.Direct2D1.DeviceColorSpace.Srgb))
                {
                    doc.SetColorContext(colorContext);
                }
            }
        }

        private static unsafe void SetLayerColorDataFromConvertedImage(IBitmap color,
                                                                       IColorContext? sourceColorContext,
                                                                       JpegXLImageFormat format,
                                                                       Surface surface,
                                                                       IImagingFactory imagingFactory)
        {
            if (sourceColorContext != null)
            {
                using (IColorContext dstColorContext = imagingFactory.CreateColorContext(PaintDotNet.Direct2D1.DeviceColorSpace.Srgb))
                using (IBitmapSource<ColorRgb24> convertedBitmapSource = imagingFactory.CreateColorTransformedBitmap<ColorRgb24>(color,
                                                                                                                                 sourceColorContext,
                                                                                                                                 dstColorContext))
                {
                    using (IBitmap<ColorRgb24> convertedBitmap = convertedBitmapSource.ToBitmap())
                    {
                        SetLayerColorDataFromRgbImage(convertedBitmap, surface);
                    }
                }
            }
            else
            {
                if (format == JpegXLImageFormat.Gray)
                {
                    SetLayerColorDataFromGrayImage(color, surface);
                }
            }
        }

        private static unsafe void SetLayerColorDataFromGrayImage(IBitmap color, Surface surface)
        {
            using (IBitmapLock bitmapLock = color.Lock(BitmapLockOptions.Read))
            {
                byte* srcScan0 = (byte*)bitmapLock.Buffer;
                int srcStride = bitmapLock.BufferStride;

                RegionPtr<ColorBgra32> destRegion = surface.AsRegionPtr().Cast<ColorBgra32>();

                int width = surface.Width;
                int height = surface.Height;

                for (int y = 0; y < height; y++)
                {
                    byte* src = srcScan0 + ((long)y * srcStride);
                    ColorBgra32* dst = destRegion.Rows[y].Ptr;

                    for (int x = 0; x < width; x++)
                    {
                        dst->R = dst->G = dst->B = *src;
                        src++;
                        dst++;
                    }
                }
            }
        }

        private static unsafe void SetLayerColorDataFromRgbImage(IBitmap color, Surface surface)
        {
            using (IBitmapLock bitmapLock = color.Lock(BitmapLockOptions.Read))
            {
                byte* srcScan0 = (byte*)bitmapLock.Buffer;
                int srcStride = bitmapLock.BufferStride;

                RegionPtr<ColorBgra32> destRegion = surface.AsRegionPtr().Cast<ColorBgra32>();

                int width = surface.Width;
                int height = surface.Height;

                for (int y = 0; y < height; y++)
                {
                    ColorRgb24* src = (ColorRgb24*)(srcScan0 + ((long)y * srcStride));
                    ColorBgra32* dst = destRegion.Rows[y].Ptr;

                    for (int x = 0; x < width; x++)
                    {
                        dst->R = src->R;
                        dst->G = src->G;
                        dst->B = src->B;
                        src++;
                        dst++;
                    }
                }
            }
        }

        private static unsafe void SetLayerTransparency(IBitmap<ColorAlpha8> transparency, Surface surface)
        {
            using (IBitmapLock<ColorAlpha8> bitmapLock = transparency.Lock(BitmapLockOptions.Read))
            {
                RegionPtr<ColorAlpha8> sourceRegion = bitmapLock.AsRegionPtr();
                RegionPtr<ColorBgra32> destRegion = surface.AsRegionPtr().Cast<ColorBgra32>();

                int width = sourceRegion.Width;
                int height = sourceRegion.Height;

                for (int y = 0; y < height; y++)
                {
                    ColorAlpha8* src = sourceRegion.Rows[y].Ptr;
                    ColorBgra32* dst = destRegion.Rows[y].Ptr;

                    for (int x = 0; x < width; x++)
                    {
                        dst->A = src->A;
                        src++;
                        dst++;
                    }
                }
            }
        }
    }
}
