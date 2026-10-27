using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPrimitives.Core.Identifiers;

namespace ZeroAsset.Curation
{
    public enum AssetFlag
    {
        Unflagged = 0,
        Pick = 1,
        Reject = -1
    }

    public enum AssetColorLabel
    {
        None = 0,
        Red = 1,
        Yellow = 2,
        Green = 3,
        Blue = 4,
        Purple = 5
    }

    /// <summary>
    /// Represents photographic curation metadata: Rating, Flag, Color Label, Keywords, and unique sortable <see cref="FastUlid"/> AssetId.
    /// Provides value equality and deep cloning.
    /// </summary>
    public sealed class AssetCuration : IEquatable<AssetCuration>
    {
        private int _rating;

        /// <summary>
        /// Gets or sets the unique, time-ordered 128-bit <see cref="FastUlid"/> identifier for this asset.
        /// </summary>
        public FastUlid AssetId { get; set; } = FastUlid.NewUlid();

        /// <summary>
        /// Star rating from 0 (unrated) to 5.
        /// </summary>
        public int Rating
        {
            get => _rating;
            set => _rating = value < 0 ? 0 : (value > 5 ? 5 : value);
        }

        public AssetFlag Flag { get; set; } = AssetFlag.Unflagged;
        public AssetColorLabel ColorLabel { get; set; } = AssetColorLabel.None;
        public List<string> Keywords { get; set; } = new List<string>();

        public AssetCuration() { }

        public AssetCuration(int rating, AssetFlag flag = AssetFlag.Unflagged, AssetColorLabel label = AssetColorLabel.None, IEnumerable<string>? keywords = null, FastUlid assetId = default)
        {
            Rating = rating;
            Flag = flag;
            ColorLabel = label;
            if (keywords != null) Keywords.AddRange(keywords);
            AssetId = assetId == default ? FastUlid.NewUlid() : assetId;
        }

        /// <summary>
        /// Creates a deep copy of this curation state.
        /// </summary>
        public AssetCuration Clone()
        {
            return new AssetCuration(Rating, Flag, ColorLabel, Keywords, AssetId);
        }

        public bool Equals(AssetCuration? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Rating == other.Rating &&
                   Flag == other.Flag &&
                   ColorLabel == other.ColorLabel &&
                   Keywords.SequenceEqual(other.Keywords);
        }

        public override bool Equals(object? obj) => Equals(obj as AssetCuration);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Rating;
                hash = hash * 31 + (int)Flag;
                hash = hash * 31 + (int)ColorLabel;
                foreach (var kw in Keywords)
                {
                    hash = hash * 31 + (kw != null ? kw.GetHashCode() : 0);
                }
                return hash;
            }
        }
    }
}
