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
using PaintDotNet.Rendering;
using System;
using System.ComponentModel;
using System.Text;

namespace JpegXLFileTypePlugin.Interop
{
    internal sealed unsafe class DecoderLayerData : Disposable
    {
        private IBitmap<ColorAlpha8>? transparency;

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
            PixelFormat colorPixelFormat;

            if (colorSpace == JpegXLColorSpace.Rgb || colorSpace == JpegXLColorSpace.Gray)
            {
                // Gray images are loaded as RGB due to WIC having poor support for
                // gray to RGB format conversions.

                colorPixelFormat = channelRepresentation switch
                {
                    JpegXLImageChannelRepresentation.Uint8 => PixelFormats.Rgb24,
                    JpegXLImageChannelRepresentation.Uint16 => PixelFormats.Rgb48,
                    JpegXLImageChannelRepresentation.Float16 => PixelFormats.Rgb48Half,
                    JpegXLImageChannelRepresentation.Float32 => PixelFormats.Rgb96Float,
                    _ => throw new InvalidEnumArgumentException(nameof(channelRepresentation),
                                                                (int)channelRepresentation,
                                                                typeof(JpegXLImageChannelRepresentation)),
                };
            }
            else if (colorSpace == JpegXLColorSpace.Cmyk)
            {
                colorPixelFormat = channelRepresentation switch
                {
                    JpegXLImageChannelRepresentation.Uint8 => PixelFormats.Cmyk32,
                    JpegXLImageChannelRepresentation.Uint16 => PixelFormats.Cmyk64,
                    _ => throw new InvalidEnumArgumentException(nameof(channelRepresentation),
                                                                (int)channelRepresentation,
                                                                typeof(JpegXLImageChannelRepresentation)),
                };
            }
            else
            {
                throw new InvalidEnumArgumentException(nameof(colorSpace),
                                                       (int)colorSpace,
                                                       typeof(JpegXLColorSpace));
            }

            Color = imagingFactory.CreateBitmap(width, height, colorPixelFormat);

            if (hasTransparency)
            {
                transparency = imagingFactory.CreateBitmap<ColorAlpha8>(width, height);
            }

            switch (colorSpace)
            {
                case JpegXLColorSpace.Gray:
                    SetGrayImageData(pixels, channelRepresentation);
                    break;
                case JpegXLColorSpace.Rgb:
                    SetRgbImageData(pixels, channelRepresentation);
                    break;
                case JpegXLColorSpace.Cmyk:
                    SetCmykImageData(pixels, channelRepresentation);
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(colorSpace),
                                                           (int)colorSpace,
                                                           typeof(JpegXLColorSpace));
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

        public IBitmap Color { get; }

        public IBitmap<ColorAlpha8>? Transparency
        {
            get => transparency;
        }

        public string Name { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Color?.Dispose();
                DisposableUtil.Free(ref transparency);
            }

            base.Dispose(disposing);
        }

        private void SetCmykImageData(byte* srcScan0, JpegXLImageChannelRepresentation channelRepresentation)
        {
            if (transparency != null)
            {
                switch (channelRepresentation)
                {
                    case JpegXLImageChannelRepresentation.Uint8:
                        SetCmykAlphaUInt8ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Uint16:
                    case JpegXLImageChannelRepresentation.Float16:
                    case JpegXLImageChannelRepresentation.Float32:
                    default:
                        throw new InvalidEnumArgumentException(nameof(channelRepresentation),
                                                               (int)channelRepresentation,
                                                               typeof(JpegXLImageChannelRepresentation));
                }
            }
            else
            {
                switch (channelRepresentation)
                {
                    case JpegXLImageChannelRepresentation.Uint8:
                        SetCmykUInt8ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Uint16:
                    case JpegXLImageChannelRepresentation.Float16:
                    case JpegXLImageChannelRepresentation.Float32:
                    default:
                        throw new InvalidEnumArgumentException(nameof(channelRepresentation),
                                                               (int)channelRepresentation,
                                                               typeof(JpegXLImageChannelRepresentation));

                }
            }
        }

