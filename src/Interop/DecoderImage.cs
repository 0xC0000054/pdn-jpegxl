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
using PaintDotNet;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    internal sealed unsafe class DecoderImage : Disposable
    {
        private bool hasTransparency;
        private IImagingFactory? imagingFactory;
        private DecoderLayerData? layerData;
        private IColorContext? colorContext;
        private ExifValueCollection? exif;
        private XmpPacket? xmp;

        private readonly SetBasicInfoDelegate setBasicInfoDelegate;
        private readonly SetMetadataDelegate setIccProfileDelegate;
        private readonly SetKnownColorProfileDelegate setKnownColorProfileDelegate;
        private readonly SetMetadataDelegate setExifDelegate;
        private readonly SetMetadataDelegate setXmpDelegate;
        private readonly SetLayerDataDelegate setLayerDataDelegate;

        public DecoderImage(IImagingFactory imagingFactory)
        {
            hasTransparency = false;
            this.imagingFactory = imagingFactory.CreateRef();
            setBasicInfoDelegate = SetBasicInfo;
            setIccProfileDelegate = SetIccProfile;
            setKnownColorProfileDelegate = SetKnownColorProfile;
            setExifDelegate = SetExif;
            setXmpDelegate = SetXmp;
            setLayerDataDelegate = SetLayerData;
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public JpegXLImageFormat Format { get; private set; }

        public DecoderLayerData? LayerData => layerData;

        public IColorContext? TryGetColorContext() => colorContext;

        public ExifValueCollection? TryGetExif() => exif;

        public XmpPacket? GetXmp() => xmp;

        public ExceptionDispatchInfo? ExceptionInfo { get; private set; }

        public DecoderCallbacks GetDecoderCallbacks()
        {
            return new DecoderCallbacks
            {
                setBasicInfo = Marshal.GetFunctionPointerForDelegate(setBasicInfoDelegate),
                setIccProfile = Marshal.GetFunctionPointerForDelegate(setIccProfileDelegate),
                setKnownColorProfile = Marshal.GetFunctionPointerForDelegate(setKnownColorProfileDelegate),
                setExif = Marshal.GetFunctionPointerForDelegate(setExifDelegate),
                setXmp = Marshal.GetFunctionPointerForDelegate(setXmpDelegate),
                setLayerData = Marshal.GetFunctionPointerForDelegate(setLayerDataDelegate)
            };
        }

        private bool IccProfileMatchesImageType(ReadOnlySpan<byte> profileBytes)
        {
            ICCProfile.ProfileHeader profileHeader = new(profileBytes);

            return Format switch
            {
                // Gray images are loaded as RGB.
                JpegXLImageFormat.Rgb => profileHeader.ColorSpace == ICCProfile.ProfileColorSpace.Rgb,
                JpegXLImageFormat.Cmyk => profileHeader.ColorSpace == ICCProfile.ProfileColorSpace.Cmyk,
                _ => false,
            };
        }

        private void SetBasicInfo(int canvasWidth, int canvasHeight, JpegXLImageFormat format, bool hasTransparency)
        {
            Width = canvasWidth;
            Height = canvasHeight;
            Format = format;
            this.hasTransparency = hasTransparency;
        }

        private bool SetIccProfile(byte* data, nuint dataLength)
        {
            try
            {
                ReadOnlySpan<byte> profileBytes = new(data, checked((int)dataLength));

                colorContext = imagingFactory!.CreateColorContext(profileBytes);

                if (!IccProfileMatchesImageType(profileBytes))
                {
                    DisposableUtil.Free(ref colorContext);
                }
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                return false;
            }

            return true;
        }

        private bool SetKnownColorProfile(KnownColorProfile profile)
        {
            try
            {
                KnownColorSpace? colorSpace = profile switch
                {
                    // Gray images are loaded as RGB due to WIC having poor support for
                    // gray to RGB format conversions.
                    KnownColorProfile.Srgb or KnownColorProfile.GraySrgbTRC => KnownColorSpace.Srgb,
                    KnownColorProfile.LinearSrgb or KnownColorProfile.LinearGray => KnownColorSpace.ScRgb,
                    KnownColorProfile.DisplayP3 => KnownColorSpace.DisplayP3,
                    _ => null,
                };

                if (colorSpace.HasValue)
                {
                    colorContext = imagingFactory!.CreateColorContext(colorSpace.Value);
                }
                else
                {
                    string resourcePath = profile switch
                    {
                        KnownColorProfile.Rec2020Linear => $"{nameof(JpegXLFileTypePlugin)}.ColorProfiles.Rec2020-elle-V4-g10.icc",
                        KnownColorProfile.Rec709 => $"{nameof(JpegXLFileTypePlugin)}.ColorProfiles.Rec709-elle-V4-rec709.icc",
                        _ => throw new InvalidEnumArgumentException(nameof(profile), (int)profile, typeof(KnownColorProfile)),
                    };

                    using (Stream? stream = typeof(DecoderImage).Assembly.GetManifestResourceStream(resourcePath))
                    {
                        if (stream == null)
                        {
                            throw new FileNotFoundException(resourcePath);
                        }

                        byte[] bytes = new byte[checked((int)stream.Length)];
                        stream.ReadExactly(bytes);

                        colorContext = imagingFactory!.CreateColorContext(bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                return false;
            }

            return true;
        }

        private bool SetExif(byte* data, nuint dataLength)
        {
            try
            {
                using (UnmanagedMemoryStream stream = new(data, checked((long)dataLength)))
                {
                    exif = ExifParser.Parse(stream);

                    if (exif != null)
                    {
                        exif.Remove(ExifPropertyKeys.Image.InterColorProfile.Path);
                        // JPEG XL does not use the EXIF data for rotation.
                        exif.Remove(ExifPropertyKeys.Image.Orientation.Path);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                return false;
            }

            return true;
        }

        private bool SetLayerData(byte* pixels, byte* name, nuint nameLength)
        {
            try
            {
                JpegXLImageFormat format = Format;

                layerData = new DecoderLayerData(Width,
                                                 Height,
                                                 format,
                                                 hasTransparency,
                                                 imagingFactory!,
                                                 name,
                                                 nameLength);

                if (format == JpegXLImageFormat.Gray)
                {
                    if (hasTransparency)
                    {
                        SetGrayAlphaImageData(pixels, layerData);
                    }
                    else
                    {
                        SetGrayImageData(pixels, layerData);
                    }
                }
                else if (format == JpegXLImageFormat.Rgb)
                {
                    if (hasTransparency)
                    {
                        SetRgbaImageData(pixels, layerData);
                    }
                    else
                    {
                        SetRgbImageData(pixels, layerData);
                    }
                }
                else if (format == JpegXLImageFormat.Cmyk)
                {
                    if (hasTransparency)
                    {
                        SetCmykAlphaImageData(pixels, layerData);
                    }
                    else
                    {
                        SetCmykImageData(pixels, layerData);
                    }
                }
                else
                {
                    throw new InvalidEnumArgumentException(nameof(Format), (int)Format, typeof(JpegXLImageFormat));
                }
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                return false;
            }

            return true;
        }

        private bool SetXmp(byte* data, nuint dataLength)
        {
            try
            {
                if (xmp == null)
                {
                    using (UnmanagedMemoryStream stream = new(data, checked((long)dataLength)))
                    {
                        xmp = XmpPacket.TryParse(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                return false;
            }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposableUtil.Free(ref layerData);
                DisposableUtil.Free(ref imagingFactory);
            }

            base.Dispose(disposing);
        }

        private void SetCmykImageData(byte* srcScan0, DecoderLayerData layerData)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(layerData.Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = layerData.Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;
                    nuint srcStride = (nuint)(uint)width * 4;
                    RegionPtr<ColorCmyk32> color = new((ColorCmyk32*)colorBitmapLock.Buffer,
                                                       colorBitmapLock.Size,
                                                       colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = srcScan0 + ((srcRowOffset + (uint)y) * srcStride);
                        ColorCmyk32* colorDst = color.Rows[y].Ptr;

                        for (int x = 0; x < width; x++)
                        {
                            colorDst->C = src[0];
                            colorDst->M = src[1];
                            colorDst->Y = src[2];
                            colorDst->K = src[3];

                            src += 4;
                            colorDst++;
                        }
                    }
                }
            }
        }

        private void SetCmykAlphaImageData(byte* srcScan0, DecoderLayerData layerData)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(layerData.Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = layerData.Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock transparencyBitmapLock = layerData.Transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;
                    nuint srcStride = (nuint)(uint)width * 5;
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

                            src += 5;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }

        private void SetGrayImageData(byte* srcScan0, DecoderLayerData layerData)
        {
            // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
            // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.

            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(layerData.Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = layerData.Color.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;
                    nuint srcStride = (uint)width;

                    RegionPtr<ColorRgb24> color = new((ColorRgb24*)colorBitmapLock.Buffer,
                                                      colorBitmapLock.Size,
                                                      colorBitmapLock.BufferStride);

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = srcScan0 + ((srcRowOffset + (uint)y) * srcStride);
                        ColorRgb24* colorDst = color.Rows[y].Ptr;

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

        private void SetGrayAlphaImageData(byte* srcScan0, DecoderLayerData layerData)
        {
            // Gray images are loaded as RGB due to WIC having poor support for gray to RGB format conversions.
            // WIC was throwing an exception when trying to convert from a gray color profile to a RGB color profile.

            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(layerData.Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = layerData.Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock transparencyBitmapLock = layerData.Transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;

                    nuint srcStride = (nuint)(uint)width * 2;

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

                            src += 2;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }

        private void SetRgbImageData(byte* srcScan0, DecoderLayerData layerData)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(layerData.Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;

                using (IBitmapLock colorBitmapLock = layerData.Color.Lock(lockRect, BitmapLockOptions.Write))
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

        private void SetRgbaImageData(byte* srcScan0, DecoderLayerData layerData)
        {
            foreach (RectInt32 lockRect in BitmapUtil2.EnumerateLockRects(layerData.Color!))
            {
                uint srcRowOffset = (uint)lockRect.Top;
                using (IBitmapLock colorBitmapLock = layerData.Color.Lock(lockRect, BitmapLockOptions.Write))
                using (IBitmapLock transparencyBitmapLock = layerData.Transparency!.Lock(lockRect, BitmapLockOptions.Write))
                {
                    SizeInt32 size = colorBitmapLock.Size;
                    int width = size.Width;
                    int height = size.Height;
                    nuint srcStride = (nuint)(uint)width * 4;

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
                            colorDst->R = src[0];
                            colorDst->G = src[1];
                            colorDst->B = src[2];
                            transparencyDst->A = src[3];

                            src += 4;
                            colorDst++;
                            transparencyDst++;
                        }
                    }
                }
            }
        }
    }
}
