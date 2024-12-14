////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

namespace JpegXLFileTypePlugin.ICCProfile
{
    [DebuggerDisplay("{ToString(), nq}")]
    internal readonly struct ProfileSignature : IEquatable<ProfileSignature>
    {
        public ProfileSignature(ReadOnlySpan<byte> bytes) => Value = BinaryPrimitives.ReadUInt32BigEndian(bytes);

        public uint Value { get; }

        public override bool Equals(object? obj) => obj is ProfileSignature other && Equals(other);

        public bool Equals(ProfileSignature other) => Value == other.Value;

        public override int GetHashCode() => unchecked(-1937169414 + Value.GetHashCode());

        public override string ToString()
        {
           StringBuilder builder = new(32);

            uint value = Value;

            builder.Append('\'');

            for (int i = 0; i <= 3; i++)
            {
                int shift = BitConverter.IsLittleEndian ? (3 - i) * 8 : i * 8;

                uint c = (value >> shift) & 0xff;

                // Ignore any bytes that are not printable ASCII characters
                // because they can not be displayed in the debugger watch windows.

                if (c >= 0x20 && c <= 0x7e)
                {
                    builder.Append((char)c);
                }
            }

            _ = builder.AppendFormat("\' (0x{0:X8})", value);

            return builder.ToString();
        }

        public static bool operator ==(ProfileSignature left, ProfileSignature right) => left.Equals(right);

        public static bool operator !=(ProfileSignature left, ProfileSignature right) => !(left == right);
    }
}
