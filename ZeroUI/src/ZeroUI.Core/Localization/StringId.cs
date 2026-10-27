using System;

namespace ZeroUI.Core.Localization
{
    /// <summary>
    /// Identifiers for localizable UI text across all ZeroUI controls and components.
    /// </summary>
    public enum StringId
    {
        // Common Actions & States
        Ok = 0,
        Cancel = 1,
        Apply = 2,
        Close = 3,
        Clear = 4,
        Reset = 5,
        Save = 6,
        Search = 7,
        Loading = 8,
        Refresh = 9,

        // Editors
        CheckedComboPlaceholder = 100,
        CheckedComboSummaryFormat = 101,
        CheckedComboSelectAll = 102,
        TokenEditPlaceholder = 103,
        DateEditPlaceholder = 104,
        DateEditToday = 105,
        DateEditClear = 106,
        ColorPickerPlaceholder = 107,

        // DataGrid & Filter Control
        FilterOpAnd = 200,
        FilterOpOr = 201,
        FilterOpNotAnd = 202,
        FilterOpNotOr = 203,
        FilterEquals = 204,
        FilterNotEquals = 205,
        FilterContains = 206,
        FilterStartsWith = 207,
        FilterEndsWith = 208,
        FilterGreaterThan = 209,
        FilterLessThan = 210,
        FilterIsNull = 211,
        FilterIsNotNull = 212,
        FilterAddCondition = 213,
        FilterAddGroup = 214,

        // Wizard Control
        WizardBack = 300,
        WizardNext = 301,
        WizardFinish = 302,
        WizardCancel = 303,
        WizardStepTitleDefault = 304,
        WizardValidationTitle = 305,
        WizardNoPages = 306,
        WizardNoPagesDesc = 307,

        // Reporting & Document Preview
        PrintButton = 400,
        PrintStatusFormat = 401,
        ZoomFit = 402,

        // Validation Rules
        ValRequired = 500,
        ValRangeFormat = 501,
        ValEmail = 502,
        ValPhone = 503,
        ValStringLengthFormat = 504,
        ValInvalidFormat = 505,

        // Pivot Grid / OLAP Reporting
        PivotGrandTotal = 600,
        PivotTotal = 601,
        PivotDropFilterFields = 602,
        PivotDropRowFields = 603,
        PivotDropColumnFields = 604,
        PivotDropDataFields = 605,

        // Range Control & Timeline
        RangeFrom = 700,
        RangeTo = 701,
        RangeSpan = 702,
        RangeAll = 703,
        RangeZoomIn = 704,
        RangeZoomOut = 705
    }

    /// <summary>
    /// Legacy identifier enum for backward compatibility. Use <see cref="StringId"/> instead.
    /// </summary>
    [Obsolete("Use StringId instead.")]
    public enum ZeroStringId
    {
        // Common Actions & States
        Ok = StringId.Ok,
        Cancel = StringId.Cancel,
        Apply = StringId.Apply,
        Close = StringId.Close,
        Clear = StringId.Clear,
        Reset = StringId.Reset,
        Save = StringId.Save,
        Search = StringId.Search,
        Loading = StringId.Loading,
        Refresh = StringId.Refresh,

        // Editors
        CheckedComboPlaceholder = StringId.CheckedComboPlaceholder,
        CheckedComboSummaryFormat = StringId.CheckedComboSummaryFormat,
        CheckedComboSelectAll = StringId.CheckedComboSelectAll,
        TokenEditPlaceholder = StringId.TokenEditPlaceholder,
        DateEditPlaceholder = StringId.DateEditPlaceholder,
        DateEditToday = StringId.DateEditToday,
        DateEditClear = StringId.DateEditClear,
        ColorPickerPlaceholder = StringId.ColorPickerPlaceholder,

        // DataGrid & Filter Control
        FilterOpAnd = StringId.FilterOpAnd,
        FilterOpOr = StringId.FilterOpOr,
        FilterOpNotAnd = StringId.FilterOpNotAnd,
        FilterOpNotOr = StringId.FilterOpNotOr,
        FilterEquals = StringId.FilterEquals,
        FilterNotEquals = StringId.FilterNotEquals,
        FilterContains = StringId.FilterContains,
        FilterStartsWith = StringId.FilterStartsWith,
        FilterEndsWith = StringId.FilterEndsWith,
        FilterGreaterThan = StringId.FilterGreaterThan,
        FilterLessThan = StringId.FilterLessThan,
        FilterIsNull = StringId.FilterIsNull,
        FilterIsNotNull = StringId.FilterIsNotNull,
        FilterAddCondition = StringId.FilterAddCondition,
        FilterAddGroup = StringId.FilterAddGroup,

        // Wizard Control
        WizardBack = StringId.WizardBack,
        WizardNext = StringId.WizardNext,
        WizardFinish = StringId.WizardFinish,
        WizardCancel = StringId.WizardCancel,
        WizardStepTitleDefault = StringId.WizardStepTitleDefault,
        WizardValidationTitle = StringId.WizardValidationTitle,
        WizardNoPages = StringId.WizardNoPages,
        WizardNoPagesDesc = StringId.WizardNoPagesDesc,

        // Reporting & Document Preview
        PrintButton = StringId.PrintButton,
        PrintStatusFormat = StringId.PrintStatusFormat,
        ZoomFit = StringId.ZoomFit,

        // Validation Rules
        ValRequired = StringId.ValRequired,
        ValRangeFormat = StringId.ValRangeFormat,
        ValEmail = StringId.ValEmail,
        ValPhone = StringId.ValPhone,
        ValStringLengthFormat = StringId.ValStringLengthFormat,
        ValInvalidFormat = StringId.ValInvalidFormat,

        // Pivot Grid / OLAP Reporting
        PivotGrandTotal = StringId.PivotGrandTotal,
        PivotTotal = StringId.PivotTotal,
        PivotDropFilterFields = StringId.PivotDropFilterFields,
        PivotDropRowFields = StringId.PivotDropRowFields,
        PivotDropColumnFields = StringId.PivotDropColumnFields,
        PivotDropDataFields = StringId.PivotDropDataFields,

        // Range Control & Timeline
        RangeFrom = StringId.RangeFrom,
        RangeTo = StringId.RangeTo,
        RangeSpan = StringId.RangeSpan,
        RangeAll = StringId.RangeAll,
        RangeZoomIn = StringId.RangeZoomIn,
        RangeZoomOut = StringId.RangeZoomOut
    }
}
