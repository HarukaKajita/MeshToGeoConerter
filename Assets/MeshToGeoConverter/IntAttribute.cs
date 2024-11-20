using System.Text;

namespace MeshToGeoConverter
{
	public class IntAttribute : Attribute
	{
		// elementId, componentId
		private int[,] _values;
		public IntAttribute(string name, int[,] values)
		{
			this.name = name;
			storage = Storage.int32;
			isNumeric = true;
			_values = values;
			size = values.GetLength(1);
		}
		
		public void SetValues(int[,] values)
		{
			_values = values;
			size = values.GetLength(0);
		}

		protected override void BuildValuePart(StringBuilder sb)
		{
			sb.AppendLine("[");
				sb.AppendLine($"\"size\",{size},");
				sb.AppendLine($"\"storage\",\"{storage.ToString()}\",");
				sb.AppendLine("\"values\",[");
					sb.AppendLine($"\"size\",{size},");
					sb.AppendLine($"\"storage\",\"{storage.ToString()}\",");
					if (size == 1)
					{
						sb.Append("\"arrays\",[[");
						var elementCount = _values.GetLength(0);
						for (var i = 0; i < elementCount; i++)
						{
							sb.Append(_values[i, 0]);
							var isLast = i == elementCount - 1;
							if (!isLast) sb.Append(",");
						}
						sb.AppendLine("]]");
					}
					else
					{
						sb.Append("\"tuples\",[");
						var elementCount = _values.GetLength(0);
						for (var i = 0; i < elementCount; i++)
						{
							sb.Append("[");
							for (var j = 0; j < size; j++)
							{
								sb.Append(_values[i, j]);
								var isLastComponent = j == size - 1;
								if (!isLastComponent) sb.Append(",");
							}
							var isLastElement = i == elementCount - 1;
							sb.Append("]");
							if (!isLastElement) sb.Append(",");
						}
						sb.AppendLine("]");
					}
				sb.AppendLine("]");
			sb.AppendLine("],");
		}
	}
}