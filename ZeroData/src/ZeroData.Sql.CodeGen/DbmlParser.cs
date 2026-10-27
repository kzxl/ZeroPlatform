using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace ZeroData.Sql.CodeGen
{
    /// <summary>
    /// Parsed DBML model representing database schema.
    /// </summary>
    public class DbmlModel
    {
        public string DatabaseName { get; set; } = "";
        public string ContextClassName { get; set; } = "";
        public List<DbmlTable> Tables { get; set; } = new List<DbmlTable>();
    }

    public class DbmlTable
    {
        public string DbTableName { get; set; } = "";    // e.g. "dbo.tbSYS_User"
        public string MemberName { get; set; } = "";      // e.g. "tbSYS_Users" (property name on context)
        public string TypeName { get; set; } = "";         // e.g. "tbSYS_User" (entity class name)
        public List<DbmlColumn> Columns { get; set; } = new List<DbmlColumn>();
        public List<DbmlAssociation> Associations { get; set; } = new List<DbmlAssociation>();
    }

    public class DbmlColumn
    {
        public string Name { get; set; } = "";            // Column name in DB
        public string ClrType { get; set; } = "";          // e.g. "System.Int64"
        public string DbType { get; set; } = "";           // e.g. "BigInt NOT NULL IDENTITY"
        public bool IsPrimaryKey { get; set; }
        public bool IsDbGenerated { get; set; }
        public bool CanBeNull { get; set; }
    }

    public class DbmlAssociation
    {
        public string Name { get; set; } = "";
        public string MemberName { get; set; } = "";
        public string ThisKey { get; set; } = "";
        public string OtherKey { get; set; } = "";
        public string OtherType { get; set; } = "";
        public bool IsForeignKey { get; set; }
        public string DeleteRule { get; set; } = "";
        public bool DeleteOnNull { get; set; }
    }

    /// <summary>
    /// Parses LINQ to SQL .dbml XML files into DbmlModel.
    /// </summary>
    public static class DbmlParser
    {
        private static readonly XNamespace Ns = "http://schemas.microsoft.com/linqtosql/dbml/2007";

        public static DbmlModel Parse(string dbmlPath)
        {
            var doc = XDocument.Load(dbmlPath);
            var dbElement = doc.Root;

            if (dbElement == null)
                throw new InvalidOperationException($"Invalid DBML file: {dbmlPath}");

            var model = new DbmlModel
            {
                DatabaseName = (string)dbElement.Attribute("Name") ?? "",
                ContextClassName = (string)dbElement.Attribute("Class") ?? "LiteDataContext"
            };

            foreach (var tableEl in dbElement.Elements(Ns + "Table"))
            {
                var table = ParseTable(tableEl);
                if (table != null)
                    model.Tables.Add(table);
            }

            return model;
        }

        private static DbmlTable ParseTable(XElement tableEl)
        {
            var typeEl = tableEl.Element(Ns + "Type");
            if (typeEl == null) return null;

            var table = new DbmlTable
            {
                DbTableName = (string)tableEl.Attribute("Name") ?? "",
                MemberName = (string)tableEl.Attribute("Member") ?? "",
                TypeName = (string)typeEl.Attribute("Name") ?? ""
            };

            foreach (var colEl in typeEl.Elements(Ns + "Column"))
            {
                table.Columns.Add(new DbmlColumn
                {
                    Name = (string)colEl.Attribute("Name") ?? "",
                    ClrType = (string)colEl.Attribute("Type") ?? "System.String",
                    DbType = (string)colEl.Attribute("DbType") ?? "",
                    IsPrimaryKey = (bool?)colEl.Attribute("IsPrimaryKey") ?? false,
                    IsDbGenerated = (bool?)colEl.Attribute("IsDbGenerated") ?? false,
                    CanBeNull = (bool?)colEl.Attribute("CanBeNull") ?? true
                });
            }

            foreach (var assocEl in typeEl.Elements(Ns + "Association"))
            {
                table.Associations.Add(new DbmlAssociation
                {
                    Name = (string)assocEl.Attribute("Name") ?? "",
                    MemberName = (string)assocEl.Attribute("Member") ?? "",
                    ThisKey = (string)assocEl.Attribute("ThisKey") ?? "",
                    OtherKey = (string)assocEl.Attribute("OtherKey") ?? "",
                    OtherType = (string)assocEl.Attribute("Type") ?? "",
                    IsForeignKey = (bool?)assocEl.Attribute("IsForeignKey") ?? false,
                    DeleteRule = (string)assocEl.Attribute("DeleteRule") ?? "",
                    DeleteOnNull = (bool?)assocEl.Attribute("DeleteOnNull") ?? false
                });
            }

            return table;
        }
    }
}
