using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MeshToGeoConverter
{
    public class MeshToGeoConverter
    {
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private FloatAttribute _pointAttribute; 
        private FloatAttribute _normalAttribute; 
        public List<Attribute> PointAttributes = new();
        public List<Attribute> PrimitiveAttributes = new();

        public MeshToGeoConverter(MeshRenderer meshRenderer)
        {
            if (meshRenderer == null)
                throw new System.ArgumentNullException(nameof(meshRenderer));
            
            // point attributes
            _meshRenderer = meshRenderer;
            var filter = _meshRenderer.GetComponent<MeshFilter>();
            _mesh = filter.sharedMesh;
            var verts = _mesh.vertices;
            var norms = _mesh.normals;
            var positions = new float[verts.Length, 3];
            var normals = new float[verts.Length, 3];
            for (var i = 0; i < verts.Length; i++)
            {
                positions[i, 0] = -verts[i].x;
                positions[i, 1] = verts[i].y;
                positions[i, 2] = verts[i].z;
                
                normals[i, 0] = -norms[i].x;
                normals[i, 1] = norms[i].y;
                normals[i, 2] = norms[i].z;
            }
            _pointAttribute = new FloatAttribute("P", positions);
            _normalAttribute = new FloatAttribute("N", normals);
            PointAttributes.Add(_normalAttribute);
            
            // primitive attributes
            var subMeshTriangleCount = new int[_mesh.subMeshCount];
            var subMeshCount = _mesh.subMeshCount;
            for (var i = 0; i < subMeshCount; i++)
            {
                var submesh = _mesh.GetSubMesh(i);
                subMeshTriangleCount[i] = submesh.indexCount / 3;
            }
        
            var triangleCount = _mesh.triangles.Length / 3;
            var materials = new List<string>(triangleCount);
            var meshNames = new List<string>(triangleCount);
            for (var i = 0; i < subMeshCount; i++)
            {
                var material = meshRenderer.sharedMaterials[i].name;
                var meshName = _mesh.name;
                var triCount = subMeshTriangleCount[i];
                for (var j = 0; j < triCount; j++)
                {
                    materials.Add(material);
                    meshNames.Add(meshName);
                }
            }
            var materialAttribute = new StringAttribute("materialName");
            var meshNameAttribute = new StringAttribute("meshName");
            materialAttribute.SetValues(materials.ToArray());
            meshNameAttribute.SetValues(meshNames.ToArray());
            PrimitiveAttributes.Add(materialAttribute);
            PrimitiveAttributes.Add(meshNameAttribute);
        }

        public void SaveAsGeo(string path)
        {
            if (_meshRenderer == null)
            {
                Debug.LogError("Mesh is null");
                return;
            }
            if (_mesh == null)
            {
                Debug.LogError("Mesh is null");
                return;
            }

            var json = MakeJson();
            File.WriteAllText(path, json);
        }

        private string MakeJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[");
            MakeContent(sb);
            sb.AppendLine("]");
            return sb.ToString();
        }

        private void MakeContent(StringBuilder sb)
        {
            sb.AppendLine($"\"pointcount\",{_mesh.vertexCount},");
            sb.AppendLine($"\"vertexcount\",{_mesh.triangles.Length},");
            sb.AppendLine($"\"primitivecount\",{_mesh.triangles.Length/3},");
            MakeTopologyContent(sb);
            MakeAttributesContent(sb);
            MakePrimitiveContent(sb);
        }

        private void MakeTopologyContent(StringBuilder sb)
        {
            var indices = _mesh.triangles;
            sb.AppendLine("\"topology\",[");
            sb.AppendLine("\"pointref\",[");
            sb.AppendLine($"\"indices\",[{string.Join(",", indices)}]");
            sb.AppendLine("]");
            sb.AppendLine("],");
        }

        private void MakeAttributesContent(StringBuilder sb)
        {
            sb.AppendLine("\"attributes\",[");
            
                sb.AppendLine("\"pointattributes\",[");
                MakePointAttributeContent(sb);
                sb.AppendLine("],");

            if (0 < PrimitiveAttributes.Count)
            {
                sb.AppendLine("\"primitiveattributes\",[");
                MakePrimitiveAttributeContent(sb);
                sb.AppendLine("],");
            }

            
            sb.AppendLine("],");
        }

        private void MakePointAttributeContent(StringBuilder sb)
        {
            sb.AppendLine(_pointAttribute.MakeJson());
            foreach (var attribute in PointAttributes)
                sb.AppendLine(attribute.MakeJson());
        }

        private void MakePrimitiveAttributeContent(StringBuilder sb)
        {
            foreach (var attribute in PrimitiveAttributes)
                sb.AppendLine(attribute.MakeJson());
        }


        private void MakePrimitiveContent(StringBuilder sb)
        {
            var trianglePointCount = 3;
            var triangleCount = _mesh.triangles.Length / trianglePointCount;
            sb.AppendLine("\"primitives\",[");
            sb.AppendLine("[");
            sb.AppendLine("[");
            sb.AppendLine("\"type\",\"Polygon_run\"");
            sb.AppendLine("],");
            sb.AppendLine("[");
            sb.AppendLine("\"startvertex\",0,");
            sb.AppendLine($"\"nprimitives\",{triangleCount},"); //12
            sb.AppendLine($"\"nvertices_rle\",[{trianglePointCount},{triangleCount}]"); //3,12
            sb.AppendLine("]");
            sb.AppendLine("]");
            sb.AppendLine("]");
        }
    }
}