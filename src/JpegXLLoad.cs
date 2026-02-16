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

using JpegXLFileTypePlugin.Exif;
using JpegXLFileTypePlugin.Interop;
using PaintDotNet;
using PaintDotNet.Direct2D1;
using PaintDotNet.Direct2D1.Effects;
using PaintDotNet.Dxgi;
using PaintDotNet.FileTypes;
using PaintDotNet.Imaging;
using System;
using System.IO;
using System.Linq;

namespace JpegXLFileTypePlugin
{
    internal static class JpegXLLoad
    {
        public static IFileTypeDocument Load(IFileTypeDocumentFactory factory, Stream input, IImagingFactory imagingFactory)
        {
            byte[] data = new byte[input.Length];
            input.ReadExactly(data, 0, data.Length);

            using (DecoderImage decoderImage = new(imagingFactory))
            {
                JpegXLNative.LoadImage(data, decoderImage);

                DecoderLayerData decoderLayerData = decoderImage.LayerData ?? throw new FormatException("The layer data was null.");
                IBitmap decoderLayerBitmap = decoderLayerData.Color;

                IColorContext? documentColorContext;
                IBitmapSource bitmapLayerSource;
                if (decoderImage.ColorSpace == JpegXLColorSpace.Rgb && decoderImage.HdrFormat == HdrFormat.PQ)
                {
                    if (decoderImage.ChannelRepresentation == JpegXLImageChannelRepresentation.Uint8)
                    {
                        throw new FormatException("PQ HDR images with 8-bit color channels are not supported.");
                    }

                    // For UINT16, use Display P3 since it has a similar gamut to the PQ color space and is designed for HDR content.
                    // For Float16/Float32, use scRGB and let PDN figure out the best way to handle it. It may convert to Display P3
                    // (e.g. v5.2 only supports BGRA32 SDR), or do something else that's appropriate.
                    documentColorContext = (decoderImage.ChannelRepresentation == JpegXLImageChannelRepresentation.Uint16)
                        ? imagingFactory.CreateColorContext(KnownColorSpace.DisplayP3)
                        : imagingFactory.CreateColorContext(KnownColorSpace.ScRgb);

                    bitmapLayerSource = decoderLayerBitmap.CreateColorTransformer(DxgiColorSpace.RgbFullGamma2084NoneP2020, documentColorContext, decoderLayerBitmap.PixelFormat);
                }
                else if (factory.SupportedPixelFormats.Contains(decoderLayerBitmap.PixelFormat))
                {
                    // This covers RGB, CMYK, and Gray (already converted to RGB by DecoderLayerData)
                    documentColorContext = decoderImage.TryGetColorContext();
                    bitmapLayerSource = decoderLayerBitmap.CreateRef();
                }
                else
                {
                    throw new FormatException($"Unsupported format: {decoderImage.ColorSpace}, {decoderImage.ChannelRepresentation}, {decoderImage.HdrFormat}");
                }

                IFileTypeDocument document = factory.CreateDocument(bitmapLayerSource.Size, bitmapLayerSource.PixelFormat);

                ExifValueCollection? exifValues = decoderImage.TryGetExif();
                if (exifValues != null)
                {
                    using (var exifTx = document.Metadata.Exif.CreateTransaction())
                    {
                        exifTx.SetItems(exifValues);
                    }
                }

                XmpPacket? xmpPacket = decoderImage.GetXmp();
                using (var xmpTx = document.Metadata.Xmp.CreateTransaction())
                {
                    xmpTx.XmpPacket = xmpPacket;
                }

                if (documentColorContext is not null)
                {
                    document.SetColorContext(documentColorContext);
                }

                using IFileTypeBitmapLayer bitmapLayer = document.CreateBitmapLayer();
                document.Layers.Add(bitmapLayer);

                if (!string.IsNullOrWhiteSpace(decoderLayerData.Name))
                {
                    bitmapLayer.Name = decoderLayerData.Name;
                }

                using IFileTypeBitmapSink bitmapLayerSink = bitmapLayer.GetBitmap();
                bitmapLayerSink.WriteSource(bitmapLayerSource);

                documentColorContext?.Dispose();
                bitmapLayerSource.Dispose();

                return document;
            }
        }
    }
}
