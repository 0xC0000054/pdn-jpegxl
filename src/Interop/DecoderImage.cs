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
        private readonly SetCicpColorInfoDelegate setCicpColorInfoDelegate;
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
            setCicpColorInfoDelegate = SetCicpColorInfo;
            setExifDelegate = SetExif;
            setXmpDelegate = SetXmp;
            setLayerDataDelegate = SetLayerData;
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public JpegXLColorSpace ColorSpace { get; private set; }

        public JpegXLImageChannelRepresentation ChannelRepresentation { get; private set; }

        // The image's color space as CICP code points, if the color encoding maps to one (RGB encodings that
        // are describable by ITU-T H.273). Null for gray / ICC-profile / untagged images, which use
        // TryGetColorContext instead.
        public CicpColorSpace? CicpColorSpace { get; private set; }

        // The HDR intensity target (peak luminance in nits) from JxlBasicInfo.intensity_target, meaningful
        // only when CicpColorSpace is set. Zero if CicpColorSpace was not set.
        public float IntensityTargetNits { get; private set; }

        public DecoderLayerData? LayerData => layerData;

        public IColorContext? TryGetColorContext() => colorContext?.CreateRef();

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
                setCicpColorInfo = Marshal.GetFunctionPointerForDelegate(setCicpColorInfoDelegate),
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
                // The native decoder reports RGB color encodings via SetCicpColorInfo, so only the two gray
                // encodings still arrive here. Gray images are loaded as RGB (WIC has poor gray-to-RGB support)
                // and tagged with the matching sRGB / linear (scRGB) color context.
                KnownColorSpace colorSpace = profile switch
                {
                    KnownColorProfile.GraySrgbTRC => KnownColorSpace.Srgb,
                    KnownColorProfile.LinearGray => KnownColorSpace.ScRgb,
                    _ => throw new InvalidEnumArgumentException(nameof(profile), (int)profile, typeof(KnownColorProfile)),
                };

                colorContext = imagingFactory!.CreateColorContext(colorSpace);
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                return false;
            }

            return true;
        }

        private SetCicpColorInfoResult SetCicpColorInfo(
            byte colorPrimaries,
            byte transferCharacteristics,
            byte matrixCoefficients,
            byte videoFullRangeFlag,
            float intensityTargetNits)
        {
            try
            {
                CicpColorSpace cicp = new(
                    (CicpColorPrimaries)colorPrimaries,
                    (CicpTransferCharacteristics)transferCharacteristics,
                    (CicpMatrixCoefficients)matrixCoefficients,
                    (CicpVideoFullRangeFlag)videoFullRangeFlag);

                if (!cicp.CanCreateColorContext && !cicp.CanColorTransformFrom)
                {
                    // PDN can't work with this CICP color space. Have the native decoder send
                    // the ICC profile instead; otherwise the image would be loaded with no
                    // color context and treated as sRGB.
                    return SetCicpColorInfoResult.Unsupported;
                }

                if (ChannelRepresentation == JpegXLImageChannelRepresentation.Uint8 &&
                    cicp.TransferCharacteristics
                        is CicpTransferCharacteristics.SmpteSt2084PQ
                        or CicpTransferCharacteristics.AribStdB67Hlg)
                {
                    // 8-bit HDR is not supported as an HDR document. Have the native decoder send the
                    // ICC profile instead, so the image loads as SDR.
                    return SetCicpColorInfoResult.Unsupported;
                }

                CicpColorSpace = cicp;
                IntensityTargetNits = intensityTargetNits;
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                return SetCicpColorInfoResult.Error;
            }

            return SetCicpColorInfoResult.Ok;
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
                DisposableUtil.Free(ref colorContext);
                DisposableUtil.Free(ref imagingFactory);
            }

            base.Dispose(disposing);
        }
    }
}
