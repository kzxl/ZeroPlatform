using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZeroData.Sql.CodeGen
{
    /// <summary>
    /// File generation mode for CodeGen output.
    /// </summary>
    public enum FileGenerationMode
    {
        /// <summary>
        /// Generate all entities in a single file (legacy mode).
        /// </summary>
        SingleFile,

        /// <summary>
        /// Generate one file per entity (recommended for large databases).
        /// </summary>
        SplitFiles
    }

    /// <summary>
    /// Represents a generated code file.
    /// </summary>
    public class GeneratedFile
    {
        public string FileName { get; set; }
        public string Content { get; set; }
    }

    /// <summary>
    /// Generates C# source code from a DbmlModel, compatible with ZeroData.Sql.
    /// </summary>
    public static class CodeGenerator
    {
        /// <summary>
        /// C# reserved keywords that need @ prefix when used as identifiers.
        /// </summary>
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal",
            "is", "lock", "long", "namespace", "new", "null", "object",
            "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while",
            // Contextual keywords commonly used as SQL column names
            "add", "from", "get", "group", "into", "join", "let", "orderby",
            "partial", "remove", "select", "set", "value", "var", "where",
            "yield", "view", "order", "level", "delete"
        };

        /// <summary>
        /// Escapes a name with @ prefix if it is a C# reserved keyword.
        /// </summary>
        private static string EscapeIdentifier(string name)
            => CSharpKeywords.Contains(name) ? $"@{name}" : name;

        /// <summary>
        /// Generates multiple files based on the specified mode.
        /// Returns a dictionary where key is the relative file path and value is the file content.
        /// </summary>
        public static Dictionary<string, string> GenerateFiles(
            DbmlModel model,
            string targetNamespace,
            FileGenerationMode mode = FileGenerationMode.SplitFiles)
        {
            var files = new Dictionary<string, string>();

            if (mode == FileGenerationMode.SingleFile)
            {
                // Legacy mode - all in one file
                files[$"{model.ContextClassName}.cs"] = Generate(model, targetNamespace);
            }
            else
            {
                // Split mode - one file per entity
                files[$"Context/{model.ContextClassName}.cs"] = GenerateContextFile(model, targetNamespace);

                foreach (var table in model.Tables)
                {
                    files[$"Entities/{table.TypeName}.cs"] = GenerateEntityFile(table, targetNamespace);
                }
            }

            return files;
        }

        /// <summary>
        /// Generates split files and returns a list of file objects.
        /// Used by CLI for split file generation.
        /// </summary>
        public static List<GeneratedFile> GenerateSplitFiles(DbmlModel model, string targetNamespace)
        {
            var files = new List<GeneratedFile>();

            // Generate context file
            files.Add(new GeneratedFile
            {
                FileName = $"{model.ContextClassName}.cs",
                Content = GenerateContextFile(model, targetNamespace)
            });

            // Generate entity files
            foreach (var table in model.Tables)
            {
                files.Add(new GeneratedFile
                {
                    FileName = $"{table.TypeName}.cs",
                    Content = GenerateEntityFile(table, targetNamespace)
                });
            }

            return files;
        }

        /// <summary>
        /// Generates the complete C# file content for a DBML model (single file mode).
        /// </summary>
        public static string Generate(DbmlModel model, string targetNamespace)
        {
            var sb = new StringBuilder();

            // File header
            AppendFileHeader(sb, model.DatabaseName);
            AppendUsings(sb, includeAll: true);
            sb.AppendLine();
            sb.AppendLine($"namespace {targetNamespace}");
            sb.AppendLine("{");

            // Generate DataContext class
            GenerateContextClass(sb, model);

            // Generate entity classes
            foreach (var table in model.Tables)
            {
                sb.AppendLine();
                GenerateEntityClass(sb, table);
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Generates a standalone file containing only the DataContext class.
        /// </summary>
        public static string GenerateContextFile(DbmlModel model, string targetNamespace)
        {
            var sb = new StringBuilder();

            AppendFileHeader(sb, model.DatabaseName);
            sb.AppendLine("using System.Data;");
            sb.AppendLine("using ZeroData.Sql;");
            sb.AppendLine();
            sb.AppendLine($"namespace {targetNamespace};");
            sb.AppendLine();

            GenerateContextClass(sb, model);

            return sb.ToString();
        }

        /// <summary>
        /// Generates a standalone file containing a single entity class.
        /// </summary>
        public static string GenerateEntityFile(DbmlTable table, string targetNamespace)
        {
            var sb = new StringBuilder();

            AppendFileHeader(sb, $"Entity: {table.TypeName}");
            sb.AppendLine("using System;");
            sb.AppendLine("using ZeroData.Sql.Mapping;");
            sb.AppendLine();
            sb.AppendLine($"namespace {targetNamespace};");
            sb.AppendLine();

            GenerateEntityClass(sb, table);

            return sb.ToString();
        }

        /// <summary>
        /// Appends the auto-generated file header.
        /// </summary>
        private static void AppendFileHeader(StringBuilder sb, string description)
        {
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine($"// Generated by ZeroData.Sql.CodeGen - {description}");
            sb.AppendLine($"// Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("// Do not modify - changes will be overwritten");
        }

        /// <summary>
        /// Appends using statements.
        /// </summary>
        private static void AppendUsings(StringBuilder sb, bool includeAll)
        {
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Data;");
            sb.AppendLine("using ZeroData.Sql;");
            sb.AppendLine("using ZeroData.Sql.Mapping;");
        }

        private static void GenerateContextClass(StringBuilder sb, DbmlModel model)
        {
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// ZeroData.Sql SqlContext for {model.DatabaseName}.");
            sb.AppendLine($"    /// Generated from DBML. Inherits from SqlContext.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public partial class {model.ContextClassName} : SqlContext");
            sb.AppendLine("    {");

            // Static constructor — auto-register SqlConnection factory
            sb.AppendLine($"        static {model.ContextClassName}()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (ConnectionFactory == null)");
            sb.AppendLine("                ConnectionFactory = cs => new Microsoft.Data.SqlClient.SqlConnection(cs);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Constructors
            sb.AppendLine($"        public {model.ContextClassName}(IDbConnection connection) : base(connection) {{ }}");
            sb.AppendLine($"        public {model.ContextClassName}(string connectionString) : base(connectionString) {{ }}");
            sb.AppendLine();

            // Static factory method (common L2S pattern)
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Creates a new instance using DefaultConnectionString.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public static {model.ContextClassName} New()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {model.ContextClassName}(DefaultConnectionString);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Default connection string. Set at app startup.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public static string DefaultConnectionString {{ get; set; }}");
            sb.AppendLine();

            // Typed table properties
            foreach (var table in model.Tables)
            {
                sb.AppendLine($"        public Table<{table.TypeName}> {table.MemberName} => GetTable<{table.TypeName}>();");
            }

            sb.AppendLine("    }");
        }

        private static void GenerateEntityClass(StringBuilder sb, DbmlTable table)
        {
            sb.AppendLine($"    [Table(Name = \"{table.DbTableName}\")]");
            sb.AppendLine($"    public partial class {table.TypeName}");
            sb.AppendLine("    {");

            // Columns
            foreach (var col in table.Columns)
            {
                var attrs = new List<string>();
                attrs.Add($"Name = \"{col.Name}\"");
                attrs.Add($"DbType = \"{col.DbType}\"");

                if (col.IsPrimaryKey)
                    attrs.Add("IsPrimaryKey = true");
                if (col.IsDbGenerated)
                    attrs.Add("IsDbGenerated = true");

                attrs.Add($"CanBeNull = {(col.CanBeNull ? "true" : "false")}");

                sb.AppendLine($"        [Column({string.Join(", ", attrs)})]");

                var clrType = MapClrType(col.ClrType, col.CanBeNull);
                var propName = EscapeIdentifier(col.Name);
                sb.AppendLine($"        public {clrType} {propName} {{ get; set; }}");
                sb.AppendLine();
            }

            // Associations
            var navAssocs = table.Associations.Where(a => !a.IsForeignKey).ToList();
            var fkAssocs = table.Associations.Where(a => a.IsForeignKey).ToList();

            if (navAssocs.Any() || fkAssocs.Any())
            {
                sb.AppendLine("        #region Associations (navigation - not loaded by default)");
                sb.AppendLine();

                // Fix duplicate FK member names: when multiple FKs reference the same type,
                // disambiguate using the FK column name (e.g. idLeader → tbSYS_User_Leader)
                var fkMemberNames = ResolveDuplicateNames(fkAssocs);
                for (int i = 0; i < fkAssocs.Count; i++)
                {
                    var assoc = fkAssocs[i];
                    var memberName = fkMemberNames[i];
                    sb.AppendLine($"        // FK: {assoc.ThisKey} -> {assoc.OtherType}.{assoc.OtherKey}");
                    sb.AppendLine($"        [Association(Name = \"{assoc.Name}\", ThisKey = \"{assoc.ThisKey}\", OtherKey = \"{assoc.OtherKey}\", IsForeignKey = true)]");
                    sb.AppendLine($"        public {assoc.OtherType} {memberName} {{ get; set; }}");
                    sb.AppendLine();
                }

                // Fix duplicate nav member names similarly
                var navMemberNames = ResolveDuplicateNames(navAssocs);
                for (int i = 0; i < navAssocs.Count; i++)
                {
                    var assoc = navAssocs[i];
                    var memberName = navMemberNames[i];
                    sb.AppendLine($"        // Nav: {assoc.OtherType}.{assoc.OtherKey} -> {assoc.ThisKey}");
                    sb.AppendLine($"        // [Association(Name = \"{assoc.Name}\", ThisKey = \"{assoc.ThisKey}\", OtherKey = \"{assoc.OtherKey}\")]");
                    sb.AppendLine($"        // public EntitySet<{assoc.OtherType}> {memberName} {{ get; set; }}");
                    sb.AppendLine();
                }

                sb.AppendLine("        #endregion");
            }

            sb.AppendLine("    }");
        }

        /// <summary>
        /// Resolves duplicate MemberNames in a list of associations.
        /// If multiple associations have the same MemberName, appends a suffix
        /// derived from their key column (e.g. "idLeader" → "_Leader").
        /// </summary>
        private static List<string> ResolveDuplicateNames(List<DbmlAssociation> assocs)
        {
            var names = assocs.Select(a => a.MemberName).ToList();

            // Find duplicates
            var counts = new Dictionary<string, int>();
            foreach (var n in names)
                counts[n] = counts.ContainsKey(n) ? counts[n] + 1 : 1;

            var result = new List<string>();
            for (int i = 0; i < assocs.Count; i++)
            {
                var assoc = assocs[i];
                var baseName = assoc.MemberName;

                if (counts[baseName] > 1)
                {
                    // Disambiguate: use the relevant key column with "id" prefix removed
                    var keyCol = assoc.IsForeignKey ? assoc.ThisKey : assoc.OtherKey;
                    var suffix = keyCol.StartsWith("id", StringComparison.OrdinalIgnoreCase)
                        ? keyCol.Substring(2)
                        : keyCol;
                    result.Add($"{assoc.OtherType}_{suffix}");
                }
                else
                {
                    result.Add(baseName);
                }
            }

            return result;
        }

        /// <summary>
        /// Maps System.* CLR type names to C# type keywords.
        /// Adds ? suffix for nullable value types.
        /// </summary>
        private static string MapClrType(string clrType, bool canBeNull)
        {
            var baseType = clrType switch
            {
                "System.Int16" => "short",
                "System.Int32" => "int",
                "System.Int64" => "long",
                "System.Boolean" => "bool",
                "System.Byte" => "byte",
                "System.Decimal" => "decimal",
                "System.Double" => "double",
                "System.Single" => "float",
                "System.DateTime" => "DateTime",
                "System.DateTimeOffset" => "DateTimeOffset",
                "System.TimeSpan" => "TimeSpan",
                "System.Guid" => "Guid",
                "System.String" => "string",
                "System.Byte[]" => "byte[]",
                "System.Data.Linq.Binary" => "byte[]",
                "System.Char" => "char",
                _ => clrType.StartsWith("System.") ? clrType.Substring(7) : clrType
            };

            // Add nullable suffix for value types that can be null
            if (canBeNull && IsValueType(baseType))
            {
                baseType += "?";
            }

            return baseType;
        }

        private static bool IsValueType(string type)
        {
            return type switch
            {
                "short" or "int" or "long" or "bool" or "byte" or
                "decimal" or "double" or "float" or "char" or
                "DateTime" or "DateTimeOffset" or "TimeSpan" or "Guid" => true,
                _ => false
            };
        }
    }
}
