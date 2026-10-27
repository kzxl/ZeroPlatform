# Standard Technical English for UI Controls and Demos

## 1. Scope and Objective
All UI controls (WinForms, WPF) and demonstration suites within the ZeroUI repository must strictly use **Standard Technical English** for all text surfaces. This ensures international enterprise readiness, clean automated test assertions, and seamless AI agent processing.

## 2. Control Library Requirements
- **Default Property Values**: Any default text assigned to control fields (e.g., `Title`, `Subtitle`, `FooterText`, `SummaryText`, `StatusTag`, `EmptyText`, `Watermark`) must be written in Standard Technical English.
- **Design-Time Attributes**: All `[DefaultValue("...")]` attributes matching string properties must match the English default strings verbatim.
- **Built-in Pushbutton Captions & Glyphs**: Action labels on industrial controls (e.g. `ZeroAnnunciatorGrid`, `ZeroToolbar`, `ZeroModal`) must follow standard industrial terminology (e.g., `ACK`, `SILENCE`, `RESET`, `LAMP TEST`, `OK`, `Cancel`, `Retry`, `Confirm`).
- **No Bilingual Mixing**: Avoid mixing local language with English (e.g. do not write `Thông tin board (Board Information)` or `ACK (Xác nhận)`). Use purely `Board Information` and `ACK`.

## 3. Demo Applications and Benchmark Suites
- **Layouts and Card Titles**: All headers, step badges, card titles, section subtitles, and status tags in demo forms and views (e.g., `BenchmarkDemo`, `WpfDemo`) must be in concise Standard Technical English.
- **User Interactions**: Tooltips, toast notifications (`ZeroToast`), alert banners (`ZeroAlertBanner`), confirmation dialogs (`ZeroModal`), and prompt windows must display English messages.
- **Mock Data & Procedural Generators**:
  - Catalog templates, product names, item descriptions, component categories, and material codes must be generated in English.
  - Lifecycle and quality statuses (e.g. `Passed OQC`, `Pending IQC`, `SMT Feeding`, `QC Quarantine`, `Low Stock Warning`) must use standard industrial manufacturing English.
  - Filter logic, color lookups, and enum mappings must evaluate English status strings.

## 4. Codebase Hygiene & Verification
- Prior to committing UI changes or adding new demo views, verify that no non-ASCII or Vietnamese diacritics exist in user-facing UI strings across `src/`.
