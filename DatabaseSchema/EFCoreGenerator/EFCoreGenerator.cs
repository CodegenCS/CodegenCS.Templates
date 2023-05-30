﻿using CodegenCS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Console = InterpolatedColorConsole.ColoredConsole;
//using CodegenCS.Models.DbSchema;
using CodegenCS.Runtime;
using System.CommandLine;

#region EFCoreGenerator
/// <summary>
/// EFCoreGenerator.cs: Given a Database Schema will Generate POCOs with a Data Access Layer class (DAL) using Dapper
///
/// Usage: dotnet-codegencs template run EFCoreGenerator.cs <DbSchema.json> <Namespace> [-p:SingleFile] [-p:AddTableAttribute <true/false>] [-p:AddKeyAttribute <true/false>] [-p:AddKeyAttribute <true/false>] [-p:GenerateEqualsHashCode <true/false>] [-p:TrackPropertiesChange <true/false>]
/// e.g.:  dotnet-codegencs template run EFCoreGenerator.cs AdventureWorksSchema.json MyPOCOs -db false
///
/// Arguments:
///  <Namespace>  Namespace of generated POCOs
///
/// Options:
///   --SingleFile                           If set all POCOs will be generated under a single filename
///                                          (default output file)
///   -t, --AddTableAttribute                If true will add [Table] attributes to POCOs. [default: True]
///   -k, --AddKeyAttribute                  If true will add [Key] attributes to primary-key columns.
///                                          This is required by FastCRUD and Entity Framework [default: True]
///   -db, --AddDatabaseGeneratedAttribute   If true will add [DatabaseGenerated] attributes to identity
///                                          and computed columns.
///                                          This is required by FastCRUD and Entity Framework [default: True]
///   -eq, --GenerateEqualsHashCode          If true POCOs will have override Equals/GetHashCode and
///                                          equality/inequality operators (== and !=) [default: True]
/// </summary>
public class EFCoreGenerator : ICodegenMultifileTemplate<CodegenCS.Models.DbSchema.DatabaseSchema>
{
    private ICodegenContext _generatorContext;
    private ILogger _logger;
    private bool _allTablesInSameSchema;
    private bool _duplicatedTableNames;
    private static Dictionary<Table, Dictionary<string, string>> _tablePropertyNames;
    private EFCoreGeneratorOptions _options { get; set; }
    private LogicalSchema _schema { get; set; } // LogicalSchema is ENRICHED on top of CodegenCS.Models.DbSchema.DatabaseSchema

    public EFCoreGenerator(ILogger logger, EFCoreGeneratorOptions options)
    {
        _logger = logger;
        _options = options;
    }

    public static void ConfigureCommand(Command command)
    {
        command.AddArgument(new Argument<string>("ContextName", "DbContext Class Name") { Arity = ArgumentArity.ExactlyOne });
        command.AddArgument(new Argument<string>("EntitiesNamespace", "Namespace of generated Entities") { Arity = ArgumentArity.ExactlyOne });
        command.AddOption(new Option<bool>("-p:SingleFile") { Description = "If defined, all POCOs will be generated under a single filename (default output file)" });
        command.AddOption(new Option<bool>("-p:WithAttributes", getDefaultValue: () => true) { Description = "If true will configure entities using attributes instead of Fluent API" });
    }

    #region EFCoreGeneratorOptions
    public class EFCoreGeneratorOptions : IAutoBindCommandLineArgs
    {

        /// <summary>
        /// DbContext Class Name
        /// </summary>
        public string ContextName { get; set; }

        /// <summary>
        /// Namespace of generated Entities.
        /// </summary>
        public string EntitiesNamespace { get; set; } = "MyEntities";

        public string DbContextNamespace { get { return EntitiesNamespace; } }

        public bool WithAttributes = false;
    }
    #endregion /EFCoreGeneratorOptions


