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

using System;
using System.Buffers.Binary;
using System.Diagnostics;

namespace JpegXLFileTypePlugin.ICCProfile
{
    [DebuggerDisplay("{ToString(), nq}")]
    internal sealed class ProfileID : IEquatable<ProfileID>
    {
        private readonly UInt128 profileID;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileID"/> class.
        /// </summary>
        /// <param name="span">The span.</param>
        /// <exception cref="ArgumentException"><paramref name="span"/> must be at least 16 bytes in length.</exception>
        public ProfileID(ReadOnlySpan<byte> span)
        {
            profileID = BinaryPrimitives.ReadUInt128BigEndian(span);
        }

        public bool IsEmpty => profileID == 0;

        public override bool Equals(object? obj) => Equals(obj as ProfileID);

        public bool Equals(ProfileID? other) => other is not null && profileID.Equals(other.profileID);

        public override int GetHashCode() => unchecked(1584140826 + profileID.GetHashCode());

        public override string ToString()
        {
            return profileID.ToString("X");
        }

        public static bool operator ==(ProfileID left, ProfileID right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.profileID == right.profileID;
        }

        public static bool operator !=(ProfileID left, ProfileID right) => !(left == right);
    }
}
