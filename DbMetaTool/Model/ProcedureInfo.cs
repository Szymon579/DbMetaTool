namespace DbMetaTool.Model;

public class ProcedureInfo
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public List<ParameterInfo> InputParameters { get; set; } = [];
    public List<ParameterInfo> OutputParameters { get; set; } = [];
}

public class ParameterInfo
{
    public string Name { get; set; } = "";
    public string TypeDefinition { get; set; } = "";
}