    /// <summary>
    /// Generates Entities and DbContext
    /// </summary>
    public void Render(ICodegenContext context, CodegenCS.Models.DbSchema.DatabaseSchema schema)
    {
        _generatorContext = context;
        _tablePropertyNames = new Dictionary<Table, Dictionary<string, string>>();
        _allTablesInSameSchema = schema.Tables.Select(t => t.TableSchema).Distinct().Count() == 1;
        _duplicatedTableNames = schema.Tables.Select(t => t.TableName).GroupBy(name => name).Where(g => g.Count() > 1).Any();
        if (_duplicatedTableNames)
            _logger.WriteLineAsync(ConsoleColor.Yellow, $"Warning: There are tables with same name (in different schemas?), class names will contain schema...");
        _schema = new LogicalSchema()
        {
            Tables = schema.Tables.Select(t => Map<Table, CodegenCS.Models.DbSchema.Table>(t)).ToList()
        };

        // Define a unique property name for each column
        foreach (var table in _schema.Tables)
        {
            if (!ShouldProcessTable(table))
                continue;
            var columns = table.Columns.Where(c => ShouldProcessColumn(table, c));
            foreach (var column in columns)
            {
                string propertyName = GetPropertyNameForDatabaseColumn(table, column);
            }
        }

        // Define a unique property name for each navigation property and reverse navigation property
        foreach (var table in _schema.Tables)
        {
            foreach (var fk in table.ForeignKeys.ToList())
            {
                var fkTable = table;
                var pkTable = _schema.Tables.SingleOrDefault(t => t.TableSchema == fk.PKTableSchema && t.TableName == fk.PKTableName);
                if (pkTable == null)
                {
                    _logger.WriteLineAsync($"Can't find table {fk.PKTableName}");
                    continue;
                }
                var reverseFk = pkTable.ChildForeignKeys.Single(rfk => rfk.ForeignKeyConstraintName == fk.ForeignKeyConstraintName);

                SetForeignKeyPropertyNames(pkTable, fkTable, fk, reverseFk);
            }

        }

        #region foreach (var table in schema.Tables)
        foreach (var table in _schema.Tables)
        {
            if (!ShouldProcessTable(table))
                continue;
            string entityClassName = GetClassNameForTable(table);

            string tableFilePath = GetFileNameForTable(table);
            _logger.WriteLineAsync($"Generating {tableFilePath}...");
            using (var writer = _generatorContext[tableFilePath])
            {
                writer.WriteLine(@"using System;");
                writer.WriteLine(@"using System.Collections.Generic;");
                if (_options.WithAttributes)
                {
                    writer.WriteLine(@"using System.ComponentModel.DataAnnotations;");
                    writer.WriteLine(@"using System.ComponentModel.DataAnnotations.Schema;");
                }
                writer.WriteLine();
                using (writer.WithCBlock($"namespace {_options.EntitiesNamespace}"))
                {
                    if (_options.WithAttributes && table.TableSchema != "dbo") //TODO or table different than class name?
                        writer.WriteLine($"[Table(\"{table.TableName}\", Schema = \"{table.TableSchema}\")]");
                    else if (entityClassName.ToLower() != table.TableName.ToLower())
                        writer.WriteLine($"[Table(\"{table.TableName}\")]");
                    using (writer.WithCBlock($"public partial class {entityClassName}"))
                    {
                        if (table.ChildForeignKeys.Any())
                        {
                            using (writer.WithCBlock($"public {entityClassName}()"))
                            {
                                foreach (var fk in table.ChildForeignKeys.OrderBy(fk => fk.NavigationPropertyName))
                                {
                                    var fkTable = _schema.Tables.Single(t => t.TableSchema == fk.FKTableSchema && t.TableName == fk.FKTableName);
                                    writer.WriteLine($"{fk.NavigationPropertyName} = new HashSet<{GetClassNameForTable(fkTable)}>();");
                                }
                            }
                            writer.WriteLine();
                        }

                        var columns = table.Columns.Where(c => ShouldProcessColumn(table, c));
                        foreach (var column in columns)
                        {
                            string propertyName = GetPropertyNameForDatabaseColumn(table, column);
                            string clrType = GetTypeDefinitionForDatabaseColumn(table, column) ?? "";
                            if (_options.WithAttributes)
                            {
                                if (column.IsPrimaryKeyMember)
                                    writer.WriteLine("[Key]");
                                if (!column.IsNullable && clrType == "System.String" && !column.IsPrimaryKeyMember) // reference types are always nullable (no need "?"), so must specify this.
                                    writer.WriteLine($"[Required]");

                                string typeName = null; // TODO: combine with identical block
                                if (column.SqlDataType == "datetime" || column.SqlDataType == "smallmoney" || column.SqlDataType == "money" || column.SqlDataType == "xml")
                                    typeName = column.SqlDataType;
                                else if (column.SqlDataType == "decimal")
                                    typeName = $"decimal({column.NumericPrecision}, {column.NumericScale})";

                                if (column.ColumnName != propertyName && typeName != null)
                                    writer.WriteLine($"[Column(\"{column.ColumnName}\", TypeName = \"{typeName}\")]");
                                else if (column.ColumnName != propertyName && typeName == null)
                                    writer.WriteLine($"[Column(\"{column.ColumnName}\")]");
                                else if (column.ColumnName == propertyName && typeName != null)
                                    writer.WriteLine($"[Column(TypeName = \"{typeName}\")]");

                                if (clrType == "System.String" && column.MaxLength != -1)
                                    writer.WriteLine($"[StringLength({column.MaxLength})]");
                            }

                            writer.WriteLine($"public {clrType} {propertyName} {{ get; set; }}");
                        }

                        if (table.ForeignKeys.Any() || table.ChildForeignKeys.Any())
                            writer.WriteLine();
                        foreach (var childToParentFK in table.ForeignKeys.OrderBy(fk => fk.NavigationPropertyName))
                        {
                            var fkTable = table;
                            var pkTable = _schema.Tables.Single(t => t.TableSchema == childToParentFK.PKTableSchema && t.TableName == childToParentFK.PKTableName);
                            var parentToChildFK = pkTable.ChildForeignKeys.Single(fk => fk.ForeignKeyConstraintName == childToParentFK.ForeignKeyConstraintName);

                            var fkCol = childToParentFK.Columns.First().FKColumnName; //TODO: composite keys
                            _logger.WriteLineAsync($"{table.TableName}{fkCol}");
                            if (_options.WithAttributes)
                            {
                                writer.WriteLine($"[ForeignKey(nameof({table.ColumnPropertyNames[fkCol]}))]");
                                writer.WriteLine($"[InverseProperty(nameof({GetClassNameForTable(pkTable)}.{parentToChildFK.NavigationPropertyName}))]");
                            }
                            writer.WriteLine($"public virtual {GetClassNameForTable(pkTable)} {childToParentFK.NavigationPropertyName} {{ get; set; }}");
                        }
                        foreach (var parentToChildFK in table.ChildForeignKeys.OrderBy(fk => fk.NavigationPropertyName))
                        {
                            var pkTable = table;
                            var fkTable = _schema.Tables.Single(t => t.TableSchema == parentToChildFK.FKTableSchema && t.TableName == parentToChildFK.FKTableName);
                            var childToParentFK = fkTable.ForeignKeys.Single(fk => fk.ForeignKeyConstraintName == parentToChildFK.ForeignKeyConstraintName);
                            var fkCol = parentToChildFK.Columns.First().FKColumnName; //TODO: composite keys
                            if (_options.WithAttributes)
                            {
                                //writer.WriteLine($"[InverseProperty(nameof({GetClassNameForTable(fkTable)}.{fk.ReverseNavigationPropertyName}))]"); // some cases attribute is set by nameof?
                                writer.WriteLine($"[InverseProperty(\"{childToParentFK.NavigationPropertyName}\")]"); // some cases attribute is set by nameof?
                            }
                            writer.WriteLine($"public virtual ICollection<{GetClassNameForTable(fkTable)}> {parentToChildFK.NavigationPropertyName} {{ get; set; }} ");
                        }
                    }
                }
            }
        }
        #endregion

        #region DbContext
        using (var dbContextWriter = _generatorContext[_options.ContextName + ".cs"])
        {
            dbContextWriter.PreserveNonWhitespaceIndent = false;
            dbContextWriter.WriteLine("using System;");
            dbContextWriter.WriteLine("using Microsoft.EntityFrameworkCore;");
            dbContextWriter.WriteLine("using Microsoft.EntityFrameworkCore.Metadata;");
            dbContextWriter.WriteLine("");
            using (dbContextWriter.WithCBlock($"namespace {_options.DbContextNamespace}"))
            {
                using (dbContextWriter.WithCBlock($"public partial class {_options.ContextName} : DbContext"))
                {
                    using (dbContextWriter.WithCBlock($"public {_options.ContextName}()"))
                    {
                    }
                    dbContextWriter.WriteLine();

                    using (dbContextWriter.WithCBlock($"public {_options.ContextName}(DbContextOptions<{_options.ContextName}> options){Environment.NewLine}    : base(options)"))
                    {
                    }
                    dbContextWriter.WriteLine();

                    foreach (var table in _schema.Tables.OrderBy(t => GetClassNameForTable(t)))
                    {
                        if (!ShouldProcessTable(table))
                            continue;
                        string entityClassName = GetClassNameForTable(table);

                        dbContextWriter.WriteLine($"public virtual DbSet<{entityClassName}> {entityClassName} {{ get; set; }}");
                    }

                    dbContextWriter.WriteLine();
                    dbContextWriter.WriteLine();
                    dbContextWriter.WriteLine();
                    using (dbContextWriter.WithCBlock($"protected override void OnModelCreating(ModelBuilder modelBuilder)"))
                    {
                        foreach (var table in _schema.Tables.OrderBy(t => GetClassNameForTable(t)))
                        {

                            if (!ShouldProcessTable(table))
                                continue;
                            string entityClassName = GetClassNameForTable(table);
                            using (dbContextWriter.WithIndent($"modelBuilder.Entity<{GetClassNameForTable(table)}>(entity =>{Environment.NewLine}{{{Environment.NewLine}", $"{Environment.NewLine}}});{Environment.NewLine}{Environment.NewLine}"))
                            {
                                var pkCols = table.Columns.Where(c => c.IsPrimaryKeyMember);
                                if (pkCols.Any() && !string.IsNullOrEmpty(table.PrimaryKeyName))
                                {
                                    if (pkCols.Count() == 1)
                                        dbContextWriter.Write($"entity.HasKey(e => e.{GetPropertyNameForDatabaseColumn(table, pkCols.Single())})");
                                    else
                                        dbContextWriter.Write($"entity.HasKey(e => new {{ " +
                                            string.Join(", ", pkCols.Select(pk => $"e.{GetPropertyNameForDatabaseColumn(table, pk)}")) +
                                            $"}})");

                                    List<string> commands = new List<string>();
                                    dbContextWriter.Write($"{Environment.NewLine}    .HasName(\"{table.PrimaryKeyName}\")");
                                    //commands.Add($"    .HasName(\"{table.PrimaryKeyName}\");");
                                    if (!table.PrimaryKeyIsClustered)
                                        dbContextWriter.Write($"{Environment.NewLine}    .IsClustered(false)");
                                    //commands.Add($"    .IsClustered(false);");
                                    //Extensions.WriteChainedMethods(dbContextWriter, commands); // dbContextWriter.WriteChainedMethods(commands); - CSX doesn't allow extensions
                                    dbContextWriter.WriteLine($";{Environment.NewLine}");
                                }
                                else
                                    dbContextWriter.WriteLine($"entity.HasNoKey();{Environment.NewLine}");

                                if (!_options.WithAttributes)
                                    dbContextWriter.WriteLine($"entity.ToTable(\"{table.TableName}\", \"{table.TableSchema}\");{Environment.NewLine}");

                                if (!string.IsNullOrEmpty(table.TableDescription))
                                {
                                    dbContextWriter.WriteLine($"entity.HasComment(@\"{table.TableDescription.Trim().Replace("\"", "\\\"")}\");");
                                    dbContextWriter.WriteLine();
                                }

                                dbContextWriter.Write($"{null}");

                                foreach (var index in table.Indexes
                                    .Where(i => i.PhysicalType == "CLUSTERED" || i.PhysicalType == "NONCLUSTERED")
                                    .Where(i => i.LogicalType != "PRIMARY_KEY")
                                    .Where(i => i.Columns.Any())
                                    .OrderBy(i => GetPropertyNameForDatabaseColumn(table, i.Columns.First().ColumnName))
                                    )
                                {
                                    dbContextWriter.Write($"entity.HasIndex(e => e.{GetPropertyNameForDatabaseColumn(table, index.Columns.First().ColumnName)})");
                                    dbContextWriter.Write($"{Environment.NewLine}    .HasName(\"{index.IndexName}\")");
                                    if (index.LogicalType == "UNIQUE_INDEX" || index.LogicalType == "UNIQUE_CONSTRAINT")
                                        dbContextWriter.Write($"{Environment.NewLine}    .IsUnique()");
                                    dbContextWriter.WriteLine($";{Environment.NewLine}");
                                }

                                foreach (var column in table.Columns
                                    .OrderBy(c => c.IsPrimaryKeyMember ? 0 : 1)
                                    .ThenBy(c => c.IsPrimaryKeyMember ? c.OrdinalPosition : 0) // respect PK order... 
                                    .ThenBy(c => GetPropertyNameForDatabaseColumn(table, c)) // but for other columns do alphabetically
                                    )
                                {
                                    dbContextWriter.Write($"entity.Property(e => e.{GetPropertyNameForDatabaseColumn(table, column)})");
                                    if (!_options.WithAttributes && column.ColumnName != GetPropertyNameForDatabaseColumn(table, column))
                                        dbContextWriter.Write($"{Environment.NewLine}    .HasColumnName(\"{column.ColumnName}\")");

                                    string typeName = null; // TODO: combine with identical block
                                    if (column.SqlDataType == "datetime" || column.SqlDataType == "smallmoney" || column.SqlDataType == "money" || column.SqlDataType == "xml")
                                        typeName = column.SqlDataType;
                                    else if (column.SqlDataType == "decimal")
                                        typeName = $"decimal({column.NumericPrecision}, {column.NumericScale})";
                                    if (typeName != null)
                                        dbContextWriter.Write($"{Environment.NewLine}    .HasColumnType(\"{typeName}\")");

                                    string defaultSetting = column.DefaultSetting;
                                    if (!string.IsNullOrEmpty(defaultSetting))
                                    {
                                        try
                                        {
                                            Type clrType = Type.GetType(column.ClrType);
                                            if ((clrType == typeof(int) ||
                                                clrType == typeof(decimal) ||
                                                clrType == typeof(byte) ||
                                                clrType == typeof(float) ||
                                                clrType == typeof(long) ||
                                                clrType == typeof(double) ||
                                                clrType == typeof(short) ||
                                                clrType == typeof(uint) ||
                                                clrType == typeof(ulong)
                                                ) && !column.IsNullable && defaultSetting == "((0))")
                                                defaultSetting = null;
                                            //TODO: object def = GetDefault(clrType);

                                        }
                                        catch (Exception ex)
                                        {
                                        }
                                    }
                                    if (defaultSetting != null) //TODO: non-nullable numerics will have default 0, so ((0)) can be ignored. etc.
                                        dbContextWriter.Write($"{Environment.NewLine}    .HasDefaultValueSql(\"{defaultSetting}\")");

                                    if (!column.IsNullable && column.ClrType == "System.String" && !column.IsPrimaryKeyMember) // reference types are always nullable (no need "?"), so must specify this.
                                        dbContextWriter.Write($"{Environment.NewLine}    .IsRequired()");
                                    if (column.ClrType == "System.String" && column.MaxLength != -1)
                                        dbContextWriter.Write($"{Environment.NewLine}    .HasMaxLength({column.MaxLength})");
                                    if (column.SqlDataType == "char" || column.SqlDataType == "nchar")
                                        dbContextWriter.Write($"{Environment.NewLine}    .IsFixedLength()");

                                    if (!string.IsNullOrEmpty(column.ColumnDescription))
                                        dbContextWriter.Write($"{Environment.NewLine}    .HasComment(@\"{column.ColumnDescription.Trim().Replace("\"", "\\\"")}\")");
                                    dbContextWriter.WriteLine($";{Environment.NewLine}");
                                    /*
                                    bool hasLineBreaks = false;

                                    if (!string.IsNullOrEmpty(c.DefaultSetting))
                                    {
                                        if (!hasLineBreaks) { dbContextWriter.IncreaseIndent(); hasLineBreaks = true; }
                                        dbContextWriter.WriteLine($"{Environment.NewLine}.HasDefaultValueSql(\"{c.DefaultSetting}\")");
                                    }
                                    if (c.SqlDataType == "char" || c.SqlDataType == "nchar")
                                    {
                                        if (!hasLineBreaks) { dbContextWriter.IncreaseIndent(); hasLineBreaks = true; }
                                        dbContextWriter.WriteLine($"{Environment.NewLine}.IsFixedLength()");
                                    }

                                    if (!string.IsNullOrEmpty(c.ColumnDescription))
                                        dbContextWriter.WriteLine($".HasComment(@\"{c.ColumnDescription.Trim().Replace("\"", "\\\"")}\");");

                                    if (hasLineBreaks)
                                        dbContextWriter.DecreaseIndent();
                                    dbContextWriter.WriteLine();
                                    */
                                }
                                foreach (var childToParentFK in table.ForeignKeys
                                        .OrderBy(fk => fk.NavigationPropertyName)
                                        )
                                {
                                    var fkTable = table;
                                    var pkTable = _schema.Tables.Single(t => t.TableSchema == childToParentFK.PKTableSchema && t.TableName == childToParentFK.PKTableName);
                                    var parentToChildFK = pkTable.ChildForeignKeys.Single(fk => fk.ForeignKeyConstraintName == childToParentFK.ForeignKeyConstraintName);

                                    var fkCol = fkTable.Columns.Single(c => c.ColumnName == childToParentFK.Columns.First().FKColumnName); //TODO: composite keys

                                    dbContextWriter.Write($"{Environment.NewLine}entity.HasOne(d => d.{childToParentFK.NavigationPropertyName})");
                                    using (dbContextWriter.WithIndent())
                                    {
                                        dbContextWriter.Write($"{Environment.NewLine}.WithMany(p => p.{parentToChildFK.NavigationPropertyName})");
                                        dbContextWriter.Write($"{Environment.NewLine}.HasForeignKey(d => d.{GetPropertyNameForDatabaseColumn(fkTable, fkCol)})");

                                        // NO_ACTION seems like a bug in ef dbcontext scaffold when we use -d (annotations) ?
                                        if (parentToChildFK.OnDeleteCascade == "SET_NULL" || (_options.WithAttributes && parentToChildFK.OnDeleteCascade == "NO_ACTION"))
                                            dbContextWriter.Write($"{Environment.NewLine}.OnDelete(DeleteBehavior.ClientSetNull)");

                                        dbContextWriter.WriteLine($";");
                                    }
                                }
                            }
                        }

                    }

                }
            }
        }
        #endregion
    }

    string GetFileNameForTable(Table table)
    {
        return $"{table.TableName}.cs";
        if (table.TableSchema == "dbo")
            return $"{table.TableName}.cs";
        else
            return $"{table.TableSchema}.{table.TableName}.cs";
    }
    string GetClassNameForTable(Table table)
    {
        return $"{table.TableName}";
        if (table.TableSchema == "dbo")
            return $"{table.TableName}";
        else
            return $"{table.TableSchema}_{table.TableName}";
    }
    bool ShouldProcessTable(Table table)
    {
        if (table.TableType == "VIEW")
            return false;
        //if (table.TableName.StartsWith("CK_")) // check constraints
        //    return false;
        //if (table.TableSchema == "audit")
        //    return false;
        //if (table.TableName.StartsWith("Audit_"))
        //    return false;
        if (table.TableName.StartsWith("QRTZ_"))
            return false;
        if (table.TableName.StartsWith("webpages_"))
            return false;
        if (table.TableName.EndsWith("2"))
            return false;
        if (table.TableName.EndsWith("TMP"))
            return false;
        if (table.TableName == "Job" || table.TableName == "JobParameter" || table.TableName == "JobQueue" || table.TableName == "Role" || table.TableName == "Schema" || table.TableName == "Server" || table.TableName == "Set" || table.TableName == "State" || table.TableName == "Hash" || table.TableName == "Counter")
            return false;
        return true;
    }

    bool ShouldProcessColumn(Table table, Column column)
    {
        string sqlDataType = column.SqlDataType;
        switch (sqlDataType)
        {
            case "hierarchyid":
            case "geography":
                return true; // some databases may not allow these types
            default:
                break;
        }

        return true;
    }

    static Dictionary<Type, string> _typeAlias = new Dictionary<Type, string>
    {
        { typeof(bool), "bool" },
        { typeof(byte), "byte" },
        { typeof(char), "char" },
        { typeof(decimal), "decimal" },
        { typeof(double), "double" },
        { typeof(float), "float" },
        { typeof(int), "int" },
        { typeof(long), "long" },
        { typeof(object), "object" },
        { typeof(sbyte), "sbyte" },
        { typeof(short), "short" },
        { typeof(string), "string" },
        { typeof(uint), "uint" },
        { typeof(ulong), "ulong" },
        // Yes, this is an odd one.  Technically it's a type though.
        { typeof(void), "void" }
    };

    string GetTypeDefinitionForDatabaseColumn(Table table, Column column)
    {
        string typeName;
        bool isReferenceType;
        try
        {
            System.Type type = Type.GetType(column.ClrType);

            if (type == null)
            {
                _logger.WriteLineAsync($"Warning - unknown Type {column.ClrType} - you may need to add some reference to your project");
                typeName = column.ClrType;
                isReferenceType = true; // non-standard types are probably reference types
            }
            else
            {
                isReferenceType = !type.IsValueType;

                // Prefer shorter type aliases (int instead of Int32, long instead of Int64, string instead of String)
                if (_typeAlias.TryGetValue(type, out string alias))
                    typeName = alias;
                else if (type.IsArray && _typeAlias.TryGetValue(type.GetElementType(), out string alias2))
                    typeName = alias2 + "[]";
                else
                    typeName = type.Name;
            }
        }
        catch (Exception ex)
        {
            // some types are vendor-specific and may require specific DLLs (e.g. Microsoft.SqlServer.Types.SqlGeography)
            _logger.WriteLineAsync($"Warning - unknown Type {column.ClrType} - you may need to add some reference to your project");
            typeName = column.ClrType;
            isReferenceType = true; // non-standard types are probably reference types
        }

        bool isNullable = column.IsNullable;

        // Many developers use POCO instances with null Primary Key to represent a new (in-memory) object, so we can force PKs as nullable
        //if (column.IsPrimaryKeyMember)
        //    isNullable = true;

        // Reference-types (including strings) are always nullable, no need to specify nullable modifier
        if (isNullable && !isReferenceType)
            return $"{typeName}?"; // some might prefer $"System.Nullable<{typeName}>"

        return typeName;
    }

    // From PetaPoco - https://github.com/CollaboratingPlatypus/PetaPoco/blob/development/T4Templates/PetaPoco.Core.ttinclude
    private static Regex rxCleanUp = new Regex(@"[^\w\d_]", RegexOptions.Compiled);
    private static string[] cs_keywords = { "abstract", "event", "new", "struct", "as", "explicit", "null",
		    "switch", "base", "extern", "object", "this", "bool", "false", "operator", "throw",
		    "break", "finally", "out", "true", "byte", "fixed", "override", "try", "case", "float",
		    "params", "typeof", "catch", "for", "private", "uint", "char", "foreach", "protected",
		    "ulong", "checked", "goto", "public", "unchecked", "class", "if", "readonly", "unsafe",
		    "const", "implicit", "ref", "ushort", "continue", "in", "return", "using", "decimal",
		    "int", "sbyte", "virtual", "default", "interface", "sealed", "volatile", "delegate",
		    "internal", "short", "void", "do", "is", "sizeof", "while", "double", "lock",
		    "stackalloc", "else", "long", "static", "enum", "namespace", "string" };

    string GetPropertyNameForDatabaseColumn(Table table, Column column)
    {
        return GetPropertyNameForDatabaseColumn(table, column.ColumnName);
    }

    /// <summary>
    /// Gets a unique identifier name for the column, which doesn't conflict with the Entity class itself or with previous identifiers for this Entity.
    /// </summary>
    /// <returns></returns>
    string GetPropertyNameForDatabaseColumn(Table table, string columnName)
    {
        if (table.ColumnPropertyNames.ContainsKey(columnName))
            return table.ColumnPropertyNames[columnName];

        string name = columnName;

        // Replace forbidden characters
        name = rxCleanUp.Replace(name, "_");

        // Split multiple words
        var parts = splitUpperCase.Split(name).Where(part => part != "_" && part != "-").ToList();
        // we'll put first word into TitleCase except if it's a single-char in lowercase (like vNameOfTable) which we assume is a prefix (like v for views) and should be preserved as is
        // if first world is a single-char in lowercase (like vNameOfTable) which we assume is a prefix (like v for views) and should be preserved as is

        // Recapitalize (to TitleCase) all words
        for (int i = 0; i < parts.Count; i++)
        {
            // if first world is a single-char in lowercase (like vNameOfTable), we assume it's a prefix (like v for views) and should be preserved as is
            if (i == 0 && parts[i].Length == 1 && parts[i].ToLower() != parts[i])
                continue;

            switch (parts[i])
            {
                //case "ID": // don't convert "ID" for "Id"
                //    break;
                default:
                    parts[i] = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(parts[i].ToLower());
                    break;
            }
        }

        name = string.Join("", parts);

        // can't start with digit
        if (char.IsDigit(name[0]))
            name = "_" + name;

        // can't be a reserved keyword
        if (cs_keywords.Contains(name))
            name = "@" + name;

        // check for name clashes
        int n = 0;
        string attemptName = name;
        while ((GetClassNameForTable(table) == attemptName || table.ColumnPropertyNames.ContainsValue((attemptName))) && n < 100)
        {
            n++;
            attemptName = name + n.ToString();
        }
        table.ColumnPropertyNames.Add(columnName, attemptName);

        return attemptName;
    }

    // Splits both camelCaseWords and also TitleCaseWords. Underscores and dashes are also splitted. Uppercase acronyms are also splitted.
    // E.g. "BusinessEntityID" becomes ["Business","Entity","ID"]
    // E.g. "Employee_SSN" becomes ["employee","_","SSN"]
    private static Regex splitUpperCase = new Regex(@"
            (?<=[A-Z])(?=[A-Z][a-z0-9]) |
                (?<=[^A-Z])(?=[A-Z]) |
                (?<=[A-Za-z0-9])(?=[^A-Za-z0-9])", RegexOptions.IgnorePatternWhitespace);

    /// <summary>
    /// Sets unique names for NavigationProperty (from child fkTable to parent pkTable) and ReverseNavigationProperty (from parent pkTable to child fkTable)
    /// Name should not conflict with the Entity class itself or with previous identifiers for this Entity.
    /// </summary>
    /// <returns></returns>
    void SetForeignKeyPropertyNames(Table pkTable, Table fkTable, ForeignKey fk, ForeignKey reverseFk)
    {
        string navigationPropertyName = fk.Columns.First().FKColumnName; //TODO: composite keys?
        if (navigationPropertyName.ToUpper().EndsWith("ID"))
            navigationPropertyName = navigationPropertyName.Substring(0, navigationPropertyName.Length - 2);

        // check for name clashes
        int n = 0;
        string attemptName = navigationPropertyName;
        while ((GetClassNameForTable(fkTable) == attemptName || fkTable.ColumnPropertyNames.ContainsValue(attemptName) || fkTable.FKPropertyNames.ContainsValue(attemptName) || fkTable.ReverseFKPropertyNames.ContainsValue(attemptName)) && n < 100)
        {
            n++;
            attemptName = navigationPropertyName + n.ToString();
        }
        fk.NavigationPropertyName = attemptName;
        fkTable.FKPropertyNames[fk.ForeignKeyConstraintName] = attemptName;

        string reverseNavigationPropertyName = GetClassNameForTable(fkTable);// + navigationPropertyName;
        attemptName = reverseNavigationPropertyName;
        while ((GetClassNameForTable(pkTable) == attemptName || pkTable.ColumnPropertyNames.ContainsValue(attemptName) || pkTable.FKPropertyNames.ContainsValue(attemptName) || pkTable.ReverseFKPropertyNames.ContainsValue(attemptName)) && n < 100)
        {
            n++;
            attemptName = reverseNavigationPropertyName + n.ToString();
        }
        reverseFk.NavigationPropertyName = attemptName;
        pkTable.FKPropertyNames[fk.ForeignKeyConstraintName] = attemptName;
    }

    public static object GetDefault(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        return null;
    }

    public static T Map<T, S>(S source)
    {
        var serialized = JsonConvert.SerializeObject(source);
        return JsonConvert.DeserializeObject<T>(serialized);
    }
}
#endregion /EFCoreGenerator

