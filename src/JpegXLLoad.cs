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

            BitmapLayer bitmapLayer = Layer.CreateBackgroundLayer(decoderImage.Width, decoderImage.Height);

            if (!string.IsNullOrWhiteSpace(layerData.Name))
            {
                bitmapLayer.Name = layerData.Name;
            }

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
            else if (format == JpegXLImageFormat.Cmyk)
            {
                // https://discord.com/channels/143867839282020352/960223751599976479/1167941100976222360
                // Clinton Ingram (saucecontrol) recommends using Adobe RGB for CMYK data that is converted to RGB.
                using (IColorContext colorContext = imagingFactory.CreateColorContext(PaintDotNet.Imaging.ExifColorSpace.AdobeRgb))
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
                PaintDotNet.Imaging.ExifColorSpace dstColorSpace = PaintDotNet.Imaging.ExifColorSpace.Srgb;

                if (format == JpegXLImageFormat.Cmyk)
                {
                    // https://discord.com/channels/143867839282020352/960223751599976479/1167941100976222360
                    // Clinton Ingram (saucecontrol) recommends using Adobe RGB for CMYK data that is converted to RGB.
                    dstColorSpace = PaintDotNet.Imaging.ExifColorSpace.AdobeRgb;
                }

                using (IColorContext dstColorContext = imagingFactory.CreateColorContext(dstColorSpace))
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
                else if (format == JpegXLImageFormat.Cmyk)
                {
                    throw new FormatException("A CMYK image must have a valid ICC profile.");
                }
            }
        }

        private static unsafe void SetLayerColorDataFromGrayImage(IBitmap color, Surface surface)
        {
            CopyFromBitmapChunked(color, surface, (bitmapLock, destRegion) =>
            {
                byte* srcScan0 = (byte*)bitmapLock.Buffer;
                int srcStride = bitmapLock.BufferStride;

                int width = destRegion.Width;
                int height = destRegion.Height;

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
            });
        }

        private static unsafe void SetLayerColorDataFromRgbImage(IBitmap color, Surface surface)
        {
            CopyFromBitmapChunked(color, surface, (bitmapLock, destRegion) =>
            {
                byte* srcScan0 = (byte*)bitmapLock.Buffer;
                int srcStride = bitmapLock.BufferStride;

                int width = destRegion.Width;
                int height = destRegion.Height;

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
            });
        }

        private static unsafe void SetLayerTransparency(IBitmap<ColorAlpha8> transparency, Surface surface)
        {
            CopyFromBitmapChunked(transparency, surface, (bitmapLock, destRegion) =>
            {
                byte* srcScan0 = (byte*)bitmapLock.Buffer;
                int srcStride = bitmapLock.BufferStride;

                int width = destRegion.Width;
                int height = destRegion.Height;

                for (int y = 0; y < height; y++)
                {
                    ColorAlpha8* src = (ColorAlpha8*)(srcScan0 + ((long)y * srcStride));
                    ColorBgra32* dst = destRegion.Rows[y].Ptr;

                    for (int x = 0; x < width; x++)
                    {
                        dst->A = src->A;
                        src++;
                        dst++;
                    }
                }
            });
        }

        private static unsafe void CopyFromBitmapChunked(IBitmap source,
                                                         Surface destination,
                                                         Action<IBitmapLock, RegionPtr<ColorBgra32>> copyAction)
        {
            RegionPtr<ColorBgra32> surfaceRegion = destination.AsRegionPtr().Cast<ColorBgra32>();

            foreach (RectInt32 copyRect in BitmapUtil2.EnumerateLockRects(source))
            {
                RegionPtr<ColorBgra32> destRegion = surfaceRegion.Slice(copyRect);

                using (IBitmapLock bitmapLock = source.Lock(copyRect, BitmapLockOptions.Read))
                {
                    copyAction(bitmapLock, destRegion);
                }
            }
        }
    }
}
