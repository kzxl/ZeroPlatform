using System.Threading;
using System.Threading.Tasks;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Core.Abstractions;

/// <summary>
/// Capability interface for RFID readers supporting discrete tag memory read, write, lock, and kill commands.
/// </summary>
public interface IRfidMemoryAccess
{
    /// <summary>
    /// Reads binary words from a specific EPC Gen2 memory bank of a target tag.
    /// </summary>
    /// <param name="epc">The EPC of the target tag to access.</param>
    /// <param name="bank">Target memory bank (Reserved, EPC, TID, User).</param>
    /// <param name="startWord">Zero-based 16-bit word offset to begin reading.</param>
    /// <param name="wordCount">Number of 16-bit words to read.</param>
    /// <param name="accessPassword">Optional 8-hex-char access password if the bank is password-protected.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Raw byte buffer containing read memory.</returns>
    Task<byte[]> ReadBankAsync(
        string epc,
        MemoryBank bank,
        ushort startWord,
        ushort wordCount,
        string? accessPassword = null,
        CancellationToken ct = default);

    /// <summary>
    /// Writes binary payload to a specific EPC Gen2 memory bank of a target tag.
    /// </summary>
    Task<bool> WriteBankAsync(
        string epc,
        MemoryBank bank,
        ushort startWord,
        byte[] data,
        string? accessPassword = null,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience method to rewrite the Electronic Product Code (EPC) of a tag.
    /// </summary>
    Task<bool> WriteEpcAsync(
        string currentEpc,
        string newEpc,
        string? accessPassword = null,
        CancellationToken ct = default);

    /// <summary>
    /// Locks a memory bank on the target tag using the specified access password.
    /// </summary>
    Task<bool> LockTagAsync(
        string epc,
        MemoryBank bank,
        string accessPassword,
        CancellationToken ct = default);

    /// <summary>
    /// Permanently deactivates (kills) the target tag using its kill password.
    /// </summary>
    Task<bool> KillTagAsync(
        string epc,
        string killPassword,
        CancellationToken ct = default);
}