#region LogicalSchema
/*************************************************************************************************************************
    The serialized JSON schema (http://codegencs.com/schemas/dbschema/2021-07/dbschema.json) has only Physical Properties.
    Here we extend the Physical definitions with some new Logical definitions.
    For example: ForeignKeys in a logical model have the "Navigation Property Name".
    And POCOs (mapped 1-to-1 by Entities) track the list of Property Names used by Columns, used by Navigation Properties, etc., 
    to avoid naming conflicts.
**************************************************************************************************************************/

public class LogicalSchema : CodegenCS.Models.DbSchema.DatabaseSchema
{
    public new List<Table> Tables { get; set; }
}
public class Table : CodegenCS.Models.DbSchema.Table
{
    public Dictionary<string, string> ColumnPropertyNames { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> FKPropertyNames { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ReverseFKPropertyNames { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public new List<Column> Columns { get; set; } = new List<Column>();
    public new List<ForeignKey> ForeignKeys { get; set; } = new List<ForeignKey>();
    public new List<ForeignKey> ChildForeignKeys { get; set; } = new List<ForeignKey>();
}

public class ForeignKey : CodegenCS.Models.DbSchema.ForeignKey
{
    public string NavigationPropertyName { get; set; }
}
public class Column : CodegenCS.Models.DbSchema.Column
{

}


#endregion /LogicalSchema

