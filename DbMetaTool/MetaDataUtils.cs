using FirebirdSql.Data.FirebirdClient;
using System.Text;

namespace DbMetaTool {
    public static class MetaDataUtils {
        public static List<string> ExportDomains(FbConnection conn) {
            var list = new List<string>();
            list.Add("-- DOMAINS");

            const string sql = @"
                SELECT
                    TRIM(RDB$FIELD_NAME) AS NAME,
                    RDB$FIELD_TYPE,
                    RDB$FIELD_SUB_TYPE,
                    RDB$FIELD_LENGTH,
                    RDB$CHARACTER_LENGTH,
                    RDB$FIELD_PRECISION,
                    RDB$FIELD_SCALE,
                    RDB$NULL_FLAG
                FROM RDB$FIELDS
                WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0
                ORDER BY RDB$FIELD_NAME;
            ";

            using var cmd = new FbCommand(sql, conn);
            using var rdr = cmd.ExecuteReader();

            while (rdr.Read()) {
                string name = rdr.GetString(rdr.GetOrdinal("NAME"));

                short fieldType = rdr.GetInt16(rdr.GetOrdinal("RDB$FIELD_TYPE"));
                short subType = rdr.IsDBNull(rdr.GetOrdinal("RDB$FIELD_SUB_TYPE"))
                    ? (short)0 : rdr.GetInt16(rdr.GetOrdinal("RDB$FIELD_SUB_TYPE"));
                int length = rdr.IsDBNull(rdr.GetOrdinal("RDB$FIELD_LENGTH"))
                    ? 0 : rdr.GetInt32(rdr.GetOrdinal("RDB$FIELD_LENGTH"));
                int charLen = rdr.IsDBNull(rdr.GetOrdinal("RDB$CHARACTER_LENGTH"))
                    ? 0 : rdr.GetInt32(rdr.GetOrdinal("RDB$CHARACTER_LENGTH"));
                short precision = rdr.IsDBNull(rdr.GetOrdinal("RDB$FIELD_PRECISION"))
                    ? (short)0 : rdr.GetInt16(rdr.GetOrdinal("RDB$FIELD_PRECISION"));
                short scale = rdr.IsDBNull(rdr.GetOrdinal("RDB$FIELD_SCALE"))
                    ? (short)0 : rdr.GetInt16(rdr.GetOrdinal("RDB$FIELD_SCALE"));
                bool notNull = !rdr.IsDBNull(rdr.GetOrdinal("RDB$NULL_FLAG"));

                string sqlType = MapType(fieldType, subType, length, charLen, precision, scale);
                list.Add($"CREATE DOMAIN {name} AS {sqlType}{(notNull ? " NOT NULL" : "")};");
            }

            return list;
        }

        public static List<string> ExportTables(FbConnection conn) {
            var list = new List<string>();
            list.Add("-- TABLES");

            const string sql = @"
                SELECT TRIM(RDB$RELATION_NAME) AS NAME
                FROM RDB$RELATIONS
                WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0
                  AND (RDB$VIEW_BLR IS NULL OR RDB$VIEW_BLR = '')
                ORDER BY RDB$RELATION_NAME;
            ";

            var tableNames = new List<string>();

            using (var cmd = new FbCommand(sql, conn))
            using (var rdr = cmd.ExecuteReader()) {
                while (rdr.Read())
                    tableNames.Add(rdr.GetString(rdr.GetOrdinal("NAME")).Trim());
            }

            foreach (var table in tableNames) {
                list.Add("");
                list.Add($"-- Table: {table}");
                list.Add($"CREATE TABLE {table} (");

                var cols = GetColumns(conn, table);
                for (int i = 0; i < cols.Count; i++) {
                    string comma = (i < cols.Count - 1) ? "," : "";
                    list.Add($"    {cols[i]}{comma}");
                }

                list.Add(");");
            }

            return list;
        }