        private void SetCmykUInt8ImageData(byte* srcScan0)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint srcChannelCount = 4;
                    nuint srcStride = (nuint)(uint)width * srcChannelCount;
                    RegionPtr<ColorCmyk32> destRegion = new((ColorCmyk32*)colorBitmapLock.Buffer,
                                                            colorBitmapLock.Size,
                                                            colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = srcScan0 + ((srcRowOffset + (uint)y) * srcStride);
                        ColorCmyk32* colorDst = destRegion.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->C = src[0];
                            colorDst->M = src[1];
                            colorDst->Y = src[2];
                            colorDst->K = src[3];

                            src += srcChannelCount;
                            colorDst++;
                        }
                    }
                }
            }
        }

        private void SetCmykAlphaUInt8ImageData(byte* srcScan0)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock transparencyBitmapLock = transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint srcChannelCount = 5;
                    nuint srcStride = (nuint)(uint)width * srcChannelCount;
                    RegionPtr<ColorCmyk32> color = new((ColorCmyk32*)colorBitmapLock.Buffer,
                                                       colorBitmapLock.Size,
                                                       colorBitmapLock.BufferStride);
                    byte* transparencyScan0 = (byte*)transparencyBitmapLock.Buffer;
                    int transparencyStride = transparencyBitmapLock.BufferStride;

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = srcScan0 + ((srcRowOffset + (uint)y) * srcStride);
                        ColorCmyk32* colorDst = color.Rows[y].Ptr;
                        byte* transparencyDst = transparencyScan0 + (((long)y) * transparencyStride);

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->C = src[0];
                            colorDst->M = src[1];
                            colorDst->Y = src[2];
                            colorDst->K = src[3];
                            *transparencyDst = src[4];

                            src += srcChannelCount;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }

        private void SetGrayImageData(byte* srcScan0, JpegXLImageChannelRepresentation channelRepresentation)
        {
            if (transparency != null)
            {
                switch (channelRepresentation)
                {
                    case JpegXLImageChannelRepresentation.Uint8:
                        SetGrayAlphaUInt8ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Uint16:
                        SetGrayAlphaUInt16ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Float16:
                        SetGrayAlphaFloat16ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Float32:
                        SetGrayAlphaFloat32ImageData(srcScan0);
                        break;
                    default:
                        throw new InvalidEnumArgumentException(nameof(channelRepresentation),
                                                               (int)channelRepresentation,
                                                               typeof(JpegXLImageChannelRepresentation));
                }
            }
            else
            {
                switch (channelRepresentation)
                {
                    case JpegXLImageChannelRepresentation.Uint8:
                        SetGrayUInt8ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Uint16:
                        SetGrayUInt16ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Float16:
                        SetGrayFloat16ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Float32:
                        SetGrayFloat32ImageData(srcScan0);
                        break;
                    default:
                        throw new InvalidEnumArgumentException(nameof(channelRepresentation),
                                                               (int)channelRepresentation,
                                                               typeof(JpegXLImageChannelRepresentation));

                }
            }
        }

        private void SetGrayUInt8ImageData(byte* srcScan0)
        {
            // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
            // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.

            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;
                    nuint srcStride = (uint)width;

                    RegionPtr<ColorRgb24> destRegion = new((ColorRgb24*)colorBitmapLock.Buffer,
                                                           colorBitmapLock.Size,
                                                           colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = srcScan0 + ((srcRowOffset + (uint)y) * srcStride);
                        ColorRgb24* colorDst = destRegion.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = colorDst->G = colorDst->B = src[0];

                            src++;
                            colorDst++;
                        }
                    }
                }
            }
        }

        private void SetGrayAlphaUInt8ImageData(byte* srcScan0)
        {
            // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
            // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.

            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock transparencyBitmapLock = transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint srcChannelCount = 2;
                    nuint srcStride = (nuint)(uint)width * srcChannelCount;

                    RegionPtr<ColorRgb24> color = new((ColorRgb24*)colorBitmapLock.Buffer,
                                                      colorBitmapLock.Size,
                                                      colorBitmapLock.BufferStride);
                    RegionPtr<ColorAlpha8> transparency = new((ColorAlpha8*)transparencyBitmapLock.Buffer,
                                                              transparencyBitmapLock.Size,
                                                              transparencyBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = srcScan0 + ((srcRowOffset + (uint)y) * srcStride);
                        ColorRgb24* colorDst = color.Rows[y].Ptr;
                        ColorAlpha8* transparencyDst = transparency.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = colorDst->G = colorDst->B = src[0];
                            transparencyDst->A = src[1];

                            src += srcChannelCount;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }

        private void SetGrayUInt16ImageData(byte* srcScan0)
        {
            // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
            // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.

            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;
                    nuint srcStride = (uint)width * 2;

                    RegionPtr<ColorRgb48> destRegion = new((ColorRgb48*)colorBitmapLock.Buffer,
                                                           colorBitmapLock.Size,
                                                           colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        ushort* src = (ushort*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb48* colorDst = destRegion.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = colorDst->G = colorDst->B = src[0];

                            src++;
                            colorDst++;
                        }
                    }
                }
            }
        }

        private void SetGrayAlphaUInt16ImageData(byte* srcScan0)
        {
            // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
            // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.

            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock transparencyBitmapLock = Transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint srcChannelCount = 2;
                    nuint srcStride = (nuint)(uint)width * srcChannelCount * 2;

                    RegionPtr<ColorRgb48> color = new((ColorRgb48*)colorBitmapLock.Buffer,
                                                      colorBitmapLock.Size,
                                                      colorBitmapLock.BufferStride);
                    RegionPtr<ColorAlpha8> transparency = new((ColorAlpha8*)transparencyBitmapLock.Buffer,
                                                              transparencyBitmapLock.Size,
                                                              transparencyBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        ushort* src = (ushort*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb48* colorDst = color.Rows[y].Ptr;
                        ColorAlpha8* transparencyDst = transparency.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = colorDst->G = colorDst->B = src[0];
                            transparencyDst->A = TransparencyMapping.ToEightBit(src[1]);

                            src += srcChannelCount;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }

        private void SetGrayFloat16ImageData(byte* srcScan0)
        {
            // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
            // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.

            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;
                    nuint srcStride = (uint)width * 2;

                    RegionPtr<ColorRgb48Half> destRegion = new((ColorRgb48Half*)colorBitmapLock.Buffer,
                                                               colorBitmapLock.Size,
                                                               colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        Half* src = (Half*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb48Half* colorDst = destRegion.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = colorDst->G = colorDst->B = src[0];

                            src++;
                            colorDst++;
                        }
                    }
                }
            }
        }

        private void SetGrayAlphaFloat16ImageData(byte* srcScan0)
        {
            // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
            // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.

            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock<ColorAlpha8> transparencyBitmapLock = Transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint srcChannelCount = 2;
                    nuint srcStride = (nuint)(uint)width * srcChannelCount * 2;

                    RegionPtr<ColorRgb48Half> color = new((ColorRgb48Half*)colorBitmapLock.Buffer,
                                                      colorBitmapLock.Size,
                                                      colorBitmapLock.BufferStride);
                    RegionPtr<ColorAlpha8> transparency = transparencyBitmapLock.AsRegionPtr();

                    for (int y = 0; y < height; y++)
                    {
                        Half* src = (Half*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb48Half* colorDst = color.Rows[y].Ptr;
                        ColorAlpha8* transparencyDst = transparency.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = colorDst->G = colorDst->B = src[0];
                            transparencyDst->A = TransparencyMapping.ToEightBit(src[1]);

                            src += srcChannelCount;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }

        private void SetGrayFloat32ImageData(byte* srcScan0)
        {
            // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
            // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.

            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;
                    nuint srcStride = (uint)width * 4;

                    RegionPtr<ColorRgb96Float> destRegion = new((ColorRgb96Float*)colorBitmapLock.Buffer,
                                                                colorBitmapLock.Size,
                                                                colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        float* src = (float*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb96Float* colorDst = destRegion.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = colorDst->G = colorDst->B = src[0];

                            src++;
                            colorDst++;
                        }
                    }
                }
            }
        }

        private void SetGrayAlphaFloat32ImageData(byte* srcScan0)
        {
            // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
            // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.

            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock<ColorAlpha8> transparencyBitmapLock = Transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint srcChannelCount = 2;
                    nuint srcStride = (nuint)(uint)width * srcChannelCount * 4;

                    RegionPtr<ColorRgb96Float> color = new((ColorRgb96Float*)colorBitmapLock.Buffer,
                                                      colorBitmapLock.Size,
                                                      colorBitmapLock.BufferStride);
                    RegionPtr<ColorAlpha8> transparency = transparencyBitmapLock.AsRegionPtr();

                    for (int y = 0; y < height; y++)
                    {
                        float* src = (float*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb96Float* colorDst = color.Rows[y].Ptr;
                        ColorAlpha8* transparencyDst = transparency.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = colorDst->G = colorDst->B = src[0];
                            transparencyDst->A = TransparencyMapping.ToEightBit(src[1]);

                            src += srcChannelCount;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }

        private void SetRgbImageData(byte* srcScan0, JpegXLImageChannelRepresentation channelRepresentation)
        {
            if (transparency != null)
            {
                switch (channelRepresentation)
                {
                    case JpegXLImageChannelRepresentation.Uint8:
                        SetRgbaUInt8ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Uint16:
                        SetRgbaUInt16ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Float16:
                        SetRgbaFloat16ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Float32:
                        SetRgbaFloat32ImageData(srcScan0);
                        break;
                    default:
                        throw new InvalidEnumArgumentException(nameof(channelRepresentation),
                                                               (int)channelRepresentation,
                                                               typeof(JpegXLImageChannelRepresentation));
                }
            }
            else
            {
                switch (channelRepresentation)
                {
                    case JpegXLImageChannelRepresentation.Uint8:
                        SetRgbUInt8ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Uint16:
                        SetRgbUInt16ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Float16:
                        SetRgbFloat16ImageData(srcScan0);
                        break;
                    case JpegXLImageChannelRepresentation.Float32:
                        SetRgbFloat32ImageData(srcScan0);
                        break;
                    default:
                        throw new InvalidEnumArgumentException(nameof(channelRepresentation),
                                                               (int)channelRepresentation,
                                                               typeof(JpegXLImageChannelRepresentation));

                }
            }
        }

        private void SetRgbUInt8ImageData(byte* srcScan0)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;
                    nuint srcStride = (nuint)(uint)width * 3;

                    RegionPtr<ColorRgb24> color = new((ColorRgb24*)colorBitmapLock.Buffer,
                                                      colorBitmapLock.Size,
                                                      colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = srcScan0 + ((srcRowOffset + (uint)y) * srcStride);
                        ColorRgb24* colorDst = color.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = src[0];
                            colorDst->G = src[1];
                            colorDst->B = src[2];

                            src += 3;
                            colorDst++;
                        }
                    }
                }
            }
        }

        private void SetRgbaUInt8ImageData(byte* srcScan0)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock<ColorAlpha8> transparencyBitmapLock = Transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint channelCount = 4;

                    nuint srcStride = (nuint)(uint)width * channelCount;

                    RegionPtr<ColorRgb24> color = new((ColorRgb24*)colorBitmapLock.Buffer,
                                                      colorBitmapLock.Size,
                                                      colorBitmapLock.BufferStride);
                    RegionPtr<ColorAlpha8> transparency = transparencyBitmapLock.AsRegionPtr();

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = srcScan0 + ((srcRowOffset + (uint)y) * srcStride);
                        ColorRgb24* colorDst = color.Rows[y].Ptr;
                        ColorAlpha8* transparencyDst = transparency.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = src[0];
                            colorDst->G = src[1];
                            colorDst->B = src[2];
                            transparencyDst->A = src[3];

                            src += channelCount;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }

        private void SetRgbUInt16ImageData(byte* srcScan0)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint channelCount = 3;

                    nuint srcStride = (nuint)(uint)width * channelCount * 2;

                    RegionPtr<ColorRgb48> color = new((ColorRgb48*)colorBitmapLock.Buffer,
                                                      colorBitmapLock.Size,
                                                      colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        ushort* src = (ushort*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb48* colorDst = color.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = src[0];
                            colorDst->G = src[1];
                            colorDst->B = src[2];

                            src += channelCount;
                            colorDst++;
                        }
                    }
                }
            }
        }

        private void SetRgbaUInt16ImageData(byte* srcScan0)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock transparencyBitmapLock = Transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint channelCount = 4;

                    nuint srcStride = (nuint)(uint)width * channelCount * 2;

                    RegionPtr<ColorRgb48> color = new((ColorRgb48*)colorBitmapLock.Buffer,
                                                      colorBitmapLock.Size,
                                                      colorBitmapLock.BufferStride);
                    RegionPtr<ColorAlpha8> transparency = new((ColorAlpha8*)transparencyBitmapLock.Buffer,
                                                              transparencyBitmapLock.Size,
                                                              transparencyBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        ushort* src = (ushort*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb48* colorDst = color.Rows[y].Ptr;
                        ColorAlpha8* transparencyDst = transparency.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = src[0];
                            colorDst->G = src[1];
                            colorDst->B = src[2];
                            transparencyDst->A = TransparencyMapping.ToEightBit(src[3]);

                            src += channelCount;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }

        private void SetRgbFloat16ImageData(byte* srcScan0)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint channelCount = 3;

                    nuint srcStride = (nuint)(uint)width * channelCount * 2;

                    RegionPtr<ColorRgb48Half> color = new((ColorRgb48Half*)colorBitmapLock.Buffer,
                                                          colorBitmapLock.Size,
                                                          colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        Half* src = (Half*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb48Half* colorDst = color.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = src[0];
                            colorDst->G = src[1];
                            colorDst->B = src[2];

                            src += channelCount;
                            colorDst++;
                        }
                    }
                }
            }
        }

        private void SetRgbaFloat16ImageData(byte* srcScan0)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock<ColorAlpha8> transparencyBitmapLock = Transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;


                    const uint channelCount = 4;
                    nuint srcStride = (nuint)(uint)width * channelCount * 2;

                    RegionPtr<ColorRgb48Half> color = new((ColorRgb48Half*)colorBitmapLock.Buffer,
                                                      colorBitmapLock.Size,
                                                      colorBitmapLock.BufferStride);
                    RegionPtr<ColorAlpha8> transparency = transparencyBitmapLock.AsRegionPtr();

                    for (int y = 0; y < height; y++)
                    {
                        Half* src = (Half*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb48Half* colorDst = color.Rows[y].Ptr;
                        ColorAlpha8* transparencyDst = transparency.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = src[0];
                            colorDst->G = src[1];
                            colorDst->B = src[2];
                            transparencyDst->A = TransparencyMapping.ToEightBit(src[3]);

                            src += channelCount;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }

        private void SetRgbFloat32ImageData(byte* srcScan0)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint channelCount = 3;

                    nuint srcStride = (nuint)(uint)width * channelCount * 4;

                    RegionPtr<ColorRgb96Float> color = new((ColorRgb96Float*)colorBitmapLock.Buffer,
                                                           colorBitmapLock.Size,
                                                           colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        float* src = (float*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb96Float* colorDst = color.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = src[0];
                            colorDst->G = src[1];
                            colorDst->B = src[2];

                            src += channelCount;
                            colorDst++;
                        }
                    }
                }
            }
        }

        private void SetRgbaFloat32ImageData(byte* srcScan0)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock<ColorAlpha8> transparencyBitmapLock = Transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    const uint channelCount = 4;

                    nuint srcStride = (nuint)(uint)width * channelCount * 4;

                    RegionPtr<ColorRgb96Float> color = new((ColorRgb96Float*)colorBitmapLock.Buffer,
                                                           colorBitmapLock.Size,
                                                           colorBitmapLock.BufferStride);
                    RegionPtr<ColorAlpha8> transparency = transparencyBitmapLock.AsRegionPtr();

                    for (int y = 0; y < height; y++)
                    {
                        float* src = (float*)(srcScan0 + ((srcRowOffset + (uint)y) * srcStride));
                        ColorRgb96Float* colorDst = color.Rows[y].Ptr;
                        ColorAlpha8* transparencyDst = transparency.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->R = src[0];
                            colorDst->G = src[1];
                            colorDst->B = src[2];
                            transparencyDst->A = TransparencyMapping.ToEightBit(src[3]);

                            src += channelCount;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }
    }
}
