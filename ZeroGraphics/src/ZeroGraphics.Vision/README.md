# ZeroGraphics.Vision ⚡

Pure C#, zero-external-dependency Industrial Machine Vision, Metrology, 1D/2D Barcodes, and Pattern Matching engine for .NET (`net462`, `net8.0-windows`).

---

## 🌟 Key Features

* **Sub-Pixel Normalized Cross-Correlation (`NccTemplateMatcher`):** High-speed pattern matching with parabolic sub-pixel peak interpolation ($<0.05$ pixel error) and illumination invariance.
* **1D Sub-Pixel Edge Caliper (`EdgeCaliper1D`, `CaliperEdge`):** Arbitrary line rake edge detector with sub-pixel gradient maxima detection for precision gauging.
* **Geometric Fitting & RANSAC (`GeometryFitters`, `RansacFitter`):** Total Least Squares (TLS) orthogonal line fitting, Taubin unbiased circle fitting, Fitzgibbon ellipse fitting, and RANSAC robust outlier rejection.
* **GD&T & Convex Polygon Geometry (`ConvexHull2D`, `RotatedRect2D`):** Graham Scan convex hull and Rotating Calipers minimum area Oriented Bounding Box (OBB) for part orientation.
* **1D & 2D Barcode Engine & Reed-Solomon:** Code 128, Code 39, QR Code, and DataMatrix ECC200 encoding and decoding with polynomial error correction.
* **Homography & Perspective Stitching (`Homography2D`, `PerspectiveWarper`, `ImageStitcher`):** Direct Linear Transform (DLT) 8-DOF homography estimation, multi-view panorama stitching, and perspective rectification.
* **8-Way CCL Blob Analysis (`BlobAnalyzer`, `ConnectedComponentLabeling`):** Two-pass connected component labeling with Disjoint Set Union extracting area, centroid, perimeter, bounding box, and circularity.
* **Part of the [ZeroPlatform](https://github.com/kzxl/ZeroPlatform) Ecosystem.**
