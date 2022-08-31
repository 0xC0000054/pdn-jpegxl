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

            using (DecoderLayerData layerData = new())
            using (DecoderImageMetadata imageMetadata = new())
            {
                JpegXLNative.LoadImage(data, layerData, imageMetadata);

                BitmapLayer? layer = layerData.Layer;

                if (layer is null)
                {
                    ExceptionUtil.ThrowInvalidOperationException("The layer is null.");
                }

                doc = new Document(layer.Width, layer.Height);

                AddMetadataToDocument(imageMetadata, doc);

                doc.Layers.Add(layer);
                layerData.OwnsLayer = false;
            }

            return doc;
        }

        private static unsafe void AddMetadataToDocument(DecoderImageMetadata imageMetadata, Document document)
        {
            byte[]? iccProfileBytes = imageMetadata.TryGetIccProfileBytes();

            if (iccProfileBytes != null)
            {
                ExifPropertyKey interColorProfile = ExifPropertyKeys.Image.InterColorProfile;

                document.Metadata.AddExifPropertyItem(interColorProfile.Path.Section,
                                                      interColorProfile.Path.TagID,
                                                      new ExifValue(ExifValueType.Undefined,
                                                                    iccProfileBytes));
            }
        }
    }
}
