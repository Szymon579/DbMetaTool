using DbMetaTool.Model;
using FirebirdSql.Data.FirebirdClient;

namespace DbMetaTool;

public class MetadataRetrievalService(string connStr)
{
    /// <summary>
    /// Queries the RDB$FIELDS system table for user-defined and explicitly named domains, builds its representation
    /// in for of <see cref="DomainInfo"/>. 
    /// </summary>
    /// <returns>List of domain metadata defined in the database.</returns>
    public List<DomainInfo> GetDomains()
    {
        var list = new List<DomainInfo>();
        var sql = """
                  SELECT 
                      F.RDB$FIELD_NAME,       -- domain name
                      F.RDB$FIELD_TYPE,       -- type
                      F.RDB$FIELD_LENGTH,     -- field length
                      F.RDB$FIELD_SCALE,      -- num type scale
                      F.RDB$FIELD_SUB_TYPE,   -- num sub type selector
                      F.RDB$NULL_FLAG,        -- not null modifier
                      F.RDB$DEFAULT_SOURCE,   -- default value declaration
                      F.RDB$VALIDATION_SOURCE -- check constraint
                  FROM RDB$FIELDS F
                  WHERE F.RDB$SYSTEM_FLAG = 0 AND F.RDB$FIELD_NAME NOT LIKE 'RDB$%'
                  """;

        using var conn = new FbConnection(connStr);
        conn.Open();
        using var cmd = new FbCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var domain = new DomainInfo
            {
                Name = reader["RDB$FIELD_NAME"].ToString().Trim(),
                TypeDefinition = DecodeType(
                    Convert.ToInt16(reader["RDB$FIELD_TYPE"]),
                    Convert.ToInt16(reader["RDB$FIELD_LENGTH"]),
                    Convert.ToInt16(reader["RDB$FIELD_SCALE"]),
                    reader["RDB$FIELD_SUB_TYPE"] == DBNull.Value
                        ? (short)0
                        : Convert.ToInt16(reader["RDB$FIELD_SUB_TYPE"])
                ),
                IsNotNull = reader["RDB$NULL_FLAG"] != DBNull.Value && Convert.ToInt16(reader["RDB$NULL_FLAG"]) == 1
            };

            if (reader["RDB$DEFAULT_SOURCE"] != DBNull.Value)
            {
                domain.DefaultExpression = reader["RDB$DEFAULT_SOURCE"].ToString().Trim();
            }

            if (reader["RDB$VALIDATION_SOURCE"] != DBNull.Value)
            {
                domain.CheckConstraint = reader["RDB$VALIDATION_SOURCE"].ToString().Trim();
            }

            list.Add(domain);
        }

