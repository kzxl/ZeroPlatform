using System;
using System.Collections.Generic;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Curation;
using ZeroGraphics.Vision.Matching;
using ZeroGraphics.Vision.Quality;

namespace ZeroGraphics.Tests
{
    public class CurationTests
    {
        [Fact]
        public void DifferenceHash_ComputesDeterministicHash_AndZeroDistanceForDuplicates()
        {
            int w = 180;
            int h = 160;

            using (var img1 = ImageBuffer.CreateGray8(w, h))
            using (var img2 = ImageBuffer.CreateGray8(w, h))
            {
                unsafe
                {
                    // Create horizontal ramp pattern
                    for (int y = 0; y < h; y++)
                    {
                        byte* r1 = img1.GetRowPointer(y);
                        byte* r2 = img2.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            byte val = (byte)((x * 255) / w);
                            r1[x] = val;
                            r2[x] = val;
                        }
                    }
                }

                ulong hash1 = DifferenceHash.Compute(img1);
                ulong hash2 = DifferenceHash.Compute(img2);

                Assert.Equal(hash1, hash2);
                Assert.Equal(0, DifferenceHash.HammingDistance(hash1, hash2));
                Assert.Equal(1.0, DifferenceHash.Similarity(hash1, hash2));
                Assert.Equal(16, DifferenceHash.ToHexString(hash1).Length);
            }
        }

