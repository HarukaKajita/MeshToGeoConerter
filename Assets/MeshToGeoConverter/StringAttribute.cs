using System;
using System.Linq;
using System.Text;

namespace MeshToGeoConverter
{
	public class StringAttribute : Attribute
	{
		//set of strings
		private string[] _strings;
		// elementId
		private int[] _indices;
		public StringAttribute(string name)
		{
			this.name = name;
			storage = Storage.int32;
			isNumeric = false;
		}
		
		public void SetValues(string[] values)
		{
			_strings = values.Distinct().ToArray();
			_indices = values.Select(v => Array.IndexOf(_strings, v)).ToArray();
			size = 1;
		}
		protected override void BuildValuePart(StringBuilder sb)
		{
			var str = string.Join(",", _strings.Select(v => $"\"{v}\""));
			var indexStr = string.Join(",", _indices.Select(v => v.ToString()).ToArray());
			sb.Append("[");
				sb.AppendLine($"\"size\",{size},");
				sb.AppendLine($"\"storage\",\"{storage.ToString()}\",");
				sb.AppendLine($"\"strings\",[");
					sb.AppendLine(str);
				sb.AppendLine($"],");
				sb.AppendLine("\"indices\",[");
					sb.AppendLine($"\"size\",{size},");
					sb.AppendLine($"\"storage\",\"{storage.ToString()}\",");
					sb.Append($"\"arrays\",[[");
						sb.Append(indexStr);
					sb.AppendLine($"]]");
				sb.AppendLine("]");
			sb.AppendLine("],");
		}
	}
}