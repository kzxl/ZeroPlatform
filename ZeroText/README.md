# ZeroText

High-performance, zero-allocation Vietnamese text normalization, SEO slug generation, Circular 200 currency-to-words spelling, and master data validation (MST, CCCD, Phone) for .NET.

Part of the **ZeroPlatform** ecosystem (Tier 1: Compute & System).

## Features

- **Vietnamese Search Normalization**:
  - Diacritic-insensitive search keyword generation (`ToSearchKeyword`).
  - Zero heap allocation on inputs up to 256 characters via `stackalloc`, with pooled memory fallback.
  - SEO slug formatting (`ToSlug`).
  - Tone mark stripping while preserving letter case and symbols (`RemoveDiacritics`).
  - URL-safe string transfer (`UnSignedTransfer`).
- **Circular 200/2014/TT-BTC Currency-to-Words**:
  - Accounting-grade Vietnamese words for financial figures up to $10^{29}$ (`ToVnCurrencyWords`).
  - Dialect customizability: Northern ("nghìn", "linh") and Southern ("ngàn", "lẻ", "tư").
  - Support for whole number suffixes ("chẵn") and fractional subunits (xu, cents).
- **Enterprise Master Data Validation**:
  - Tax Identification Number (MST) Modulo 11 check digit verification and format normalization (Circular 105/2020/TT-BTC).
  - 12-digit Citizen Identity Card (CCCD) validation, century/birth year decoding (1900–2300), gender decoding, and 63-province administrative mapping.
  - Mobile phone number normalization across standard 10-digit telco prefixes (`03x`, `05x`, `07x`, `08x`, `09x`) and international E.164 formats (`+84`).
- **ZeroPrimitives Compatibility Shim**:
  - Preserves full binary and source compatibility for existing consumers migrating from `ZeroPrimitives.Validation`.

## Target Frameworks

- `.NET Standard 2.0`
- `.NET Framework 4.6.2`
- `.NET 8.0`

## License

MIT License. Copyright © 2026 Phong Võ.
