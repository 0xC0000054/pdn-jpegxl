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

using PaintDotNet;
using PaintDotNet.Imaging;
using System.ComponentModel;
using System.Text;

namespace JpegXLFileTypePlugin.Interop
{
    internal sealed class DecoderLayerData : Disposable
    {
        private IBitmap? color;
        private IBitmap<ColorAlpha8>? transparency;

        public unsafe DecoderLayerData(
            int width,
            int height,
            JpegXLImageFormat imageFormat,
            bool hasTransparency,
            IImagingFactory imagingFactory,
            byte* name,
            nuint nameLength)
        {
            var colorPixelFormat = imageFormat switch
            {
                // Gray images are loaded as RGB due to WIC having poor support for
                // gray to RGB format conversions.
                JpegXLImageFormat.Gray or JpegXLImageFormat.Rgb => PixelFormats.Rgb24,
                JpegXLImageFormat.Cmyk => PixelFormats.Cmyk32,
                _ => throw new InvalidEnumArgumentException(nameof(imageFormat), (int)imageFormat, typeof(JpegXLImageFormat)),
            };
            color = imagingFactory.CreateBitmap(width, height, colorPixelFormat);

            if (hasTransparency)
            {
                transparency = imagingFactory.CreateBitmap<ColorAlpha8>(width, height);
            }

            if (nameLength > 0 && nameLength < int.MaxValue)
            {
                Name = Encoding.UTF8.GetString(name, (int)nameLength);
            }
            else
            {
                Name = string.Empty;
            }
        }

        public IBitmap Color
        {
            get => color!;
        }

        public IBitmap<ColorAlpha8>? Transparency
        {
            get => transparency;
        }

        public string Name { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposableUtil.Free(ref color);
                DisposableUtil.Free(ref transparency);
            }

            base.Dispose(disposing);
        }
    }
}
