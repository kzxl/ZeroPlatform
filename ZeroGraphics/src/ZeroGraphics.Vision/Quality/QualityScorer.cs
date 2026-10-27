using System;
using System.Collections.Generic;

namespace ZeroGraphics.Vision.Quality
{
    /// <summary>
    /// Recommended curation or quality inspection action.
    /// </summary>
    public enum QualityAction
    {
        /// <summary>
        /// Mark for deletion/rejection (out of focus, blown, defective).
        /// </summary>
        Reject,

        /// <summary>
        /// Borderline quality, requires human or secondary inspection.
        /// </summary>
        Review,

        /// <summary>
        /// Acceptable quality, standard pass.
        /// </summary>
        Keep,

        /// <summary>
        /// High-fidelity, sharpest/best quality pass.
        /// </summary>
        Pick
    }

    /// <summary>
    /// Comprehensive quality assessment containing Technical Quality Index (TQI), star rating, and recommendations.
    /// </summary>
    public sealed class QualityAssessment
    {
        /// <summary>
        /// Technical Quality Index normalized from 0.0 to 100.0.
        /// </summary>
        public double Tqi { get; set; }

        /// <summary>
        /// Recommended star rating from 1 to 5 stars.
        /// </summary>
        public int StarRating { get; set; }

        /// <summary>
        /// Recommended workflow action (Pick, Keep, Review, Reject).
        /// </summary>
        public QualityAction RecommendedAction { get; set; }

        /// <summary>
        /// Detailed sharpness metrics.
        /// </summary>
        public SharpnessResult Sharpness { get; set; } = null!;

        /// <summary>
        /// Detailed exposure metrics.
        /// </summary>
        public ExposureResult Exposure { get; set; } = null!;

        /// <summary>
        /// Diagnostic reason strings explaining why an image was flagged or rated.
        /// </summary>
        public List<string> Reasons { get; set; } = new List<string>();
    }

    /// <summary>
    /// Synthesizes sharpness and exposure metrics into an objective Technical Quality Index (TQI)
    /// and generates automated quality pass/reject recommendations.
    /// </summary>
    public static class QualityScorer
    {
        /// <summary>
        /// Scores an image based on sharpness and exposure assessment results.
        /// </summary>
        /// <param name="sharpness">Sharpness evaluation result.</param>
        /// <param name="exposure">Exposure evaluation result.</param>
        /// <returns><see cref="QualityAssessment"/> with TQI score, star rating, and recommended action.</returns>
        public static QualityAssessment Score(SharpnessResult sharpness, ExposureResult exposure)
        {
            if (sharpness == null) throw new ArgumentNullException(nameof(sharpness));
            if (exposure == null) throw new ArgumentNullException(nameof(exposure));

            var reasons = new List<string>();

            // Sharpness subscore [0..100]: target 150 variance for 100%
            double sharpnessSubscore = Math.Min(100.0, (sharpness.EffectiveSharpness / 150.0) * 100.0);

            // Composite TQI: 65% Sharpness + 35% Exposure
            double tqi = Math.Round(sharpnessSubscore * 0.65 + exposure.ExposureScore * 0.35, 1);
            tqi = Math.Max(0.0, Math.Min(100.0, tqi));

            // Star rating mapping
            int stars;
            if (tqi >= 80.0) stars = 5;
            else if (tqi >= 65.0) stars = 4;
            else if (tqi >= 50.0) stars = 3;
            else if (tqi >= 35.0) stars = 2;
            else stars = 1;

            // Decision logic for automated action
            QualityAction action;

            if (sharpness.IsSevereBlur)
            {
                action = QualityAction.Reject;
                reasons.Add($"Severe blur (Sharpness {sharpness.EffectiveSharpness:F1} < 45.0)");
            }
            else if (exposure.IsSevereOverexposure)
            {
                action = QualityAction.Reject;
                reasons.Add($"Severe overexposure (Mean brightness {exposure.MeanBrightness:F1} > 225.0)");
            }
            else if (exposure.IsSevereUnderexposure)
            {
                action = QualityAction.Reject;
                reasons.Add($"Severe underexposure (Mean brightness {exposure.MeanBrightness:F1} < 35.0)");
            }
            else if (exposure.HighlightClipRatio > 0.05) // Over 5% blown highlight
            {
                action = QualityAction.Reject;
                reasons.Add($"Excessive blown highlights ({exposure.HighlightClipRatio * 100:F1}% clipped)");
            }
            else if (tqi < 40.0 || exposure.IsBlownHighlights || exposure.IsCrushedShadows)
            {
                action = QualityAction.Review;
                if (exposure.IsBlownHighlights)
                    reasons.Add($"Blown highlights detected ({exposure.HighlightClipRatio * 100:F1}%)");
                if (exposure.IsCrushedShadows)
                    reasons.Add($"Crushed shadows detected ({exposure.ShadowClipRatio * 100:F1}%)");
                if (tqi < 40.0)
                    reasons.Add($"Borderline quality (TQI {tqi:F1})");
            }
            else if (tqi >= 75.0 && sharpness.IsSharp)
            {
                action = QualityAction.Pick;
                reasons.Add($"Excellent sharpness ({sharpness.EffectiveSharpness:F1}) & balanced exposure");
            }
            else
            {
                action = QualityAction.Keep;
                reasons.Add($"Acceptable quality (TQI {tqi:F1})");
            }

            return new QualityAssessment
            {
                Tqi = tqi,
                StarRating = stars,
                RecommendedAction = action,
                Sharpness = sharpness,
                Exposure = exposure,
                Reasons = reasons
            };
        }
    }
}
