using System;

namespace ZeroVector.Core.Results
{
    /// <summary>
    /// Represents an immutable top-k search result containing the matched item identifier and its similarity/distance score.
    /// </summary>
    public readonly struct VectorSearchResult : IComparable<VectorSearchResult>, IEquatable<VectorSearchResult>
    {
        public int Id { get; }
        public float Score { get; }

        public VectorSearchResult(int id, float score)
        {
            Id = id;
            Score = score;
        }

        public int CompareTo(VectorSearchResult other) => Score.CompareTo(other.Score);

        public bool Equals(VectorSearchResult other) => Id == other.Id && Math.Abs(Score - other.Score) < 1e-6f;

        public override bool Equals(object? obj) => obj is VectorSearchResult other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id * 397) ^ Score.GetHashCode();
            }
        }

        public static bool operator ==(VectorSearchResult left, VectorSearchResult right) => left.Equals(right);
        public static bool operator !=(VectorSearchResult left, VectorSearchResult right) => !left.Equals(right);

        public override string ToString() => $"VectorSearchResult(Id={Id}, Score={Score:F4})";
    }
}
