using System;

namespace ZeroText.Localization
{
    /// <summary>
    /// Configuration options for Vietnamese number and currency to words conversion.
    /// </summary>
    public class VnWordsOptions
    {
        /// <summary>
        /// Default configuration using standard Northern conventions ("nghìn", "linh", "tư").
        /// </summary>
        public static readonly VnWordsOptions Default = new VnWordsOptions();

        /// <summary>
        /// Preconfigured options for Southern Vietnamese dialect ("ngàn", "lẻ", "tư").
        /// </summary>
        public static readonly VnWordsOptions SouthernDialect = new VnWordsOptions
        {
            UseSouthernThousands = true,
            UseSouthernZeroTens = true,
            UseTuForFour = true,
            CapitalizeFirstLetter = true
        };

        /// <summary>
        /// When true, uses "lẻ" instead of "linh" for zero-tens position (e.g. "lẻ năm" vs "linh năm"). Default is false ("linh").
        /// </summary>
        public bool UseSouthernZeroTens { get; set; } = false;

        /// <summary>
        /// When true, uses the Southern regional variant "ngàn" instead of "nghìn" for thousands scale. Default is false ("nghìn").
        /// </summary>
        public bool UseSouthernThousands { get; set; } = false;

        /// <summary>
        /// When true, uses "tư" instead of "bốn" when tens digit is 2 or greater (e.g. "hai mươi tư" vs "hai mươi bốn"). Default is true.
        /// </summary>
        public bool UseTuForFour { get; set; } = true;

        /// <summary>
        /// When true, capitalizes the first character of the generated text. Default is true.
        /// </summary>
        public bool CapitalizeFirstLetter { get; set; } = true;

        /// <summary>
        /// Backward-compatible alias for <see cref="UseSouthernThousands"/>.
        /// </summary>
        [Obsolete("Use UseSouthernThousands instead.")]
        public bool UseNgan
        {
            get => UseSouthernThousands;
            set => UseSouthernThousands = value;
        }

        /// <summary>
        /// Backward-compatible alias for <see cref="UseSouthernZeroTens"/>.
        /// </summary>
        [Obsolete("Use UseSouthernZeroTens instead.")]
        public bool UseLe
        {
            get => UseSouthernZeroTens;
            set => UseSouthernZeroTens = value;
        }

        /// <summary>
        /// Backward-compatible alias for <see cref="UseTuForFour"/>.
        /// </summary>
        [Obsolete("Use UseTuForFour instead.")]
        public bool UseTu
        {
            get => UseTuForFour;
            set => UseTuForFour = value;
        }
    }
}
