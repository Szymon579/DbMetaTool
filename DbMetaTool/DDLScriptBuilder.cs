using System.Text;
using DbMetaTool.Model;

namespace DbMetaTool;

public class DDLScriptBuilder
{
    /// <summary>
    /// Creates the DDL script for domains.
    /// </summary>
    /// <param name="domains">Domains metadata.</param>
    /// <param name="sb">Script string to which the generated script is appended.</param>
    public static void BuildDomains(List<DomainInfo> domains, StringBuilder sb)
    {
        sb.AppendLine("/* --- 1. DOMAINS --- */");
        foreach (var d in domains)
        {
            sb.Append($"CREATE DOMAIN {d.Name} AS {d.TypeDefinition}");
            
            if (!string.IsNullOrEmpty(d.DefaultExpression))
            {
                // RDB$DEFAULT_SOURCE should already contain the DEFAULT prefix, but just in case we will verify that
                if (!d.DefaultExpression.StartsWith("DEFAULT", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(" DEFAULT");
                }
                
                sb.Append($" {d.DefaultExpression}");
            }
            
            if (d.IsNotNull)
            {
                sb.Append(" NOT NULL");
            }
            
            if (!string.IsNullOrEmpty(d.CheckConstraint))
            {
                // RDB$VALIDATION_SOURCE is usually contains only the condition like "(VALUE > 0)", without the CHECK keyword
                if (!d.CheckConstraint.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
                    sb.Append(" CHECK");
                
                sb.Append($" {d.CheckConstraint}");
            }

            sb.AppendLine(";");
        }
    }
    
    /// <summary>
    /// Creates the DDL script for tables.
    /// </summary>
    /// <param name="tables">Tables metadata.</param>
    /// <param name="sb">Script string to which the generated script is appended.</param>
    public static void BuildTables(List<TableInfo> tables, StringBuilder sb)
    {
        sb.AppendLine("\n/* --- 2. TABLES --- */");
        foreach (var t in tables)
        {
            sb.AppendLine($"CREATE TABLE {t.Name} (");
    
            var colDefinitions = new List<string>();
            foreach (var c in t.Columns)
            {
                var line = new StringBuilder($"\t{c.Name} {c.TypeDefinition}");
                
                if (!string.IsNullOrEmpty(c.DefaultExpression))
                {
                    if (!c.DefaultExpression.StartsWith("DEFAULT", StringComparison.OrdinalIgnoreCase))
                        line.Append(" DEFAULT");
            
                    line.Append($" {c.DefaultExpression}");
                }
                
                if (!c.IsNullable)
                {
                    line.Append(" NOT NULL");
                }

                colDefinitions.Add(line.ToString());
            }

            sb.AppendLine(string.Join(",\n", colDefinitions));
            sb.AppendLine(");");
        }
    }

    /// <summary>
    /// Creates the DDL script for procedures.
    /// </summary>
    /// <param name="procedures">Procedures metadata.</param>
    /// <param name="sb">Script string to which the generated script is appended.</param>
    public static void BuildProcedures(List<ProcedureInfo> procedures, StringBuilder sb)
    {
        sb.AppendLine("\n/* --- 3. PROCEDURES --- */");
        foreach (var proc in procedures)
        {
            // ensure terminator is set at the begging of procedure
            sb.AppendLine("SET TERM ^ ;");
            sb.Append($"CREATE PROCEDURE {proc.Name}");
            
            if (proc.InputParameters.Count != 0)
            {
                sb.AppendLine(" (");
                var paramsDef = proc.InputParameters.Select(ip => $"\t{ip.Name} {ip.TypeDefinition}");
                sb.AppendLine(string.Join(",\n", paramsDef));
                sb.Append(")");
            }
            
            if (proc.OutputParameters.Count != 0)
            {
                sb.AppendLine("\nRETURNS (");
                var returnsDef = proc.OutputParameters.Select(op => $"\t{op.Name} {op.TypeDefinition}");
                sb.AppendLine(string.Join(",\n", returnsDef));
                sb.Append(")");
            }

            sb.AppendLine("\nAS");
            sb.AppendLine(proc.Source.TrimEnd());
            
            // add procedure terminator if it's not already present
            if (!proc.Source.TrimEnd().EndsWith("^"))
            {
                sb.Append("^");
            }
        
            sb.AppendLine("\nSET TERM ; ^");
            sb.AppendLine();
        }
    }
}