using System;
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
        public List<Attribute> PointAttributes = new();
        public List<Attribute> VertexAttributes = new();
        public List<Attribute> PrimitiveAttributes = new();
        private int[] indices;
        private int pointCount = 0;
        bool outputNaively = false;
        public MeshToGeoConverter(MeshRenderer meshRenderer, bool outputNaively = false, float weldThreshold = float.Epsilon)
        {
            this.outputNaively = outputNaively;
            if (meshRenderer == null)
                throw new ArgumentNullException(nameof(meshRenderer));
            
            // point attributes
            _meshRenderer = meshRenderer;
            var filter = _meshRenderer.GetComponent<MeshFilter>();
            _mesh = filter.sharedMesh;
            var verts = _mesh.vertices;
            var norms = _mesh.normals;
            var positions = new float[,]{};
            var normals = outputNaively ? new float[verts.Length, 3] : new float[_mesh.triangles.Length, 3];
            indices = _mesh.triangles;

            var distinctPositions = new List<Vector3>();
            var remappedIndices = new int[verts.Length];
            if (!outputNaively)
            {
                // 頂点座標の重複を削除
                for (var i = 0; i < verts.Length; i++)
                {
                    var alreadyExists = false;
                    for (var index = 0; index < distinctPositions.Count; index++)
                    {
                        var p = distinctPositions[index];
                        if (weldThreshold < Math.Abs(p.x - verts[i].x)) continue;
                        if (weldThreshold < Math.Abs(p.y - verts[i].y)) continue;
                        if (weldThreshold < Math.Abs(p.z - verts[i].z)) continue;
                        // ほぼ同値の場合
                        remappedIndices[i] = index;
                        alreadyExists = true;
                        break;
                    }
                    if (alreadyExists) continue;

                    remappedIndices[i] = distinctPositions.Count;
                    distinctPositions.Add(verts[i]);
                }
                // distinctPositionsをpositionsにコピー
                pointCount = distinctPositions.Count;
                positions = new float[pointCount, 3];
                for (var i = 0; i < distinctPositions.Count; i++)
                {
                    positions[i, 0] = -distinctPositions[i].x;
                    positions[i, 1] = distinctPositions[i].y;
                    positions[i, 2] = distinctPositions[i].z;
                }
                // 頂点法線のコピー
                for (var i = 0; i < indices.Length; i++)
                {   
                    normals[i, 0] = -norms[indices[i]].x;
                    normals[i, 1] = norms[indices[i]].y;
                    normals[i, 2] = norms[indices[i]].z;
                }
                // remap indices
                for (var i = 0; i < indices.Length; i++)
                    indices[i] = remappedIndices[indices[i]];
            }
            else
            {
                pointCount = verts.Length;
                positions = new float[pointCount, 3];
                // 頂点座標の重複を削除せずにそのままコピー
                for (var i = 0; i < pointCount; i++)
                {
                    positions[i, 0] = -verts[i].x;
                    positions[i, 1] = verts[i].y;
                    positions[i, 2] = verts[i].z;
                }
                // 頂点法線のコピー
                for (var i = 0; i < pointCount; i++)
                {   
                    normals[i, 0] = -norms[i].x;
                    normals[i, 1] = norms[i].y;
                    normals[i, 2] = norms[i].z;
                }
            }
            
            // 頂点座標アトリビュート
            PointAttributes.Add(new FloatAttribute("P", positions));
            
            // 頂点法線アトリビュート
            if(outputNaively) PointAttributes.Add(new FloatAttribute("N", normals));
            else  VertexAttributes.Add(new FloatAttribute("N", normals));
            
            // primitive attributes
            var subMeshTriangleCount = new int[_mesh.subMeshCount];
            var subMeshCount = _mesh.subMeshCount;
            for (var i = 0; i < subMeshCount; i++)
                subMeshTriangleCount[i] = _mesh.GetSubMesh(i).indexCount / 3;
        
            var triangleCount = _mesh.triangles.Length / 3;
            var materials = new List<string>(triangleCount);
            var meshNames = new List<string>(triangleCount);
            var subMeshId = new int[triangleCount,1];
            for (var i = 0; i < subMeshCount; i++)
            {
                var material = meshRenderer.sharedMaterials[i].name;
                var meshName = _mesh.name;
                var triCount = subMeshTriangleCount[i];
                for (var j = 0; j < triCount; j++)
                {
                    materials.Add(material);
                    meshNames.Add(meshName);
                    subMeshId[j,0] = i;
                }
            }
            var materialAttribute = new StringAttribute("shop_materialpath");
            var meshNameAttribute = new StringAttribute("meshName");
            var subMeshIdAttribute = new IntAttribute("subMeshId", subMeshId);
            materialAttribute.SetValues(materials.ToArray());
            meshNameAttribute.SetValues(meshNames.ToArray());
            PrimitiveAttributes.Add(materialAttribute);
            PrimitiveAttributes.Add(meshNameAttribute);
            PrimitiveAttributes.Add(subMeshIdAttribute);
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
            sb.AppendLine($"\"pointcount\",{pointCount},");
            sb.AppendLine($"\"vertexcount\",{_mesh.triangles.Length},");
            sb.AppendLine($"\"primitivecount\",{_mesh.triangles.Length/3},");
            MakeTopologyContent(sb);
            MakeAttributesContent(sb);
            MakePrimitiveContent(sb);
        }

        private void MakeTopologyContent(StringBuilder sb)
        {
            sb.AppendLine("\"topology\",[");
            sb.AppendLine("\"pointref\",[");
            sb.AppendLine($"\"indices\",[{string.Join(",", indices)}]");
            sb.AppendLine("]");
            sb.AppendLine("],");
        }

        private void MakeAttributesContent(StringBuilder sb)
        {
            sb.AppendLine("\"attributes\",[");
            
                if(0 < VertexAttributes.Count)
                {
                    sb.AppendLine("\"vertexattributes\",[");
                    MakeAttributeContent(sb, VertexAttributes);
                    sb.AppendLine("],");
                }
                
                sb.AppendLine("\"pointattributes\",[");
                MakeAttributeContent(sb, PointAttributes);
                sb.AppendLine("],");
                
                if (0 < PrimitiveAttributes.Count)
                {
                    sb.AppendLine("\"primitiveattributes\",[");
                    MakeAttributeContent(sb, PrimitiveAttributes);
                    sb.AppendLine("],");
                }
            
            sb.AppendLine("],");
        }

        private void MakeAttributeContent(StringBuilder sb, IEnumerable<Attribute> attributes)
        {
            foreach (var attribute in attributes)
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