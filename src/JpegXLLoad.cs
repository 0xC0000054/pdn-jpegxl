////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using JpegXLFileTypePlugin.Interop;
using PaintDotNet;
using PaintDotNet.Imaging;
using PaintDotNet.IO;
using System;
using System.IO;

namespace JpegXLFileTypePlugin
{
    internal static class JpegXLLoad
    {
        public static Document Load(Stream input)
        {
            Document doc;

            byte[] data = new byte[input.Length];
            input.ProperRead(data, 0, data.Length);

            using (SafeDecoderContext context = JpegXLNative.CreateDecoder())
            {
                JpegXLNative.DecodeFile(context, data, out DecoderImageInfo imageInfo);

                doc = new Document(imageInfo.width, imageInfo.height);

                AddMetadataToDocument(context, imageInfo, doc);

                BitmapLayer layer = Layer.CreateBackgroundLayer(imageInfo.width, imageInfo.height);

                JpegXLNative.CopyDecodedPixelsToSurface(context, layer.Surface);

                doc.Layers.Add(layer);
            }

            return doc;
        }

        private static unsafe void AddMetadataToDocument(SafeDecoderContext context,
                                                         DecoderImageInfo imageInfo,
                                                         Document document)
        {
            if (imageInfo.iccProfileSize > 0 && imageInfo.iccProfileSize <= int.MaxValue)
            {
                byte[] iccProfileBytes = GC.AllocateUninitializedArray<byte>((int)imageInfo.iccProfileSize);

                fixed (byte* data = iccProfileBytes)
                {
                    JpegXLNative.GetIccProfileData(context, data, imageInfo.iccProfileSize);
                }

                ExifPropertyKey interColorProfile = ExifPropertyKeys.Image.InterColorProfile;

                document.Metadata.AddExifPropertyItem(interColorProfile.Path.Section,
                                                      interColorProfile.Path.TagID,
                                                      new ExifValue(ExifValueType.Undefined,
                                                                    iccProfileBytes));
            }
        }
    }
}
