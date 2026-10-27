# ZeroGraphics: Machine Vision, Metrology & Inspection

High-precision, pure C# industrial computer vision engine designed for Automated Optical Inspection (AOI), quality control (QC), robotics guidance, and metrology.

---

## 1. Pattern Matching & Alignment

### Normalized Cross-Correlation (NCC) Template Matching
- **Illumination Invariant**: Matches patterns under arbitrary linear illumination and contrast variations.
- **Fast Candidate Search**: Uses $O(1)$ Integral and Squared Integral tables for rapid multi-scale candidate bounding.
- **Sub-Pixel Precision**: Applies 2D parabolic surface interpolation around correlation peaks to achieve $< 0.05$ pixel localization error.

### 2-Point Pose Alignment (`PoseAligner`)
- Computes rigid affine transformation (rotation $\Delta \theta$, translational offset $\Delta x, \Delta y$, and scaling factor) between CAD nominal coordinates and measured camera fiducials.
- Used in automated pick-and-place robotics and high-speed PCB surface mount inspection.

---

## 2. Dimensional Metrology & Edge Inspection

### 1D Sub-Pixel Edge Caliper (Rake)
- Casts projection rays along arbitrary line segments and orientation angles.
- Bilinear profile sampling with first-derivative Gaussian convolution.
- Sub-pixel edge position determined via polynomial peak interpolation.

### Geometric Orthogonal Fitting
- **Total Least Squares (TLS)**: Orthogonal distance line regression minimizing perpendicular errors (unlike standard OLS which only minimizes vertical distance).
- **Taubin Circle Fit**: Algebraic, unbiased, non-iterative circular regression for hole diameter and concentricity verification.
- **Fitzgibbon Ellipse Fit**: Solves the generalized eigenvalue problem for slanted drilled holes and bevel surfaces.

### GD&T and RANSAC Outlier Rejection (`RansacFitter`)
- Random Sample Consensus (RANSAC) rejecting up to 60% outliers caused by dust, chips, burrs, or lighting glares.
- **Graham Scan Convex Hull**: $O(N \log N)$ 2D polygon hull calculation.
- **Rotating Calipers Minimum OBB**: Computes optimal minimum-area Oriented Bounding Box for component bounding and rotational orientation.

---

## 3. Connected Component Analysis (Blob)

- **Fast Two-Pass CCL**: 8-way connected component labeling using Disjoint Set Union (DSU) trees.
- **Geometric Descriptors**: Computes bounding box, pixel area, centroid $(C_x, C_y)$, perimeter, equivalent circular diameter, and circularity compactness.
- Fast filtering of particles, pinholes, and foreign material contamination.

---

## 4. Industrial 1D/2D Barcode Engine (`ZeroGraphics.Vision.Codes`)

- **1D Barcodes**: Complete encoders and decoders for Code 128 (Sets A, B, C with auto-switching and Mod-103 checksum) and Code 39 (with Mod-43 checksum).
- **2D Matrix Barcodes**: QR Code Model 2 and DataMatrix ECC200 encoding and decoding with perimeter finder localization.
- **Polynomial Reed-Solomon Correction**: Galois Field $GF(2^8)$ arithmetic with Berlekamp-Massey syndrome decoding and Forney algorithm, repairing scratched or partially obscured direct part markings (DPM).

---

## 5. Multi-View Homography & Perspective Stitching

- **Planar Homography (`Homography2D`)**: Direct Linear Transform (DLT) 8-DOF matrix decomposition mapping arbitrary quad viewports.
- **Inverse Perspective Warping (`PerspectiveWarper`)**: Sub-pixel bilinear warping to rectify oblique camera angles.
- **Continuous Web Stitching (`ImageStitcher`)**: Wide-area panorama generation for conveyor belt inspection.

---

## 6. Color Metrology (`ZeroGraphics.Imaging.ColorScience`)

- **CIE L\*a\*b\* Color Space**: Standard D65 white point adaptation and sRGB gamma companding.
- **CIEDE2000 ($\Delta E_{00}$)**: Industrial color tolerance standard accounting for lightness, chroma, and hue weighting factors ($S_L, S_C, S_H$) and the neutral rotation term $R_T$.

---

## 7. Statistical Process Control (`ZeroGraphics.Core.Analytics`)

- Real-time computation of Mean, Standard Deviation ($\sigma$), Control Limits ($CL$, $UCL$, $LCL$), and Capability Indices ($Cp$, $Cpk$).
- Automated detection of **Western Electric & Nelson Rules**:
  - Rule 1: 1 point beyond Zone A ($> 3\sigma$)
  - Rule 2: 9 points in a row on one side of the center line
  - Rule 3: 6 consecutive points steadily increasing or decreasing
  - Rule 4: 14 points alternating up and down
