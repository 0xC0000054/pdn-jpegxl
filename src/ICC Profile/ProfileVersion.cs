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
using System.Globalization;

namespace JpegXLFileTypePlugin.ICCProfile
{
    [DebuggerDisplay("{ToString(), nq}")]
    internal readonly struct ProfileVersion : IEquatable<ProfileVersion>
    {
        private readonly uint packedVersion;

        public ProfileVersion(ReadOnlySpan<byte> bytes) => packedVersion = BinaryPrimitives.ReadUInt32BigEndian(bytes);

        public uint Major => (packedVersion >> 24) & 0xff;

        public uint Minor => HighNibble(packedVersion >> 16);

        public uint Fix => LowNibble(packedVersion >> 16);

        public override bool Equals(object? obj) => obj is ProfileVersion other && Equals(other);

        public bool Equals(ProfileVersion other) => packedVersion == other.packedVersion;

        public override int GetHashCode() => unchecked(-1289286301 + packedVersion.GetHashCode());

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", Major, Minor, Fix);

        public static bool operator ==(ProfileVersion left, ProfileVersion right) => left.Equals(right);

        public static bool operator !=(ProfileVersion left, ProfileVersion right) => !(left == right);

        private static uint HighNibble(uint value) => (value >> 4) & 0xf;

        private static uint LowNibble(uint value) => value & 0xf;
    }
}