        private static List<string> GetColumns(FbConnection conn, string table) {
            var cols = new List<string>();

            const string sql = @"
                SELECT
                    TRIM(rf.RDB$FIELD_NAME) AS COL_NAME,
                    TRIM(rf.RDB$FIELD_SOURCE) AS FIELD_SOURCE,
                    f.RDB$FIELD_TYPE,
                    f.RDB$FIELD_SUB_TYPE,
                    f.RDB$FIELD_LENGTH,
                    f.RDB$CHARACTER_LENGTH,
                    f.RDB$FIELD_PRECISION,
                    f.RDB$FIELD_SCALE,
                    rf.RDB$NULL_FLAG,
                    COALESCE(f.RDB$SYSTEM_FLAG, 0) AS SYS_FLAG
                FROM RDB$RELATION_FIELDS rf
                JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE
                WHERE rf.RDB$RELATION_NAME = @table
                ORDER BY rf.RDB$FIELD_POSITION;
            ";

            using var cmd = new FbCommand(sql, conn);
            cmd.Parameters.AddWithValue("@table", table);

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read()) {
                string colName = rdr.GetString(rdr.GetOrdinal("COL_NAME"));
                string fieldSource = rdr.GetString(rdr.GetOrdinal("FIELD_SOURCE")); // <-- domena lub pole systemowe
                bool nn = !rdr.IsDBNull(rdr.GetOrdinal("RDB$NULL_FLAG"));

                bool isDomain = rdr.GetInt16(rdr.GetOrdinal("SYS_FLAG")) == 0;

                string sqlType;

                if (isDomain) {
                    sqlType = fieldSource;
                } else {
                    short fieldType = rdr.GetInt16(rdr.GetOrdinal("RDB$FIELD_TYPE"));
                    short subType = rdr.IsDBNull(rdr.GetOrdinal("RDB$FIELD_SUB_TYPE"))
                        ? (short)0 : rdr.GetInt16(rdr.GetOrdinal("RDB$FIELD_SUB_TYPE"));
                    int length = rdr.IsDBNull(rdr.GetOrdinal("RDB$FIELD_LENGTH"))
                        ? 0 : rdr.GetInt32(rdr.GetOrdinal("RDB$FIELD_LENGTH"));
                    int charLen = rdr.IsDBNull(rdr.GetOrdinal("RDB$CHARACTER_LENGTH"))
                        ? 0 : rdr.GetInt32(rdr.GetOrdinal("RDB$CHARACTER_LENGTH"));
                    short precision = rdr.IsDBNull(rdr.GetOrdinal("RDB$FIELD_PRECISION"))
                        ? (short)0 : rdr.GetInt16(rdr.GetOrdinal("RDB$FIELD_PRECISION"));
                    short scale = rdr.IsDBNull(rdr.GetOrdinal("RDB$FIELD_SCALE"))
                        ? (short)0 : rdr.GetInt16(rdr.GetOrdinal("RDB$FIELD_SCALE"));

                    sqlType = MapType(fieldType, subType, length, charLen, precision, scale);
                }

                cols.Add($"{colName} {sqlType}{(nn ? " NOT NULL" : "")}");
            }

            return cols;
        }