        [Fact]
        public void DifferenceHash_DetectsSmallChanges_WithinBurstThreshold()
        {
            int w = 180;
            int h = 160;

            using (var img1 = ImageBuffer.CreateGray8(w, h))
            using (var img2 = ImageBuffer.CreateGray8(w, h))
            {
                unsafe
                {
                    // Base gradient
                    for (int y = 0; y < h; y++)
                    {
                        byte* r1 = img1.GetRowPointer(y);
                        byte* r2 = img2.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            byte val = (byte)((x * 255) / w);
                            r1[x] = val;
                            // Minor brightness jitter (simulating sensor noise / micro burst shift)
                            r2[x] = (byte)Math.Max(0, Math.Min(255, val + (x % 3 == 0 ? 2 : -2)));
                        }
                    }
                }

                ulong hash1 = DifferenceHash.Compute(img1);
                ulong hash2 = DifferenceHash.Compute(img2);

                int dist = DifferenceHash.HammingDistance(hash1, hash2);
                // Minor variations should still yield a low Hamming distance (< 6)
                Assert.True(dist <= 4, $"Expected Hamming distance <= 4 for minor variations, got {dist}");
            }
        }

        [Fact]
        public void ImageSharpnessEvaluator_IdentifiesSevereBlurVsHighSharpness()
        {
            int w = 200;
            int h = 200;

            using (var flatImage = ImageBuffer.CreateGray8(w, h))
            using (var sharpImage = ImageBuffer.CreateGray8(w, h))
            {
                unsafe
                {
                    // 1. Uniform / blurry image (zero or almost zero Laplacian)
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = flatImage.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            row[x] = 128;
                        }
                    }

                    // 2. High contrast checkerboard / edge pattern (high Laplacian)
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = sharpImage.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            row[x] = (byte)(((x / 8) % 2 == (y / 8) % 2) ? 240 : 15);
                        }
                    }
                }

                var flatResult = ImageSharpnessEvaluator.Evaluate(flatImage);
                var sharpResult = ImageSharpnessEvaluator.Evaluate(sharpImage);

                Assert.True(flatResult.IsSevereBlur);
                Assert.True(flatResult.EffectiveSharpness < 5.0);

                Assert.True(sharpResult.IsSharp);
                Assert.False(sharpResult.IsSevereBlur);
                Assert.True(sharpResult.EffectiveSharpness > 150.0);
            }
        }

        [Fact]
        public void ImageSharpnessEvaluator_PreservesFocalSharpness_ForShallowDepthOfField()
        {
            int w = 300;
            int h = 300;

            // Simulate shallow DOF: 90% smooth blurred background (flat 120), center 10% patch has crisp sharp edges
            using (var portrait = ImageBuffer.CreateGray8(w, h))
            {
                unsafe
                {
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = portrait.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            // Center region [120..180, 120..180] is sharp subject
                            if (x >= 120 && x <= 180 && y >= 120 && y <= 180)
                            {
                                row[x] = (byte)(((x / 4) % 2 == (y / 4) % 2) ? 230 : 25);
                            }
                            else
                            {
                                // Creamy bokeh background
                                row[x] = 120;
                            }
                        }
                    }
                }

                var result = ImageSharpnessEvaluator.Evaluate(portrait);

                // Focal sharpness from the center patch must be high even if global variance is diluted
                Assert.True(result.FocalSharpness > 100.0, $"Focal sharpness was {result.FocalSharpness}");
                Assert.True(result.EffectiveSharpness >= result.FocalSharpness);
                Assert.False(result.IsSevereBlur); // Should not falsely reject shallow DOF portrait
            }
        }

        [Fact]
        public void ImageExposureEvaluator_DetectsBlownHighlightsAndClipping()
        {
            int w = 200;
            int h = 200;

            using (var blownImg = ImageBuffer.CreateGray8(w, h))
            using (var balancedImg = ImageBuffer.CreateGray8(w, h))
            {
                unsafe
                {
                    // 1. Blown highlights: 15% of pixels pinned to 255
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = blownImg.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            row[x] = (x < 30) ? (byte)255 : (byte)140;
                        }
                    }

                    // 2. Well-exposed midtone gradient
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = balancedImg.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            row[x] = (byte)(40 + (x * 160) / w);
                        }
                    }
                }

                var blownResult = ImageExposureEvaluator.Evaluate(blownImg);
                var balancedResult = ImageExposureEvaluator.Evaluate(balancedImg);

                Assert.True(blownResult.IsBlownHighlights);
                Assert.True(blownResult.HighlightClipRatio > 0.10);
                Assert.True(blownResult.ExposureScore < 70.0);

                Assert.False(balancedResult.IsBlownHighlights);
                Assert.False(balancedResult.IsCrushedShadows);
                Assert.True(balancedResult.ExposureScore >= 80.0);
            }
        }

        [Fact]
        public void BurstClusterer_ElectsSharpestShot_AndMarksRedundantDuplicates()
        {
            // Simulate 3 burst shots of the same scene with varying sharpness
            ulong burstHash = 0x123456789ABCDEF0UL;

            var candidates = new List<CurationCandidate>
            {
                new CurationCandidate
                {
                    Id = "IMG_001.JPG",
                    Hash = burstHash,
                    EffectiveSharpness = 42.0, // Blurry
                    ExposureScore = 90.0,
                    CaptureTime = DateTime.UtcNow
                },
                new CurationCandidate
                {
                    Id = "IMG_002.JPG",
                    Hash = burstHash ^ 0x01UL, // 1-bit Hamming distance (same burst)
                    EffectiveSharpness = 185.0, // Crisp!
                    ExposureScore = 92.0,
                    CaptureTime = DateTime.UtcNow.AddSeconds(0.2)
                },
                new CurationCandidate
                {
                    Id = "IMG_003.JPG",
                    Hash = burstHash ^ 0x03UL, // 2-bit Hamming distance
                    EffectiveSharpness = 88.0, // Moderate
                    ExposureScore = 91.0,
                    CaptureTime = DateTime.UtcNow.AddSeconds(0.4)
                },
                new CurationCandidate
                {
                    Id = "IMG_999.JPG",
                    Hash = ~burstHash, // Completely different scene
                    EffectiveSharpness = 140.0,
                    ExposureScore = 85.0,
                    CaptureTime = DateTime.UtcNow.AddMinutes(10)
                }
            };

            var clusters = BurstClusterer.Cluster(candidates);

            Assert.Equal(2, clusters.Count);

            var burst = clusters.Find(c => c.Members.Count == 3);
            Assert.NotNull(burst);
            Assert.Equal("IMG_002.JPG", burst!.BestShot?.Id);
            Assert.Equal(2, burst.RedundantShots.Count);
            Assert.Contains(burst.RedundantShots, r => r.Id == "IMG_001.JPG");
            Assert.Contains(burst.RedundantShots, r => r.Id == "IMG_003.JPG");
        }

        [Fact]
        public void QualityScorer_AssignsAppropriateRecommendationsAndStarRatings()
        {
            // Case 1: Severe blur
            var blurrySharpness = new SharpnessResult
            {
                GlobalSharpness = 15.0,
                FocalSharpness = 20.0,
                EffectiveSharpness = 20.0,
                IsSevereBlur = true
            };
            var goodExposure = new ExposureResult
            {
                MeanBrightness = 120.0,
                HighlightClipRatio = 0.001,
                ShadowClipRatio = 0.002,
                ExposureScore = 95.0
            };

            var rejectAssessment = QualityScorer.Score(blurrySharpness, goodExposure);
            Assert.Equal(QualityAction.Reject, rejectAssessment.RecommendedAction);
            Assert.True(rejectAssessment.StarRating <= 2);
            Assert.Contains(rejectAssessment.Reasons, r => r.Contains("Severe blur"));

            // Case 2: Razor sharp and well-exposed
            var sharpSharpness = new SharpnessResult
            {
                GlobalSharpness = 160.0,
                FocalSharpness = 220.0,
                EffectiveSharpness = 220.0,
                IsSharp = true
            };
            var pickAssessment = QualityScorer.Score(sharpSharpness, goodExposure);
            Assert.Equal(QualityAction.Pick, pickAssessment.RecommendedAction);
            Assert.True(pickAssessment.StarRating >= 4);
            Assert.True(pickAssessment.Tqi >= 75.0);
        }
    }
}
