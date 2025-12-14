namespace DbMetaTool.Model;

public record DomainInfo
{
    public string Name { get; set; } = "";
    public string TypeDefinition { get; set; } = "";
    public bool IsNotNull { get; set; }
    public string? DefaultExpression { get; set; }
    public string? CheckConstraint { get; set; }
}