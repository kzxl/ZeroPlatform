using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ZeroTensor.Core
{
    /// <summary>
    /// High-performance, zero-dependency NumPy (.npy) file format serializer and deserializer.
    /// Provides seamless zero-copy interoperability with NumPy, PyTorch, and Python scientific libraries.
    /// </summary>
    public static class NpyFormat
    {
        private static readonly byte[] MagicBytes = new byte[] { 0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y' };

        /// <summary>
        /// Saves a tensor to a NumPy (.npy) file.
        /// </summary>
        public static void Save<T>(Tensor<T> tensor, string filePath) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

            using var stream = File.Create(filePath);
            Save(tensor, stream);
        }

        /// <summary>
        /// Saves a tensor to a stream in NumPy (.npy) format.
        /// </summary>
        public static void Save<T>(Tensor<T> tensor, Stream stream) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            string descr = GetNpyTypeDescriptor<T>();
            string shapeStr = FormatNpyShape(tensor.Shape);

            // Construct header string
            string dictHeader = $"{{'descr': '{descr}', 'fortran_order': False, 'shape': {shapeStr}, }}";
            byte[] dictBytes = Encoding.ASCII.GetBytes(dictHeader);

            // Total preamble length = 6 (magic) + 1 (major) + 1 (minor) + 2 (headerLen) = 10
            // Total header size (10 + headerLen) must be a multiple of 64 bytes.
            int currentLen = 10 + dictBytes.Length + 1; // +1 for trailing newline
            int remainder = currentLen % 64;
            int padding = remainder == 0 ? 0 : (64 - remainder);
            int headerLen = dictBytes.Length + padding + 1;

            using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(MagicBytes);
            writer.Write((byte)1); // Major 1
            writer.Write((byte)0); // Minor 0
            writer.Write((ushort)headerLen);

            writer.Write(dictBytes);
            for (int i = 0; i < padding; i++)
            {
                writer.Write((byte)' ');
            }
            writer.Write((byte)'\n');

            // Write contiguous raw binary data
            var contig = tensor.ToContiguous();
            int elementSize = Marshal.SizeOf<T>();
            int byteCount = contig.Length * elementSize;

            byte[] byteBuffer = new byte[byteCount];
            Buffer.BlockCopy(contig.Buffer, contig.Offset * elementSize, byteBuffer, 0, byteCount);
            writer.Write(byteBuffer);
        }

        /// <summary>
        /// Loads a tensor from a NumPy (.npy) file.
        /// </summary>
        public static Tensor<T> Load<T>(string filePath) where T : unmanaged, IEquatable<T>
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            using var stream = File.OpenRead(filePath);
            return Load<T>(stream);
        }

        /// <summary>
        /// Loads a tensor from a stream in NumPy (.npy) format.
        /// </summary>
        public static Tensor<T> Load<T>(Stream stream) where T : unmanaged, IEquatable<T>
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            // Read & verify magic
            byte[] magic = reader.ReadBytes(6);
            if (magic.Length < 6) throw new InvalidDataException("Unexpected end of stream while reading .npy magic.");
            for (int i = 0; i < 6; i++)
            {
                if (magic[i] != MagicBytes[i])
                {
                    throw new InvalidDataException("Invalid .npy magic header.");
                }
            }

            byte major = reader.ReadByte();
            byte minor = reader.ReadByte();

            int headerLen;
            if (major == 1)
            {
                headerLen = reader.ReadUInt16();
            }
            else if (major == 2)
            {
                headerLen = (int)reader.ReadUInt32();
            }
            else
            {
                throw new NotSupportedException($"Unsupported .npy version {major}.{minor}.");
            }

            byte[] headerBytes = reader.ReadBytes(headerLen);
            string headerText = Encoding.ASCII.GetString(headerBytes);

            var (descr, fortranOrder, shape) = ParseNpyHeader(headerText);
            if (fortranOrder)
            {
                throw new NotSupportedException("Fortran-order (column-major) .npy tensors are currently not supported.");
            }

            string expectedDescr = GetNpyTypeDescriptor<T>();
            if (!descr.Equals(expectedDescr, StringComparison.OrdinalIgnoreCase) &&
                !descr.EndsWith(expectedDescr.Substring(1), StringComparison.OrdinalIgnoreCase))
            {
                throw excitingTypeMismatch<T>(descr, expectedDescr);
            }

            var tensor = new Tensor<T>(new TensorShape(shape));
            int elementSize = Marshal.SizeOf<T>();
            int byteCount = tensor.Length * elementSize;

            byte[] rawBytes = reader.ReadBytes(byteCount);
            if (rawBytes.Length < byteCount)
            {
                throw new EndOfStreamException($"Expected {byteCount} bytes of tensor data, but read {rawBytes.Length}.");
            }

            Buffer.BlockCopy(rawBytes, 0, tensor.Buffer, tensor.Offset * elementSize, byteCount);
            return tensor;
        }

        private static Exception excitingTypeMismatch<T>(string actual, string expected)
        {
            return new InvalidDataException($"Data type mismatch in .npy file: expected '{expected}' ({typeof(T).Name}), found '{actual}'.");
        }

        private static string GetNpyTypeDescriptor<T>()
        {
            if (typeof(T) == typeof(float)) return "<f4";
            if (typeof(T) == typeof(double)) return "<f8";
            if (typeof(T) == typeof(int)) return "<i4";
            if (typeof(T) == typeof(long)) return "<i8";
            if (typeof(T) == typeof(short)) return "<i2";
            if (typeof(T) == typeof(byte)) return "|u1";
            if (typeof(T) == typeof(sbyte)) return "|i1";
            if (typeof(T) == typeof(bool)) return "|b1";

            throw new NotSupportedException($"Unsupported element type {typeof(T)} for .npy serialization.");
        }

        private static string FormatNpyShape(TensorShape shape)
        {
            if (shape.Rank == 0) return "()";
            if (shape.Rank == 1) return $"({shape[0]},)";
            return $"({string.Join(", ", shape.Dimensions)})";
        }

        private static (string Descr, bool FortranOrder, int[] Shape) ParseNpyHeader(string header)
        {
            string descr = "";
            bool fortranOrder = false;
            int[] shape = Array.Empty<int>();

            // Parse 'descr': '...'
            int descrIdx = header.IndexOf("'descr'", StringComparison.Ordinal);
            if (descrIdx >= 0)
            {
                int colonIdx = header.IndexOf(':', descrIdx);
                int quote1 = header.IndexOf('\'', colonIdx + 1);
                int quote2 = header.IndexOf('\'', quote1 + 1);
                if (quote1 >= 0 && quote2 > quote1)
                {
                    descr = header.Substring(quote1 + 1, quote2 - quote1 - 1);
                }
            }

            // Parse 'fortran_order': True/False
            int fortranIdx = header.IndexOf("'fortran_order'", StringComparison.Ordinal);
            if (fortranIdx >= 0)
            {
                int colonIdx = header.IndexOf(':', fortranIdx);
                int commaIdx = header.IndexOf(',', colonIdx);
                string val = header.Substring(colonIdx + 1, commaIdx - colonIdx - 1).Trim();
                fortranOrder = bool.Parse(val);
            }

            // Parse 'shape': (...)
            int shapeIdx = header.IndexOf("'shape'", StringComparison.Ordinal);
            if (shapeIdx >= 0)
            {
                int paren1 = header.IndexOf('(', shapeIdx);
                int paren2 = header.IndexOf(')', paren1 + 1);
                if (paren1 >= 0 && paren2 > paren1)
                {
                    string shapeContent = header.Substring(paren1 + 1, paren2 - paren1 - 1).Trim();
                    if (string.IsNullOrEmpty(shapeContent))
                    {
                        shape = Array.Empty<int>();
                    }
                    else
                    {
                        var parts = shapeContent.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape = new int[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                        {
                            shape[i] = int.Parse(parts[i].Trim());
                        }
                    }
                }
            }

            return (descr, fortranOrder, shape);
        }
    }

    public static partial class Tensor
    {
        /// <summary>
        /// Saves this tensor to a NumPy (.npy) file.
        /// </summary>
        public static void SaveNpy<T>(this Tensor<T> tensor, string filePath) where T : unmanaged, IEquatable<T> =>
            NpyFormat.Save(tensor, filePath);

        /// <summary>
        /// Saves this tensor to a stream in NumPy (.npy) format.
        /// </summary>
        public static void SaveNpy<T>(this Tensor<T> tensor, Stream stream) where T : unmanaged, IEquatable<T> =>
            NpyFormat.Save(tensor, stream);

        /// <summary>
        /// Loads a tensor from a NumPy (.npy) file.
        /// </summary>
        public static Tensor<T> LoadNpy<T>(string filePath) where T : unmanaged, IEquatable<T> =>
            NpyFormat.Load<T>(filePath);

        /// <summary>
        /// Loads a tensor from a stream in NumPy (.npy) format.
        /// </summary>
        public static Tensor<T> LoadNpy<T>(Stream stream) where T : unmanaged, IEquatable<T> =>
            NpyFormat.Load<T>(stream);
    }
}
