using System.Text;

public class Attribute
{
    protected string name;
    protected bool isNumeric = true;
    protected int size = 1;
    protected Storage storage = Storage.fpreal32;
    public string MakeJson()
    {
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[");
            BuildDescriptionPart(sb);
            BuildValuePart(sb);
        sb.AppendLine("],");
        return sb.ToString();
    }

    private void BuildDescriptionPart(StringBuilder sb)
    {
        var type = isNumeric ? "numeric" : "string";
        sb.AppendLine("[");
            sb.AppendLine("\"scope\",\"public\",");
            sb.AppendLine($"\"type\",\"{type}\",");
            sb.AppendLine($"\"name\",\"{name}\",");
        sb.AppendLine("],");
    }
    
    protected virtual void BuildValuePart(StringBuilder sb)
    {
        sb.AppendLine("[");
            sb.AppendLine($"\"size\",{size},");
            sb.AppendLine($"\"storage\",\"{storage.ToString()}\",");
            sb.AppendLine("\"values\",[");
                sb.AppendLine($"\"size\",{size},");
                sb.AppendLine($"\"storage\",\"{storage.ToString()}\",");
            sb.AppendLine("]");
        sb.AppendLine("],");
    }
}
