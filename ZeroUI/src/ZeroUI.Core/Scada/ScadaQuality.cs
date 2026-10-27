using System;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// OPC / Modbus / Industrial SCADA signal quality status.
    /// </summary>
    public enum ScadaQuality : byte
    {
        Good = 0,
        Bad = 1,
        Uncertain = 2,
        CommFailure = 3
    }
}
