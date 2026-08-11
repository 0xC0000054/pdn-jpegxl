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
                bool isHdrDocument = false;
                float? contentMaxLuminanceNits = null;

                CicpColorSpace? cicpColorSpace = decoderImage.CicpColorSpace;
                bool isHdrTransfer = cicpColorSpace.HasValue && cicpColorSpace.Value.TransferCharacteristics 
                    is CicpTransferCharacteristics.SmpteSt2084PQ 
                    or CicpTransferCharacteristics.AribStdB67Hlg;

                if (isHdrTransfer)
                {
                    CicpColorSpace cicp = cicpColorSpace!.Value;

                    if (decoderImage.ChannelRepresentation == JpegXLImageChannelRepresentation.Uint8)
                    {
                        throw new FormatException("HDR images with 8-bit color channels are not supported.");
                    }

                    // Decode the HDR (PQ/HLG) content to linear-light and keep it as an HDR document, instead of
                    // flattening it to SDR at load. The output is tagged with the linearized form of the gamut-
                    // appropriate working space (e.g. BT.2020 -> BT.2020 linear), and the document is flagged as
                    // HDR so Paint.NET tone-maps it when importing as or exporting to SDR.
                    using IColorContext recommendedColorContext = imagingFactory.CreateColorContext(cicp.RecommendedColorSpace);
                    documentColorContext = imagingFactory.TryCreateLinearizedColorContext(recommendedColorContext);
                    if (documentColorContext is null)
                    {
                        // If for some reason the color context can't be linearized (shouldn't be possible), fallback to scRGB.
                        documentColorContext = imagingFactory.CreateColorContext(CicpColorSpaces.ScRgb);
                    }

                    bitmapLayerSource = decoderLayerBitmap.CreateColorTransformer<ColorRgba128Float>(cicp, documentColorContext);
                    isHdrDocument = true;

                    // The JPEG XL intensity target is the peak content luminance (like an AVIF MaxCLL). Use it as the
                    // content light level when it is a specific HDR value; the PQ default of 10000 nits (the container
                    // maximum, not the actual content peak) is treated as unknown so that Paint.NET measures the peak
                    // itself.
                    float intensityTarget = decoderImage.IntensityTargetNits;
                    if (intensityTarget > 0.0f && intensityTarget < 10000.0f)
                    {
                        contentMaxLuminanceNits = intensityTarget;
                    }
                }
                else if (cicpColorSpace.HasValue &&
                    cicpColorSpace.Value.CanCreateColorContext &&
                    factory.SupportedPixelFormats.Contains(decoderLayerBitmap.PixelFormat))
                {
                    // SDR CICP + CanCreateColorContext: the pixels are already in this color space, so just tag them
                    // with a matching color profile synthesized from the CICP code points.
                    documentColorContext = imagingFactory.CreateColorContext(cicpColorSpace.Value);
                    bitmapLayerSource = decoderLayerBitmap.CreateRef();
                }
                else if (cicpColorSpace.HasValue &&
                    cicpColorSpace.Value.CanColorTransformFrom &&
                    factory.SupportedPixelFormats.Contains(decoderLayerBitmap.PixelFormat))
                {
                    // SDR CICP + CanColorTransformFrom: the pixels need to be transformed to a color space that is ICC compatible.
                    // This shouldn't happen in practice, but I'm including this for robustness.
                    documentColorContext = imagingFactory.CreateColorContext(cicpColorSpace.Value.RecommendedColorSpace);
                    bitmapLayerSource = decoderLayerBitmap.CreateColorTransformer(cicpColorSpace.Value, documentColorContext, decoderLayerBitmap.PixelFormat);
                }
                else if (factory.SupportedPixelFormats.Contains(decoderLayerBitmap.PixelFormat))
                {
                    // Gray (KnownColorProfile), an embedded ICC profile, or an untagged image: use whatever
                    // color context the decoder produced. This also covers CMYK (converted to RGB earlier).
                    documentColorContext = decoderImage.TryGetColorContext();
                    bitmapLayerSource = decoderLayerBitmap.CreateRef();
                }
                else
                {
                    if (cicpColorSpace.HasValue)
                    {
                        throw new FormatException($"Unsupported format: {decoderImage.ColorSpace}, {decoderImage.ChannelRepresentation}, CICP: {cicpColorSpace.Value}");
                    }
                    else
                    {
                        throw new FormatException($"Unsupported format: {decoderImage.ColorSpace}, {decoderImage.ChannelRepresentation}");
                    }
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

                if (isHdrDocument)
                {
                    using (var hdrTx = document.Metadata.Hdr.CreateTransaction())
                    {
                        hdrTx.IsHdrDocument = true;

                        // From the JPEG XL intensity target, when it was a specific value; otherwise null so
                        // Paint.NET measures the content peak itself. SceneReferredSdrWhiteLevelNits keeps its
                        // default (80 nits).
                        hdrTx.ContentMaxLuminanceNits = contentMaxLuminanceNits;
                    }
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
