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

using PaintDotNet;
using PaintDotNet.Imaging;
using System;
using System.Text;

namespace JpegXLFileTypePlugin.Interop
{
    internal sealed unsafe class DecoderLayerData : Disposable
    {
        public DecoderLayerData(
            int width,
            int height,
            JpegXLColorSpace colorSpace,
            JpegXLImageChannelRepresentation channelRepresentation,
            bool hasTransparency,
            IImagingFactory imagingFactory,
            byte* name,
            nuint nameLength,
            byte* pixels)
        {
            Color = CopyToBitmap(width, height, colorSpace, channelRepresentation, hasTransparency, imagingFactory, pixels);

            if (nameLength > 0 && nameLength < int.MaxValue)
            {
                Name = Encoding.UTF8.GetString(name, (int)nameLength);
            }
            else
            {
                Name = string.Empty;
            }
        }

        public IBitmap Color { get; }

        public string Name { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Color?.Dispose();
            }

            base.Dispose(disposing);
        }

        private static IBitmap CopyToBitmap(int width,
                                            int height,
                                            JpegXLColorSpace colorSpace,
                                            JpegXLImageChannelRepresentation channelRepresentation,
                                            bool hasTransparency,
                                            IImagingFactory imagingFactory,
                                            byte* pixels)
        {
            System.Diagnostics.Debugger.Break();

            return (colorSpace, channelRepresentation, hasTransparency) switch
            {
                (JpegXLColorSpace.Rgb, JpegXLImageChannelRepresentation.Uint8, false) => CopyToBitmap<ColorRgb24>(width, height, pixels),
                (JpegXLColorSpace.Rgb, JpegXLImageChannelRepresentation.Uint8, true) => CopyToBitmap<ColorRgba32>(width, height, pixels),
                (JpegXLColorSpace.Rgb, JpegXLImageChannelRepresentation.Uint16, false) => CopyToBitmap<ColorRgb48>(width, height, pixels),
                (JpegXLColorSpace.Rgb, JpegXLImageChannelRepresentation.Uint16, true) => CopyToBitmap<ColorRgba64>(width, height, pixels),
                (JpegXLColorSpace.Rgb, JpegXLImageChannelRepresentation.Float16, false) => CopyToBitmap<ColorRgb48Half>(width, height, pixels),
                (JpegXLColorSpace.Rgb, JpegXLImageChannelRepresentation.Float16, true) => CopyToBitmap<ColorRgba64Half>(width, height, pixels),
                (JpegXLColorSpace.Rgb, JpegXLImageChannelRepresentation.Float32, false) => CopyToBitmap<ColorRgb96Float>(width, height, pixels),
                (JpegXLColorSpace.Rgb, JpegXLImageChannelRepresentation.Float32, true) => CopyToBitmap<ColorRgba128Float>(width, height, pixels),
                (JpegXLColorSpace.Gray, JpegXLImageChannelRepresentation.Uint8, false) => CopyGrayToRgbBitmap<ColorGray8, ColorRgb24>(width, height, pixels),
                (JpegXLColorSpace.Gray, JpegXLImageChannelRepresentation.Uint8, true) => CopyGrayAlphaToRgbaBitmap<ColorGenericXY16, ColorGray8, ColorAlpha8, ColorRgba32>(imagingFactory, width, height, pixels),
                (JpegXLColorSpace.Gray, JpegXLImageChannelRepresentation.Uint16, false) => CopyGrayToRgbBitmap<ColorGray16, ColorRgb48>(width, height, pixels),
                (JpegXLColorSpace.Gray, JpegXLImageChannelRepresentation.Uint16, true) => CopyGrayAlphaToRgbaBitmap<ColorGenericXY32, ColorGray16, ColorAlpha16, ColorRgba64>(imagingFactory, width, height, pixels),
                (JpegXLColorSpace.Gray, JpegXLImageChannelRepresentation.Float16, false) => CopyGrayToRgbBitmap<ColorGray16Half, ColorRgb48Half>(width, height, pixels),
                (JpegXLColorSpace.Gray, JpegXLImageChannelRepresentation.Float16, true) => CopyGrayAlphaToRgbaBitmap<ColorGenericXY32Half, ColorGray16Half, ColorAlpha16Half, ColorRgba64Half>(imagingFactory, width, height, pixels),
                (JpegXLColorSpace.Gray, JpegXLImageChannelRepresentation.Float32, false) => CopyGrayToRgbBitmap<ColorGray32Float, ColorRgb96Float>(width, height, pixels),
                (JpegXLColorSpace.Gray, JpegXLImageChannelRepresentation.Float32, true) => CopyGrayAlphaToRgbaBitmap<ColorGenericXY64Float, ColorGray32Float, ColorAlpha32Float, ColorRgba128Float>(imagingFactory, width, height, pixels),
                (JpegXLColorSpace.Cmyk, JpegXLImageChannelRepresentation.Uint8, false) => CopyToBitmap<ColorCmyk32>(width, height, pixels),
                (JpegXLColorSpace.Cmyk, JpegXLImageChannelRepresentation.Uint8, true) => CopyToBitmap<ColorCmyka40>(width, height, pixels),
                (JpegXLColorSpace.Cmyk, JpegXLImageChannelRepresentation.Uint16, false) => CopyToBitmap<ColorCmyk64>(width, height, pixels),
                (JpegXLColorSpace.Cmyk, JpegXLImageChannelRepresentation.Uint16, true) => CopyToBitmap<ColorCmyka80>(width, height, pixels),
                _ => throw new FormatException($"Unsupported color space {colorSpace} and channel representation {channelRepresentation} combination.")
            };
        }

