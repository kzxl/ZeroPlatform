using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZeroUI.Core.Localization
{
    /// <summary>
    /// Centralized Internationalization & Localization Coordinator for ZeroUI.
    /// Provides zero-allocation lookup for standard UI strings with built-in English and Vietnamese
    /// dictionaries, dynamic runtime language switching, and custom string overriding.
    /// </summary>
    public static class Localizer
    {
        private static readonly Dictionary<StringId, string> English = new Dictionary<StringId, string>
        {
            [StringId.Ok] = "OK",
            [StringId.Cancel] = "Cancel",
            [StringId.Apply] = "Apply",
            [StringId.Close] = "Close",
            [StringId.Clear] = "Clear",
            [StringId.Reset] = "Reset",
            [StringId.Save] = "Save",
            [StringId.Search] = "Search...",
            [StringId.Loading] = "Loading...",
            [StringId.Refresh] = "Refresh",

            [StringId.CheckedComboPlaceholder] = "Select items...",
            [StringId.CheckedComboSummaryFormat] = "{0} items selected",
            [StringId.CheckedComboSelectAll] = "(Select All)",
            [StringId.TokenEditPlaceholder] = "Type and press Enter...",
            [StringId.DateEditPlaceholder] = "Select date...",
            [StringId.DateEditToday] = "Today",
            [StringId.DateEditClear] = "Clear",
            [StringId.ColorPickerPlaceholder] = "Select color...",

            [StringId.FilterOpAnd] = "AND",
            [StringId.FilterOpOr] = "OR",
            [StringId.FilterOpNotAnd] = "NOT AND",
            [StringId.FilterOpNotOr] = "NOT OR",
            [StringId.FilterEquals] = "Equals",
            [StringId.FilterNotEquals] = "Does not equal",
            [StringId.FilterContains] = "Contains",
            [StringId.FilterStartsWith] = "Starts with",
            [StringId.FilterEndsWith] = "Ends with",
            [StringId.FilterGreaterThan] = "Greater than",
            [StringId.FilterLessThan] = "Less than",
            [StringId.FilterIsNull] = "Is null",
            [StringId.FilterIsNotNull] = "Is not null",
            [StringId.FilterAddCondition] = "+ Condition",
            [StringId.FilterAddGroup] = "+ Group",

            [StringId.WizardBack] = "← Back",
            [StringId.WizardNext] = "Next →",
            [StringId.WizardFinish] = "Finish ✓",
            [StringId.WizardCancel] = "Cancel",
            [StringId.WizardStepTitleDefault] = "Step Title",
            [StringId.WizardValidationTitle] = "Step Validation",
            [StringId.WizardNoPages] = "No Pages",
            [StringId.WizardNoPagesDesc] = "Add pages to this wizard.",

            [StringId.PrintButton] = "🖨️ Print",
            [StringId.PrintStatusFormat] = "Page {0} of {1}",
            [StringId.ZoomFit] = "100%",

            [StringId.ValRequired] = "This field is required.",
            [StringId.ValRangeFormat] = "Value must be between {0} and {1}.",
            [StringId.ValEmail] = "Invalid email address format.",
            [StringId.ValPhone] = "Invalid phone number format.",
            [StringId.ValStringLengthFormat] = "Text length must be between {0} and {1} characters.",
            [StringId.ValInvalidFormat] = "Input format is invalid.",

            [StringId.PivotGrandTotal] = "Grand Total",
            [StringId.PivotTotal] = "Total",
            [StringId.PivotDropFilterFields] = "Drop Filter Fields Here",
            [StringId.PivotDropRowFields] = "Drop Row Fields Here",
            [StringId.PivotDropColumnFields] = "Drop Column Fields Here",
            [StringId.PivotDropDataFields] = "Drop Data Fields Here",

            [StringId.RangeFrom] = "From",
            [StringId.RangeTo] = "To",
            [StringId.RangeSpan] = "Span",
            [StringId.RangeAll] = "All",
            [StringId.RangeZoomIn] = "Zoom In",
            [StringId.RangeZoomOut] = "Zoom Out"
        };

        private static readonly Dictionary<StringId, string> Vietnamese = new Dictionary<StringId, string>
        {
            [StringId.Ok] = "Đồng ý",
            [StringId.Cancel] = "Hủy",
            [StringId.Apply] = "Áp dụng",
            [StringId.Close] = "Đóng",
            [StringId.Clear] = "Xóa",
            [StringId.Reset] = "Đặt lại",
            [StringId.Save] = "Lưu",
            [StringId.Search] = "Tìm kiếm...",
            [StringId.Loading] = "Đang tải...",
            [StringId.Refresh] = "Làm mới",

            [StringId.CheckedComboPlaceholder] = "Chọn mục...",
            [StringId.CheckedComboSummaryFormat] = "Đã chọn {0} mục",
            [StringId.CheckedComboSelectAll] = "(Chọn tất cả)",
            [StringId.TokenEditPlaceholder] = "Nhập và nhấn Enter...",
            [StringId.DateEditPlaceholder] = "Chọn ngày...",
            [StringId.DateEditToday] = "Hôm nay",
            [StringId.DateEditClear] = "Xóa",
            [StringId.ColorPickerPlaceholder] = "Chọn màu...",

            [StringId.FilterOpAnd] = "VÀ",
            [StringId.FilterOpOr] = "HOẶC",
            [StringId.FilterOpNotAnd] = "KHÔNG VÀ",
            [StringId.FilterOpNotOr] = "KHÔNG HOẶC",
            [StringId.FilterEquals] = "Bằng",
            [StringId.FilterNotEquals] = "Khác",
            [StringId.FilterContains] = "Chứa",
            [StringId.FilterStartsWith] = "Bắt đầu bằng",
            [StringId.FilterEndsWith] = "Kết thúc bằng",
            [StringId.FilterGreaterThan] = "Lớn hơn",
            [StringId.FilterLessThan] = "Nhỏ hơn",
            [StringId.FilterIsNull] = "Là rỗng (Null)",
            [StringId.FilterIsNotNull] = "Không rỗng",
            [StringId.FilterAddCondition] = "+ Điều kiện",
            [StringId.FilterAddGroup] = "+ Nhóm",

            [StringId.WizardBack] = "← Quay lại",
            [StringId.WizardNext] = "Tiếp theo →",
            [StringId.WizardFinish] = "Hoàn tất ✓",
            [StringId.WizardCancel] = "Hủy",
            [StringId.WizardStepTitleDefault] = "Tiêu đề bước",
            [StringId.WizardValidationTitle] = "Xác thực bước",
            [StringId.WizardNoPages] = "Chưa có trang",
            [StringId.WizardNoPagesDesc] = "Thêm các trang bước vào wizard này.",

            [StringId.PrintButton] = "🖨️ In ấn",
            [StringId.PrintStatusFormat] = "Trang {0} / {1}",
            [StringId.ZoomFit] = "100%",

            [StringId.ValRequired] = "Trường này là bắt buộc.",
            [StringId.ValRangeFormat] = "Giá trị phải nằm trong khoảng từ {0} đến {1}.",
            [StringId.ValEmail] = "Định dạng địa chỉ email không hợp lệ.",
            [StringId.ValPhone] = "Định dạng số điện thoại không hợp lệ.",
            [StringId.ValStringLengthFormat] = "Độ dài văn bản phải từ {0} đến {1} ký tự.",
            [StringId.ValInvalidFormat] = "Định dạng nhập liệu không hợp lệ.",

            [StringId.PivotGrandTotal] = "Tổng cộng",
            [StringId.PivotTotal] = "Tổng",
            [StringId.PivotDropFilterFields] = "Kéo thả trường bộ lọc vào đây",
            [StringId.PivotDropRowFields] = "Kéo thả trường hàng vào đây",
            [StringId.PivotDropColumnFields] = "Kéo thả trường cột vào đây",
            [StringId.PivotDropDataFields] = "Kéo thả trường dữ liệu vào đây",

            [StringId.RangeFrom] = "Từ",
            [StringId.RangeTo] = "Đến",
            [StringId.RangeSpan] = "Khoảng",
            [StringId.RangeAll] = "Tất cả",
            [StringId.RangeZoomIn] = "Phóng to",
            [StringId.RangeZoomOut] = "Thu nhỏ"
        };

        private static readonly Dictionary<string, Dictionary<StringId, string>> CustomLanguages =
            new Dictionary<string, Dictionary<StringId, string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<StringId, string> Overrides =
            new Dictionary<StringId, string>();

        private static CultureInfo _currentCulture = CultureInfo.InvariantCulture;

        public static event EventHandler? CultureChanged;

        public static CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set
            {
                if (_currentCulture != value)
                {
                    _currentCulture = value ?? CultureInfo.InvariantCulture;
                    CultureChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        public static void SetLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                CurrentCulture = CultureInfo.InvariantCulture;
                return;
            }

            try
            {
                CurrentCulture = new CultureInfo(languageCode);
            }
            catch
            {
                CurrentCulture = CultureInfo.InvariantCulture;
            }
        }

        public static void SetLanguage(CultureInfo culture)
        {
            CurrentCulture = culture ?? CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// Overrides a specific string ID with a custom application-defined text value.
        /// </summary>
        public static void Override(StringId id, string customText)
        {
            if (customText == null)
            {
                Overrides.Remove(id);
            }
            else
            {
                Overrides[id] = customText;
            }
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Legacy overload for backward compatibility with <see cref="ZeroStringId"/>.
        /// </summary>
        [Obsolete("Use StringId overload instead.")]
        public static void Override(ZeroStringId id, string customText) => Override((StringId)id, customText);

        /// <summary>
        /// Resets all custom overrides.
        /// </summary>
        public static void ResetOverrides()
        {
            Overrides.Clear();
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Registers a full custom language dictionary (e.g. "ja", "de", "fr").
        /// </summary>
        public static void RegisterLanguage(string languageCode, IDictionary<StringId, string> dictionary)
        {
            if (string.IsNullOrWhiteSpace(languageCode) || dictionary == null) return;
            var dict = new Dictionary<StringId, string>(dictionary);
            CustomLanguages[languageCode] = dict;
        }

        /// <summary>
        /// Legacy overload for backward compatibility with <see cref="ZeroStringId"/>.
        /// </summary>
        [Obsolete("Use StringId overload instead.")]
        public static void RegisterLanguage(string languageCode, IDictionary<ZeroStringId, string> dictionary)
        {
            if (string.IsNullOrWhiteSpace(languageCode) || dictionary == null) return;
            var dict = new Dictionary<StringId, string>();
            foreach (var kvp in dictionary)
            {
                dict[(StringId)kvp.Key] = kvp.Value;
            }
            CustomLanguages[languageCode] = dict;
        }

        /// <summary>
        /// Retrieves the localized string for the specified ID according to CurrentCulture.
        /// Falls back to English if missing from the active culture.
        /// </summary>
        public static string GetString(StringId id)
        {
            // 1. Check user-defined overrides
            if (Overrides.TryGetValue(id, out var overridden))
            {
                return overridden;
            }

            // 2. Check registered custom languages
            string lang = _currentCulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (CustomLanguages.TryGetValue(lang, out var customDict) && customDict.TryGetValue(id, out var customStr))
            {
                return customStr;
            }

            // 3. Check built-in Vietnamese
            if (lang == "vi")
            {
                if (Vietnamese.TryGetValue(id, out var viStr)) return viStr;
            }

            // 4. Fallback to built-in English
            if (English.TryGetValue(id, out var enStr))
            {
                return enStr;
            }

            return id.ToString();
        }

        /// <summary>
        /// Legacy overload for backward compatibility with <see cref="ZeroStringId"/>.
        /// </summary>
        [Obsolete("Use StringId overload instead.")]
        public static string GetString(ZeroStringId id) => GetString((StringId)id);

        /// <summary>
        /// Retrieves a localized formatted string with argument replacement.
        /// </summary>
        public static string GetFormattedString(StringId id, params object[] args)
        {
            string format = GetString(id);
            if (args == null || args.Length == 0) return format;
            try
            {
                return string.Format(_currentCulture, format, args);
            }
            catch
            {
                return format;
            }
        }

        /// <summary>
        /// Legacy overload for backward compatibility with <see cref="ZeroStringId"/>.
        /// </summary>
        [Obsolete("Use StringId overload instead.")]
        public static string GetFormattedString(ZeroStringId id, params object[] args) => GetFormattedString((StringId)id, args);
    }

    /// <summary>
    /// Legacy coordinator for backward compatibility. Use <see cref="Localizer"/> instead.
    /// </summary>
    [Obsolete("Use Localizer instead.")]
    public static class ZeroLocalizer
    {
        public static CultureInfo CurrentCulture
        {
            get => Localizer.CurrentCulture;
            set => Localizer.CurrentCulture = value;
        }

        public static event EventHandler? CultureChanged
        {
            add => Localizer.CultureChanged += value;
            remove => Localizer.CultureChanged -= value;
        }

        public static void SetLanguage(string languageCode) => Localizer.SetLanguage(languageCode);
        public static void SetLanguage(CultureInfo culture) => Localizer.SetLanguage(culture);
        public static void Override(StringId id, string customText) => Localizer.Override(id, customText);
        public static void Override(ZeroStringId id, string customText) => Localizer.Override(id, customText);
        public static void ResetOverrides() => Localizer.ResetOverrides();
        public static void RegisterLanguage(string languageCode, IDictionary<StringId, string> dictionary) => Localizer.RegisterLanguage(languageCode, dictionary);
        public static void RegisterLanguage(string languageCode, IDictionary<ZeroStringId, string> dictionary) => Localizer.RegisterLanguage(languageCode, dictionary);
        public static string GetString(StringId id) => Localizer.GetString(id);
        public static string GetString(ZeroStringId id) => Localizer.GetString(id);
        public static string GetFormattedString(StringId id, params object[] args) => Localizer.GetFormattedString(id, args);
        public static string GetFormattedString(ZeroStringId id, params object[] args) => Localizer.GetFormattedString(id, args);
    }
}