        public static List<string> ExportProcedures(FbConnection conn) {
            var list = new List<string>();
            list.Add("-- PROCEDURES");

            const string procSql = @"
                SELECT
                    TRIM(RDB$PROCEDURE_NAME) AS NAME,
                    RDB$PROCEDURE_SOURCE AS SRC
                FROM RDB$PROCEDURES
                WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0
                ORDER BY RDB$PROCEDURE_NAME;
            ";

            using var cmd = new FbCommand(procSql, conn);
            using var rdr = cmd.ExecuteReader();

            while (rdr.Read()) {
                string name = rdr.GetString(rdr.GetOrdinal("NAME"));
                string? src = rdr.IsDBNull(rdr.GetOrdinal("SRC"))
                    ? null
                    : rdr.GetString(rdr.GetOrdinal("SRC"));

                if (string.IsNullOrWhiteSpace(src)) {
                    list.Add("");
                    list.Add($"-- Procedure: {name}");
                    list.Add("-- brak źródła procedury");
                    continue;
                }

                // ---- pobierz parametry procedury ----
                var inputs = new List<string>();
                var outputs = new List<string>();

                const string paramSql = @"
                    SELECT
                        TRIM(pp.RDB$PARAMETER_NAME)   AS PARAM_NAME,
                        pp.RDB$PARAMETER_TYPE         AS PARAM_TYPE, -- 0 = IN, 1 = OUT
                        pp.RDB$PARAMETER_NUMBER       AS PARAM_NO,
                        TRIM(pp.RDB$FIELD_SOURCE)     AS FIELD_SOURCE,
                        f.RDB$FIELD_TYPE,
                        f.RDB$FIELD_SUB_TYPE,
                        f.RDB$FIELD_LENGTH,
                        f.RDB$CHARACTER_LENGTH,
                        f.RDB$FIELD_PRECISION,
                        f.RDB$FIELD_SCALE,
                        COALESCE(f.RDB$SYSTEM_FLAG, 0) AS SYS_FLAG
                    FROM RDB$PROCEDURE_PARAMETERS pp
                    JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = pp.RDB$FIELD_SOURCE
                    WHERE pp.RDB$PROCEDURE_NAME = @procName
                    ORDER BY pp.RDB$PARAMETER_TYPE, pp.RDB$PARAMETER_NUMBER;
                ";

                using (var pCmd = new FbCommand(paramSql, conn)) {
                    pCmd.Parameters.AddWithValue("@procName", name);

                    using var pRdr = pCmd.ExecuteReader();
                    while (pRdr.Read()) {
                        string pName = pRdr.GetString(pRdr.GetOrdinal("PARAM_NAME"));
                        short pType = pRdr.GetInt16(pRdr.GetOrdinal("PARAM_TYPE"));
                        string fieldSource = pRdr.GetString(pRdr.GetOrdinal("FIELD_SOURCE"));
                        short sysFlag = pRdr.GetInt16(pRdr.GetOrdinal("SYS_FLAG"));

                        string typeSql;

                        if (sysFlag == 0) {
                            // użyj nazwy domeny (FIELD_SOURCE = nazwa domeny)
                            typeSql = fieldSource;
                        } else {
                            // policz typ bazowy
                            short fType = pRdr.GetInt16(pRdr.GetOrdinal("RDB$FIELD_TYPE"));
                            short fSub = pRdr.IsDBNull(pRdr.GetOrdinal("RDB$FIELD_SUB_TYPE"))
                                ? (short)0 : pRdr.GetInt16(pRdr.GetOrdinal("RDB$FIELD_SUB_TYPE"));
                            int fLen = pRdr.IsDBNull(pRdr.GetOrdinal("RDB$FIELD_LENGTH"))
                                ? 0 : pRdr.GetInt32(pRdr.GetOrdinal("RDB$FIELD_LENGTH"));
                            int fChar = pRdr.IsDBNull(pRdr.GetOrdinal("RDB$CHARACTER_LENGTH"))
                                ? 0 : pRdr.GetInt32(pRdr.GetOrdinal("RDB$CHARACTER_LENGTH"));
                            short prec = pRdr.IsDBNull(pRdr.GetOrdinal("RDB$FIELD_PRECISION"))
                                ? (short)0 : pRdr.GetInt16(pRdr.GetOrdinal("RDB$FIELD_PRECISION"));
                            short scale = pRdr.IsDBNull(pRdr.GetOrdinal("RDB$FIELD_SCALE"))
                                ? (short)0 : pRdr.GetInt16(pRdr.GetOrdinal("RDB$FIELD_SCALE"));

                            typeSql = MapType(fType, fSub, fLen, fChar, prec, scale);
                        }

                        string def = $"{pName} {typeSql}";

                        if (pType == 0)
                            inputs.Add(def);   // IN
                        else
                            outputs.Add(def);  // OUT (RETURNS)
                    }
                }

                // ---- składamy nagłówek CREATE OR ALTER PROCEDURE ----
                list.Add("");
                list.Add($"-- Procedure: {name}");

                var sb = new StringBuilder();

                sb.Append($"CREATE OR ALTER PROCEDURE {name}");

                if (inputs.Count > 0) {
                    sb.AppendLine(" (");
                    for (int i = 0; i < inputs.Count; i++) {
                        string comma = i < inputs.Count - 1 ? "," : "";
                        sb.Append("    ").Append(inputs[i]).AppendLine(comma);
                    }
                    sb.Append(")");
                } else {
                    sb.AppendLine();
                }

                if (outputs.Count > 0) {
                    sb.AppendLine("RETURNS (");
                    for (int i = 0; i < outputs.Count; i++) {
                        string comma = i < outputs.Count - 1 ? "," : "";
                        sb.Append("    ").Append(outputs[i]).AppendLine(comma);
                    }
                    sb.Append(")");
                }

                sb.AppendLine("AS");

                list.Add(sb.ToString().TrimEnd());
                list.Add(src.Trim());  
            }

            return list;
        }

        public static string MapType(short fieldType, short subType, int length, int charLen, short precision, short scale) {
            int realLen = charLen > 0 ? charLen : length;
            int s = Math.Abs(scale);

            return fieldType switch {
                7 => "SMALLINT",
                8 => "INTEGER",
                10 => "FLOAT",
                12 => "DATE",
                13 => "TIME",
                14 => $"CHAR({realLen})",
                16 => subType switch {
                    1 => $"NUMERIC({precision}, {s})",
                    2 => $"DECIMAL({precision}, {s})",
                    _ => "BIGINT"
                },
                27 => "DOUBLE PRECISION",
                35 => "TIMESTAMP",
                37 => $"VARCHAR({realLen})",
                _ => "BLOB"
            };
        }
    }
}
