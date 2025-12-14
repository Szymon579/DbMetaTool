namespace DbMetaTool.Model;

public record ColumnInfo 
{ 
    public string Name { get; set; } = ""; 
    public string TypeDefinition { get; set; } = "";
    public bool IsNullable { get; set; }
    public string? DefaultExpression { get; set; }
}