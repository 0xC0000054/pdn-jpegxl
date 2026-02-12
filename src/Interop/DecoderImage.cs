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
            HdrFormat = HdrFormat.None;
            setBasicInfoDelegate = SetBasicInfo;
            setIccProfileDelegate = SetIccProfile;
            setKnownColorProfileDelegate = SetKnownColorProfile;
            setExifDelegate = SetExif;
            setXmpDelegate = SetXmp;
            setLayerDataDelegate = SetLayerData;
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public JpegXLColorSpace ColorSpace { get; private set; }

        public JpegXLImageChannelRepresentation ChannelRepresentation { get; private set; }

        public HdrFormat HdrFormat { get; private set; }

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

            return ColorSpace switch
            {
                // Gray images are loaded as RGB.
                JpegXLColorSpace.Rgb => profileHeader.ColorSpace == ICCProfile.ProfileColorSpace.Rgb,
                JpegXLColorSpace.Cmyk => profileHeader.ColorSpace == ICCProfile.ProfileColorSpace.Cmyk,
                _ => false,
            };
        }

        private void SetBasicInfo(int canvasWidth,
                                  int canvasHeight,
                                  JpegXLColorSpace format,
                                  JpegXLImageChannelRepresentation channelRepresentation,
                                  bool hasTransparency)
        {
            Width = canvasWidth;
            Height = canvasHeight;
            ColorSpace = format;
            ChannelRepresentation = channelRepresentation;
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
                KnownColorSpace? colorSpace;

                if (profile == KnownColorProfile.Rec2020PQ)
                {
                    // We load Rec. 2020 PQ images as DisplayP3 after using Direct2D to remove the PQ curve.
                    colorSpace = KnownColorSpace.DisplayP3;
                    HdrFormat = HdrFormat.PQ;
                }
                else
                {
                    colorSpace = profile switch
                    {
                        // Gray images are loaded as RGB due to WIC having poor support for
                        // gray to RGB format conversions.
                        KnownColorProfile.Srgb or KnownColorProfile.GraySrgbTRC => KnownColorSpace.Srgb,
                        KnownColorProfile.LinearSrgb or KnownColorProfile.LinearGray => KnownColorSpace.ScRgb,
                        KnownColorProfile.DisplayP3 => KnownColorSpace.DisplayP3,
                        _ => null,
                    };
                }

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
                layerData = new DecoderLayerData(Width,
                                                 Height,
                                                 ColorSpace,
                                                 ChannelRepresentation,
                                                 hasTransparency,
                                                 imagingFactory!,
                                                 name,
                                                 nameLength,
                                                 pixels);
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
    }
}
