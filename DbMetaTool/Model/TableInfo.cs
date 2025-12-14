namespace DbMetaTool.Model;

public record TableInfo 
{ 
    public string Name { get; set; } = ""; 
    public List<ColumnInfo> Columns { get; set; } = []; 
}