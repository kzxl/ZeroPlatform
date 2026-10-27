using System;
using System.ComponentModel;
using System.Windows.Forms;
using ZeroUI.Core.Compliance;

namespace ZeroUI.WinForms.Compliance
{
    public class ZElectronicSignature
    {
        public static SignatureResult Show(IWin32Window owner, string reason)
        {
            return new SignatureResult { IsValid = false };
        }
    }

    [Obsolete("ElectronicSignature is deprecated and will be removed in 5 release cycles. Please migrate to ZElectronicSignature instead.")]
    public class ElectronicSignature : ZElectronicSignature { }

    [Obsolete("ZeroElectronicSignature is deprecated. Please use ZElectronicSignature instead.")]
    public class ZeroElectronicSignature : ZElectronicSignature { }
}
