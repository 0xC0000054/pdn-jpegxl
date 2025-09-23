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
using PaintDotNet.Direct2D1;
using PaintDotNet.Direct2D1.Effects;
using PaintDotNet.Dxgi;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

            BitmapLayer bitmapLayer = Layer.CreateBackgroundLayer(decoderImage.Width, decoderImage.Height);

            if (!string.IsNullOrWhiteSpace(layerData.Name))
            {
                bitmapLayer.Name = layerData.Name;
            }

            Surface surface = bitmapLayer.Surface;

            switch (decoderImage.ColorSpace)
            {
                case JpegXLColorSpace.Cmyk:
                    SetLayerColorDataFromCmykImage(layerData.Color,
                                                   decoderImage.TryGetColorContext(),
                                                   surface,
                                                   imagingFactory);
                    break;
                case JpegXLColorSpace.Gray:
                case JpegXLColorSpace.Rgb:
                    // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
                    // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.
                    SetLayerColorDataFromRgbImage(layerData.Color,
                                                  decoderImage.ChannelRepresentation,
                                                  decoderImage.HdrFormat,
                                                  surface);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown {nameof(JpegXLColorSpace)} value: {decoderImage.ColorSpace}.");
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
            JpegXLColorSpace colorSpace = decoderImage.ColorSpace;

            if (colorSpace == JpegXLColorSpace.Gray || colorSpace == JpegXLColorSpace.Rgb)
            {
                // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
                // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.
                // The color profiles will always be RGB, even if the original image was gray.
                // If the gray image has an ICC profile, it will be ignored.

                IColorContext? colorContext = decoderImage.TryGetColorContext();

                if (colorContext != null)
                {
                    doc.SetColorContext(colorContext);
                }
            }
            else if (colorSpace == JpegXLColorSpace.Cmyk)
            {
                // https://discord.com/channels/143867839282020352/960223751599976479/1167941100976222360
                // Clinton Ingram (saucecontrol) recommends using Adobe RGB for CMYK data that is converted to RGB.
                using (IColorContext colorContext = imagingFactory.CreateColorContext(PaintDotNet.Imaging.ExifColorSpace.AdobeRgb))
                {
                    doc.SetColorContext(colorContext);
                }
            }
        }

        private static unsafe void SetLayerColorDataFromCmykImage(IBitmap color,
                                                                  IColorContext? sourceColorContext,
                                                                  Surface surface,
                                                                  IImagingFactory imagingFactory)
        {
            if (sourceColorContext != null)
            {
                // https://discord.com/channels/143867839282020352/960223751599976479/1167941100976222360
                // Clinton Ingram (saucecontrol) recommends using Adobe RGB for CMYK data that is converted to RGB.
                PaintDotNet.Imaging.ExifColorSpace dstColorSpace = PaintDotNet.Imaging.ExifColorSpace.AdobeRgb;

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
                throw new FormatException("A CMYK image must have a valid ICC profile.");
            }
        }

        private static unsafe void SetLayerColorDataFromRgbImage(IBitmap color,
                                                                 JpegXLImageChannelRepresentation imageChannelRepresentation,
                                                                 HdrFormat hdrFormat,
                                                                 Surface surface)
        {
            if (hdrFormat == HdrFormat.None)
            {
                switch (imageChannelRepresentation)
                {
                    case JpegXLImageChannelRepresentation.Uint8:
                        SetLayerColorDataFromRgbImage(color.Cast<ColorRgb24>(), surface);
                        break;
                    case JpegXLImageChannelRepresentation.Uint16:
                    case JpegXLImageChannelRepresentation.Float16:
                    case JpegXLImageChannelRepresentation.Float32:
                        SetLayerColorDataFromSdrImage(color, surface);
                        break;
                    default:
                        throw new InvalidEnumArgumentException(nameof(imageChannelRepresentation),
                                                               (int)imageChannelRepresentation,
                                                               typeof(JpegXLImageChannelRepresentation));
                }
            }
            else if (hdrFormat == HdrFormat.PQ)
            {
                switch (imageChannelRepresentation)
                {
                    case JpegXLImageChannelRepresentation.Uint16:
                    case JpegXLImageChannelRepresentation.Float16:
                    case JpegXLImageChannelRepresentation.Float32:
                        SetLayerColorDataFromHdrPQImage(color, surface);
                        break;
                    case JpegXLImageChannelRepresentation.Uint8:
                    default:
                        throw new InvalidEnumArgumentException(nameof(imageChannelRepresentation),
                                                               (int)imageChannelRepresentation,
                                                               typeof(JpegXLImageChannelRepresentation));
                }
            }
            else
            {
                throw new InvalidOperationException($"Unsupported {nameof(hdrFormat)} value: {hdrFormat}.");
            }
        }

        private static unsafe void SetLayerColorDataFromRgbImage(IBitmap<ColorRgb24> color, Surface surface)
        {
            CopyFromBitmapChunked(color, surface, (srcRegion, destRegion) =>
            {
                int width = destRegion.Width;
                int height = destRegion.Height;

                for (int y = 0; y < height; y++)
                {
                    ColorRgb24* src = srcRegion.Rows[y].Ptr;
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
            CopyFromBitmapChunked(transparency, surface, (srcRegion, destRegion) =>
            {
                PixelKernels.ReplaceChannel(destRegion, srcRegion.Cast<byte>(), 3);
            });
        }

        private static unsafe void CopyFromBitmapChunked<TPixel>(IBitmap<TPixel> source,
                                                                 Surface destination,
                                                                 Action<RegionPtr<TPixel>, RegionPtr<ColorBgra32>> copyAction)
            where TPixel : unmanaged, INaturalPixelInfo
        {
            RegionPtr<ColorBgra32> surfaceRegion = destination.AsRegionPtr().Cast<ColorBgra32>();

            foreach (RectInt32 copyRect in BitmapUtil2.EnumerateLockRects(source))
            {
                RegionPtr<ColorBgra32> destRegion = surfaceRegion.Slice(copyRect);

                using (IBitmapLock<TPixel> bitmapLock = source.Lock(copyRect, BitmapLockOptions.Read))
                {
                    copyAction(bitmapLock.AsRegionPtr(), destRegion);
                }
            }
        }

        private static void SetLayerColorDataFromHdrPQImage(IBitmap source, Surface destination)
        {
            using (IImagingFactory imagingFactory = ImagingFactory.CreateRef())
            using (IColorContext dp3ColorContext = imagingFactory.CreateColorContext(KnownColorSpace.DisplayP3))
            using (IDirect2DFactory d2dFactory = Direct2DFactory.Create())
            {
                using (IBitmapSource<ColorPbgra32> dp3Image = PQToColorContext(source,
                                                                               imagingFactory,
                                                                               d2dFactory,
                                                                               dp3ColorContext))
                {
                    dp3Image.CopyPixels(destination.AsRegionPtr().Cast<ColorPbgra32>());
                }
            }

            static IBitmapSource<ColorPbgra32> PQToColorContext(
                IBitmapSource bitmap,
                IImagingFactory imagingFactory,
                IDirect2DFactory d2dFactory,
                IColorContext colorContext)
            {
                return d2dFactory.CreateBitmapSourceFromImage<ColorPbgra32>(
                    bitmap.Size,
                    DevicePixelFormats.Prgba128Float,
                    delegate (IDeviceContext dc)
                    {
                        dc.EffectBufferPrecision = BufferPrecision.Float32;
                        using IDeviceImage srcImage = dc.CreateImageFromBitmap(bitmap, null, BitmapImageOptions.UseStraightAlpha);
                        using IDeviceColorContext srcColorContext = dc.CreateColorContext(DxgiColorSpace.RgbFullGamma2084NoneP2020);
                        using IDeviceColorContext dstColorContext = dc.CreateColorContext(colorContext);

                        ColorManagementEffect colorMgmtEffect = new(
                            dc,
                            srcImage,
                            srcColorContext,
                            dstColorContext,
                            ColorManagementAlphaMode.Straight);

                        return colorMgmtEffect;
                    });
            }
        }

        private static void SetLayerColorDataFromSdrImage(IBitmap source, Surface destination)
        {
            using (IBitmapSource<ColorRgb24> convertedImage = source.CreateFormatConverter<ColorRgb24>())
            using (IBitmap<ColorRgb24> asBitmap = convertedImage.ToBitmap())
            {
                SetLayerColorDataFromRgbImage(asBitmap, destination);
            }
        }
    }
}
