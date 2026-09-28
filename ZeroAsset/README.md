# 📦 ZeroAsset

**High-Performance Digital Asset Management, Zero-Byte Variant Branching, and Hierarchical Curation Engine for .NET.**

Part of the **ZeroUniverse** ecosystem (`ZeroPlatform`), `ZeroAsset` provides sovereign, zero-allocation algorithms and data structures for managing media assets, virtual copies/variants, two-way sidecar synchronization, contiguous master-variant sorting, and conflict-free content-addressable cache keys.

## Features

- **Zero-Byte Variant Branching**: Canonical `#vc<n>` suffix parser, disk path resolver, and monotonic copy ID allocation.
- **Hierarchical Contiguous Sorting**: Strict Weak Ordering comparator guaranteeing that child variants/virtual copies always group contiguously adjacent to their master parent in any sort mode (Date, Name, Rating, Size, Custom).
- **Asset Curation**: Rating (0-5 stars), Color labels, Flags (Pick/Reject), Keyword tags with zero-allocation immutable cloning.
- **Content-Addressable Cache Keys**: Cryptographic cache key generation isolating variants from master assets to prevent thumbnail or proxy collisions.
