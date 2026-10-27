using System;

namespace ZeroUI.WinForms.Editors
{
    // =========================================================================================
    // BACKWARD COMPATIBILITY SHIMS
    // These type declarations ensure that existing code referencing ZeroUI.WinForms.Editors
    // continues to compile without breaking changes.
    // =========================================================================================

    [Obsolete("Use ZeroUI.WinForms.Containers.StatisticCard instead.")]
    public class StatisticCard : ZeroUI.WinForms.Containers.StatisticCard { }

    [Obsolete("Use ZeroUI.WinForms.Navigation.PaginationControl instead.")]
    public class PaginationControl : ZeroUI.WinForms.Navigation.PaginationControl { }

    [Obsolete("ColorPickerEdit is deprecated. Use ColorPickEdit instead.")]
    public class ColorPickerEdit : ColorPickEdit
    {
        [Obsolete("Use SelectedColor instead.")]
        public System.Drawing.Color Color
        {
            get => SelectedColor;
            set => SelectedColor = value;
        }
    }

    [Obsolete("ZPictureEdit has moved to ZeroUI.WinForms.Media. Please migrate to ZeroUI.WinForms.Media.ZPictureEdit instead.")]
    public class ZPictureEdit : ZeroUI.WinForms.Media.ZPictureEdit { }

    [Obsolete("PictureEdit has moved to ZeroUI.WinForms.Media. Please migrate to ZeroUI.WinForms.Media.ZPictureEdit instead.")]
    public class PictureEdit : ZeroUI.WinForms.Media.ZPictureEdit { }

    [Obsolete("ZeroPictureEdit has moved to ZeroUI.WinForms.Media. Please migrate to ZeroUI.WinForms.Media.ZPictureEdit instead.")]
    public class ZeroPictureEdit : ZeroUI.WinForms.Media.ZPictureEdit { }

    [Obsolete("ZeroImage has moved to ZeroUI.WinForms.Media. Please migrate to ZeroUI.WinForms.Media.ZPictureEdit instead.")]
    public class ZeroImage : ZeroUI.WinForms.Media.ZPictureEdit { }
}
