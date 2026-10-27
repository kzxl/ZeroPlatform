using System;
using System.Data;
using System.Text;
using Xunit;
using ZeroPrimitives.Mapping;

namespace ZeroPrimitives.Tests
{
    public class FastTableBinaryTests
    {
        [Fact]
        public void FastTableBinary_Roundtrip_AllSupportedTypes_Succeeds()
        {
            // Arrange
            var table = new DataTable("TestMasterTable");
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Code", typeof(string));
            table.Columns.Add("Amount", typeof(decimal));
            table.Columns.Add("Ratio", typeof(double));
            table.Columns.Add("Scale", typeof(float));
            table.Columns.Add("IsActive", typeof(bool));
            table.Columns.Add("CreatedAt", typeof(DateTime));
            table.Columns.Add("GuidVal", typeof(Guid));
            table.Columns.Add("RawBytes", typeof(byte[]));
            table.Columns.Add("NullableLong", typeof(long));

            var now = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);
            var guid = Guid.NewGuid();
            byte[] bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            // Row 1: Full data
            table.Rows.Add(1, "MAT-001", 123456.78m, 3.14159, 1.5f, true, now, guid, bytes, 9876543210L);

            // Row 2: Nulls / DBNull
            table.Rows.Add(2, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, false, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);

            // Row 3: Special strings & unicode
            table.Rows.Add(3, "Phiếu Nhập Kho #123 (Vật Tư Tiếng Việt)", 0.0m, 0.0, 0.0f, true, now.AddDays(1), Guid.Empty, Array.Empty<byte>(), 0L);

            // Act: Serialize to ZDT1
            byte[] serialized = FastTableBinary.Serialize(table);
            Assert.NotNull(serialized);
            Assert.True(serialized.Length > 4);

            // Assert Magic Header 'Z', 'D', 'T', 0x01
            Assert.Equal((byte)'Z', serialized[0]);
            Assert.Equal((byte)'D', serialized[1]);
            Assert.Equal((byte)'T', serialized[2]);
            Assert.Equal((byte)0x01, serialized[3]);

            // Act: Deserialize from ZDT1
            DataTable deserialized = FastTableBinary.Deserialize(serialized);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("TestMasterTable", deserialized.TableName);
            Assert.Equal(table.Columns.Count, deserialized.Columns.Count);
            Assert.Equal(table.Rows.Count, deserialized.Rows.Count);

            // Check Row 1
            var r0 = deserialized.Rows[0];
            Assert.Equal(1, r0["Id"]);
            Assert.Equal("MAT-001", r0["Code"]);
            Assert.Equal(123456.78m, r0["Amount"]);
            Assert.Equal(3.14159, (double)r0["Ratio"], 5);
            Assert.Equal(1.5f, (float)r0["Scale"], 2);
            Assert.Equal(true, r0["IsActive"]);
            Assert.Equal(now, (DateTime)r0["CreatedAt"]);
            Assert.Equal(guid, r0["GuidVal"]);
            Assert.Equal(bytes, (byte[])r0["RawBytes"]);
            Assert.Equal(9876543210L, r0["NullableLong"]);

            // Check Row 2 (Nulls)
            var r1 = deserialized.Rows[1];
            Assert.Equal(2, r1["Id"]);
            Assert.Equal(DBNull.Value, r1["Code"]);
            Assert.Equal(DBNull.Value, r1["Amount"]);
            Assert.Equal(DBNull.Value, r1["CreatedAt"]);

            // Check Row 3 (Unicode & Special)
            var r2 = deserialized.Rows[2];
            Assert.Equal(3, r2["Id"]);
            Assert.Equal("Phiếu Nhập Kho #123 (Vật Tư Tiếng Việt)", r2["Code"]);
            Assert.Equal(0.0m, r2["Amount"]);
            Assert.Equal(Guid.Empty, r2["GuidVal"]);
        }

        [Fact]
        public void FastTableBinary_EmptyTable_SerializesAndDeserializesSuccessfully()
        {
            var table = new DataTable("EmptyTable");
            table.Columns.Add("Col1", typeof(string));
            table.Columns.Add("Col2", typeof(int));

            byte[] serialized = FastTableBinary.Serialize(table);
            DataTable result = FastTableBinary.Deserialize(serialized);

            Assert.NotNull(result);
            Assert.Equal("EmptyTable", result.TableName);
            Assert.Equal(2, result.Columns.Count);
            Assert.Empty(result.Rows);
        }

        [Fact]
        public void FastTableBinary_NullAndEmptyInputs_HandledSafelyWithoutCrash()
        {
            byte[] nullTableBytes = FastTableBinary.Serialize(null);
            Assert.Empty(nullTableBytes);

            DataTable fromNull = FastTableBinary.Deserialize((byte[])null!);
            Assert.NotNull(fromNull);
            Assert.Empty(fromNull.Rows);

            DataTable fromEmpty = FastTableBinary.Deserialize(Array.Empty<byte>());
            Assert.NotNull(fromEmpty);
            Assert.Empty(fromEmpty.Rows);

            // Corrupted payload that is not ZDT1 and not BinaryFormatter
            byte[] garbage = new byte[] { 1, 2, 3, 4, 5 };
            DataTable fromGarbage = FastTableBinary.Deserialize(garbage);
            Assert.NotNull(fromGarbage);
            Assert.Empty(fromGarbage.Rows);
        }
    }
}