        return list;
    }

    /// <summary>
    /// Queries the RDB$RELATIONS table in order to retrieve tables defined in the database.
    /// </summary>
    /// <returns>List of <see cref="TableInfo"/> with table metadata representation.</returns>
    public List<TableInfo> GetTables()
    {
        var list = new List<TableInfo>();

        // only get user defined tables
        var sql = """
                  SELECT RDB$RELATION_NAME 
                  FROM RDB$RELATIONS 
                  WHERE RDB$VIEW_BLR IS NULL AND RDB$SYSTEM_FLAG = 0
                  """;

        using var conn = new FbConnection(connStr);
        conn.Open();
        using var cmd = new FbCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var tableName = reader["RDB$RELATION_NAME"].ToString()?.Trim();
            if (tableName == null)
            {
                Console.WriteLine("Skipped.");
                continue;
            }

            list.Add(new TableInfo
            {
                Name = tableName,
                Columns = GetColumnsForTable(tableName, conn)
            });
        }

        return list;
    }

    /// <summary>
    /// Gets column definitions for passed table.
    /// </summary>
    /// <param name="tableName">Table for which the column's metadata will be retrieved.</param>
    /// <param name="conn">Connection with the database.</param>
    /// <returns>List of <see cref="ColumnInfo"/> metadata of specified table.</returns>
    private List<ColumnInfo> GetColumnsForTable(string tableName, FbConnection conn)
    {
        var cols = new List<ColumnInfo>();

        // Zmienione zapytanie: dodano RF.RDB$DEFAULT_SOURCE
        var sql = """
                  SELECT 
                      RF.RDB$FIELD_NAME, 
                      RF.RDB$FIELD_SOURCE, 
                      F.RDB$FIELD_TYPE, 
                      F.RDB$FIELD_LENGTH, 
                      F.RDB$FIELD_SCALE, 
                      F.RDB$FIELD_SUB_TYPE,
                      RF.RDB$NULL_FLAG, 
                      RF.RDB$DEFAULT_SOURCE 
                  FROM RDB$RELATION_FIELDS RF
                  JOIN RDB$FIELDS F ON RF.RDB$FIELD_SOURCE = F.RDB$FIELD_NAME
                  WHERE RF.RDB$RELATION_NAME = @Tbl
                  ORDER BY RF.RDB$FIELD_POSITION
                  """;

        using var cmd = new FbCommand(sql, conn);
        cmd.Parameters.AddWithValue("Tbl", tableName);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var source = reader["RDB$FIELD_SOURCE"].ToString().Trim();
            var isDomain = !source.StartsWith("RDB$");

            // Dekodowanie typu (tu pamiętaj o swojej poprawce UTF8 / 4!)
            var typeDef = isDomain
                ? source
                : DecodeType(
                    Convert.ToInt16(reader["RDB$FIELD_TYPE"]),
                    Convert.ToInt16(reader["RDB$FIELD_LENGTH"]),
                    Convert.ToInt16(reader["RDB$FIELD_SCALE"]),
                    reader["RDB$FIELD_SUB_TYPE"] == DBNull.Value
                        ? (short)0
                        : Convert.ToInt16(reader["RDB$FIELD_SUB_TYPE"])
                );

            var colInfo = new ColumnInfo
            {
                Name = reader["RDB$FIELD_NAME"].ToString().Trim(),
                TypeDefinition = typeDef,
                // 0 lub NULL = Nullable, 1 = Not Null
                IsNullable = reader["RDB$NULL_FLAG"] == DBNull.Value || Convert.ToInt16(reader["RDB$NULL_FLAG"]) == 0
            };

            // Obsługa DEFAULT
            if (reader["RDB$DEFAULT_SOURCE"] != DBNull.Value)
            {
                colInfo.DefaultExpression = reader["RDB$DEFAULT_SOURCE"].ToString().Trim();
            }

            cols.Add(colInfo);
        }

        return cols;
    }

    /// <summary>
    /// Gets the procedures stored in the database.
    /// </summary>
    /// <returns>List of <see cref="ProcedureInfo"/> containng procedure metadata.</returns>
    public List<ProcedureInfo> GetProcedures()
    {
        var list = new List<ProcedureInfo>();

        // Pobieramy nazwy i ciała procedur
        var sql = """
                  SELECT RDB$PROCEDURE_NAME, RDB$PROCEDURE_SOURCE 
                  FROM RDB$PROCEDURES 
                  WHERE RDB$SYSTEM_FLAG = 0 AND RDB$PROCEDURE_SOURCE IS NOT NULL
                  """;

        using var conn = new FbConnection(connStr);
        conn.Open();
        using var cmd = new FbCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var procName = reader["RDB$PROCEDURE_NAME"].ToString().Trim();
            var proc = new ProcedureInfo
            {
                Name = procName,
                Source = reader["RDB$PROCEDURE_SOURCE"].ToString()
            };

            // TERAZ POBIERAMY PARAMETRY DLA TEJ PROCEDURY
            var allParams = GetProcedureParameters(procName, conn);

            // Dzielimy je na wejściowe (Type 0) i wyjściowe (Type 1)
            proc.InputParameters = allParams.Where(p => p.Type == 0).Select(p => p.Info).ToList();
            proc.OutputParameters = allParams.Where(p => p.Type == 1).Select(p => p.Info).ToList();

            list.Add(proc);
        }

        return list;
    }

    /// <summary>
    /// Retrieves the procedure parameters.
    /// </summary>
    /// <param name="procedureName">Name of the procedure of which the parameters are retrieved.</param>
    /// <param name="conn">Connection to the database.</param>
    /// <returns>List of parameters info per type</returns>
    private static List<(int Type, ParameterInfo Info)> GetProcedureParameters(string procedureName, FbConnection conn)
    {
        var result = new List<(int, ParameterInfo)>();

        var sql = """
                  SELECT 
                      P.RDB$PARAMETER_NAME, 
                      P.RDB$PARAMETER_TYPE, -- 0 = IN, 1 = OUT
                      F.RDB$FIELD_TYPE, 
                      F.RDB$FIELD_LENGTH, 
                      F.RDB$FIELD_SCALE, 
                      F.RDB$FIELD_SUB_TYPE,
                      P.RDB$FIELD_SOURCE 
                  FROM RDB$PROCEDURE_PARAMETERS P
                  LEFT JOIN RDB$FIELDS F ON P.RDB$FIELD_SOURCE = F.RDB$FIELD_NAME
                  WHERE P.RDB$PROCEDURE_NAME = @ProcName
                  ORDER BY P.RDB$PARAMETER_TYPE, P.RDB$PARAMETER_NUMBER
                  """;

        using var cmd = new FbCommand(sql, conn);
        cmd.Parameters.AddWithValue("ProcName", procedureName);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var source = reader["RDB$FIELD_SOURCE"].ToString().Trim();
            var isDomain = !source.StartsWith("RDB$");

            var typeDef = isDomain
                ? source
                : DecodeType(
                    Convert.ToInt16(reader["RDB$FIELD_TYPE"]),
                    Convert.ToInt16(reader["RDB$FIELD_LENGTH"]),
                    Convert.ToInt16(reader["RDB$FIELD_SCALE"]),
                    reader["RDB$FIELD_SUB_TYPE"] == DBNull.Value
                        ? (short)0
                        : Convert.ToInt16(reader["RDB$FIELD_SUB_TYPE"])
                );

            result.Add((
                Convert.ToInt16(reader["RDB$PARAMETER_TYPE"]),
                new ParameterInfo
                {
                    Name = reader["RDB$PARAMETER_NAME"].ToString().Trim(),
                    TypeDefinition = typeDef
                }
            ));
        }

        return result;
    }

    /// <summary>
    /// Decodes internal type codes for SQL type names.
    /// </summary>
    /// <param name="type">Code of type.</param>
    /// <param name="length">Length of type.</param>
    /// <param name="scale">Scale factor of numeric type</param>
    /// <param name="subType">Subtype of numeric types (BIGINT, DECIMAL, NUMERIC).</param>
    /// <remarks>This method assumes that the charset of the DB is UTF-8 to properly compute the datatype length.</remarks>
    /// <returns>String representation of type definition</returns>
    private static string DecodeType(short type, short length, short scale, short subType)
    {
        switch (type)
        {
            case 7: return "SMALLINT";
            case 8: return "INTEGER";
            case 10: return "FLOAT";
            case 12: return "DATE";
            case 13: return "TIME";
            case 14: return $"CHAR({length / 4})"; // assuming UTF-8 charset
            case 16:
                return subType switch
                {
                    1 => $"NUMERIC(18, {-scale})",
                    2 => $"DECIMAL(18, {-scale})",
                    _ => "BIGINT"
                };
            case 27: return "DOUBLE PRECISION";
            case 35: return "TIMESTAMP";
            case 37: return $"VARCHAR({length / 4})"; // assuming UTF-8 charset
            case 261: return "BLOB SUB_TYPE TEXT";
            default: return "VARCHAR(100)";
        }
    }
}