        private static IBitmap<TPixel> CopyToBitmap<TPixel>(int width, int height, byte* pixels)
            where TPixel : unmanaged, INaturalPixelInfo
        {
            RegionPtr<TPixel> region = new RegionPtr<TPixel>((TPixel*)pixels, width, height, sizeof(TPixel) * width);
            using IBitmapSource<TPixel> regionBitmap = new RegionPtrBitmapSource<TPixel>(region);
            return regionBitmap.ToBitmap();
        }

        private static IBitmap<TPixelRgb> CopyGrayToRgbBitmap<TPixelGray, TPixelRgb>(int width, int height, byte* pixels)
            where TPixelGray : unmanaged, INaturalPixelInfo
            where TPixelRgb : unmanaged, INaturalPixelInfo
        {
            // WIC doesn't support Gray pixel formats, so we must transform to RGB by duplicating the Gray channels
            RegionPtr<TPixelGray> regionG = new RegionPtr<TPixelGray>((TPixelGray*)pixels, width, height, sizeof(TPixelGray) * width);
            using IBitmapSource<TPixelGray> regionGBitmap = new RegionPtrBitmapSource<TPixelGray>(regionG);
            using IBitmapSource<TPixelRgb> rgbBitmap = regionGBitmap.CreateFormatConverter<TPixelRgb>();
            return rgbBitmap.ToBitmap();
        }

        private static IBitmap<TPixelRgba> CopyGrayAlphaToRgbaBitmap<TPixelGrayAlpha, TPixelGray, TPixelAlpha, TPixelRgba>(
            IImagingFactory imagingFactory, 
            int width, 
            int height, 
            byte* pixels)
            where TPixelGrayAlpha : unmanaged, INaturalPixelInfo
            where TPixelGray : unmanaged, INaturalPixelInfo
            where TPixelAlpha : unmanaged, INaturalPixelInfo
            where TPixelRgba : unmanaged, INaturalPixelInfo
        {
            // WIC doesn't support Gray+Alpha pixel formats, so we must transform to RGBA by duplicating the Gray channels
            RegionPtr<TPixelGrayAlpha> regionGA = new RegionPtr<TPixelGrayAlpha>((TPixelGrayAlpha*)pixels, width, height, sizeof(TPixelGrayAlpha) * width);
            using IBitmapSource<TPixelGrayAlpha> regionGABitmap = new RegionPtrBitmapSource<TPixelGrayAlpha>(regionGA);
            using IBitmapSource<TPixelGray> grayBitmap = regionGABitmap.CreateChannelExtractor<TPixelGray>(0);
            using IBitmapSource<TPixelAlpha> alphaBitmap = regionGABitmap.CreateChannelExtractor<TPixelAlpha>(1);
            using IBitmapSource<TPixelRgba> rgbaBitmap = imagingFactory.CreateBitmapFromChannels<TPixelRgba>(grayBitmap, grayBitmap, grayBitmap, alphaBitmap);
            return rgbaBitmap.ToBitmap();
        }
    }
}
