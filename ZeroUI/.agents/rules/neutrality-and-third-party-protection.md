# Neutrality & Third-Party Trademark Protection Standard

## 1. Objective and Policy Scope
To protect the ZeroUI project from trademark infringement, copyright claims, and licensing disputes, this repository enforces a **Strict Neutrality Policy**:
- **Zero Competitor / Third-Party Project Naming:** Under NO circumstances should third-party commercial UI suites, competitor component vendors, or proprietary external projects (e.g. DevExpress, Syncfusion, Telerik, Infragistics, ComponentOne, GrapeCity, etc.) be named or cited anywhere in the project.
- **Scope of Enforcement:**
  1. Documentation files (`README.md`, `docs/**/*.md`).
  2. Roadmap, initiative, and proposal catalogs (`docs/proposals.md`, `docs/roadmap.md`).
  3. Source code files: Class docstrings (`/// <summary>`), inline comments (`// ...`), `[Description("...")]` attributes.
  4. User interface text: Window titles, card headers, labels, placeholders, dialog messages.
  5. Git commit messages and pull request descriptions.

---

## 2. Knowledge Extraction & Conceptual Modeling
- **Extract Concepts, Not Brands:** Agents and developers must learn and extract abstract engineering concepts (e.g. flyweight editing, DIBSection blitting, virtualized scrollbars, multi-column search popups, hierarchical BOM trees, WCAG contrast calculation) without associating them with external commercial product names.
- **Never Claim "Parity" with a Proprietary Brand:** Do not write "DevExpress parity", "XtraGrid benchmark", or "SpinEdit clone". Instead, frame features in terms of industrial standards and business requirements:
  - ❌ *DevExpress parity* &rarr; ✔️ *Standard enterprise data-entry ergonomics*
  - ❌ *XtraGrid Excel Filter* &rarr; ✔️ *Excel-style column popup filter*
  - ❌ *DXErrorProvider* &rarr; ✔️ *Vector validation error provider*
  - ❌ *SpinEdit* &rarr; ✔️ *High-precision numeric stepper*
  - ❌ *LookUpEdit* &rarr; ✔️ *Multi-column virtualized lookup dropdown*
  - ❌ *SplashScreenManager* &rarr; ✔️ *Thread-safe splash screen manager*
  - ❌ *WindowsFormsSettings* &rarr; ✔️ *Global UI configuration engine*
  - ❌ *DevExpress Skin Manager* &rarr; ✔️ *Dynamic palette & skin manager*

---

## 3. Pre-Commit Audit Requirement
Before committing code or documentation updates:
- Run a case-insensitive search across modified files for competitor brand names (`DevExpress`, `Syncfusion`, `Telerik`, `Infragistics`, etc.).
- Ensure all descriptions use neutral, professional, and open technical terminology.
