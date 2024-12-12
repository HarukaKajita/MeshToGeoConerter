using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MeshToGeoConverter
{
    public class MeshToGeoConverter
    {
        public List<Attribute> PointAttributes = new();
        public List<Attribute> VertexAttributes = new();
        public List<Attribute> PrimitiveAttributes = new();
        public List<Attribute> DetailAttributes = new();
        
        private MeshRenderer _meshRenderer;
        private Mesh _mesh; 
        private int[] indices;
        private int pointCount = 0;
        bool outputNaively = false;
        public MeshToGeoConverter(MeshRenderer meshRenderer, bool exportInWorldSpace, bool outputNaively = true, float weldThreshold = float.Epsilon)
        {
            this.outputNaively = outputNaively;
            if (meshRenderer == null)
                throw new ArgumentNullException(nameof(meshRenderer));
            
            // point attributes
            _meshRenderer = meshRenderer;
            _mesh = _meshRenderer?.GetComponent<MeshFilter>()?.sharedMesh;
            
            if(_mesh == null) throw new ArgumentException($"{meshRenderer.name}:Mesh is null");
            if(_mesh.vertices == null || _mesh.vertices.Length == 0) throw new ArgumentException($"{meshRenderer.name} {_mesh.name}: Mesh has no vertices");
            
            var verts = _mesh.vertices;
            var norms = _mesh.normals;
            var basMeshUV = _mesh.uv;
            float[,] positions;
            float[,] normals;
            float[,] uv0S;
            indices = _mesh.triangles;
            
            var distinctPositions = new List<Vector3>();
            var remappedIndices = new int[verts.Length];
            if (!outputNaively)
            {
                // ComputeShaderで高速に重複頂点の判定をする改善が可能
                
                // 頂点座標の重複を削除
                for (var i = 0; i < verts.Length; i++)
                {
                    var alreadyExists = false;
                    for (var index = 0; index < distinctPositions.Count; index++)
                    {
                        var p = distinctPositions[index];
                        if (weldThreshold < Vector3.Distance(p, verts[i])) continue;
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
                    var v = verts[i];
                    if (exportInWorldSpace) v = GetWorldPosition(v);
                    positions[i, 0] = -v.x;
                    positions[i, 1] = v.y;
                    positions[i, 2] = v.z;
                }
                // 頂点法線のコピー
                var vertexCount = _mesh.vertexCount;
                normals = new float[vertexCount, 3];
                for (var i = 0; i < vertexCount; i++)
                {   
                    var dir = norms[indices[i]];
                    if (exportInWorldSpace) dir = GetWorldDirection(dir);
                    normals[i, 0] = -dir.x;
                    normals[i, 1] = dir.y;
                    normals[i, 2] = dir.z;
                }
                uv0S = new float[vertexCount, 3];
                for (var i = 0; i < vertexCount; i++)
                {
                    var uv = basMeshUV[indices[i]];
                    uv0S[i, 0] = uv.x;
                    uv0S[i, 1] = uv.y;
                    uv0S[i, 2] = 0;
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
                    var v = verts[i];
                    if (exportInWorldSpace) v = GetWorldPosition(v);
                    positions[i, 0] = -v.x;
                    positions[i, 1] = v.y;
                    positions[i, 2] = v.z;
                }
                // 頂点法線のコピー
                normals = new float[pointCount, 3];
                for (var i = 0; i < pointCount; i++)
                {
                    var dir = norms[i];
                    if (exportInWorldSpace) dir = GetWorldDirection(dir);
                    normals[i, 0] = -dir.x;
                    normals[i, 1] = dir.y;
                    normals[i, 2] = dir.z;
                }
                uv0S = new float[pointCount, 3];
                for (var i = 0; i < pointCount; i++)
                {
                    var uv = basMeshUV[i];
                    uv0S[i, 0] = uv.x;
                    uv0S[i, 1] = uv.y;
                    uv0S[i, 2] = 0;
                }
            }
            
            // 頂点座標アトリビュート
            PointAttributes.Add(new FloatAttribute("P", positions));
            
            // 頂点法線アトリビュート
            if(outputNaively) PointAttributes.Add(new FloatAttribute("N", normals));
            else  VertexAttributes.Add(new FloatAttribute("N", normals));
            
            if(outputNaively) PointAttributes.Add(new FloatAttribute("uv", uv0S));
            else  VertexAttributes.Add(new FloatAttribute("Nuv", uv0S));
            
            // primitive attributes
            var subMeshTriangleCount = new int[_mesh.subMeshCount];
            var subMeshCount = _mesh.subMeshCount;
            for (var i = 0; i < subMeshCount; i++)
                subMeshTriangleCount[i] = _mesh.GetSubMesh(i).indexCount / 3;
        
            var triangleCount = _mesh.triangles.Length / 3;
            var materials = new List<string>(triangleCount);
            var meshNames = new List<string>(triangleCount);
            var castShadows = new List<string>(triangleCount);
            var mainTexturePaths = new List<string>(triangleCount);
            var normalMapPaths = new List<string>(triangleCount);
            var baseColors = new float[triangleCount,4];
            var metallics = new float[triangleCount,1];
            var smoothnesses = new float[triangleCount,1];
            var renderQueues = new int[triangleCount,1];
            var shadowCast = meshRenderer.shadowCastingMode.ToString();
            var tag = meshRenderer.tag;
            var layer = LayerMask.LayerToName(meshRenderer.gameObject.layer);
            
            var accumulatedTriangleCount = 0;
            for (var i = 0; i < subMeshCount; i++)
            {
                var material = meshRenderer.sharedMaterials[i];
                var meshName = _mesh.name;
                var materialName = material?.name;
                var triCount = subMeshTriangleCount[i];
                var baseMapPath = MaterialTextureAbsolutePath(material, new[] { "_BaseMap", "_MainTex" });
                var normalMapPath = MaterialTextureAbsolutePath(material, new[] {"_NormalMap", "_BumpMap"});
                var color = MaterialColor(material, new[] {"_BaseColor", "_Color"});
                var metallic = MaterialFloat(material, new[] { "_Metallic" });
                var smoothnes = MaterialFloat(material, new[] { "_Smoothness" });
                var renderQueue = MaterialQueue(material);
                if (renderQueue <= 0)
                    Debug.LogWarning($"{material.name} : {renderQueue}");
                for (var j = 0; j < triCount; j++)
                {
                    var triIndex = accumulatedTriangleCount + j;
                    materials.Add(materialName);
                    meshNames.Add(meshName);
                    castShadows.Add(shadowCast);
                    mainTexturePaths.Add(baseMapPath);
                    normalMapPaths.Add(normalMapPath);
                    baseColors[triIndex, 0] = color.r;
                    baseColors[triIndex, 1] = color.g;
                    baseColors[triIndex, 2] = color.b;
                    baseColors[triIndex, 3] = color.a;
                    metallics[triIndex, 0] = metallic;
                    smoothnesses[triIndex, 0] = smoothnes;
                    renderQueues[triIndex, 0] = renderQueue;
                }
                accumulatedTriangleCount += triCount;
            }
            var materialAttribute = new StringAttribute("shop_materialpath", materials.ToArray());
            var meshNameAttribute = new StringAttribute("meshName", meshNames.ToArray());
            var mainTexturePathAttribute = new StringAttribute("baseMap", mainTexturePaths.ToArray());
            var normalMapPathAttribute = new StringAttribute("normalMap", normalMapPaths.ToArray());
            var baseColorAttribute = new FloatAttribute("baseColor", baseColors);
            var metallicAttribute = new FloatAttribute("metallic", metallics);
            var smoothnessAttribute = new FloatAttribute("smoothness", smoothnesses);
            var queueAttribute = new IntAttribute("queue", renderQueues);
            PrimitiveAttributes.Add(materialAttribute);
            PrimitiveAttributes.Add(meshNameAttribute);
            PrimitiveAttributes.Add(mainTexturePathAttribute);
            PrimitiveAttributes.Add(normalMapPathAttribute);
            PrimitiveAttributes.Add(baseColorAttribute);
            PrimitiveAttributes.Add(metallicAttribute);
            PrimitiveAttributes.Add(smoothnessAttribute);
            PrimitiveAttributes.Add(queueAttribute);
            DetailAttributes.Add(new StringAttribute("shadowCast", new []{shadowCast}));
            DetailAttributes.Add(new StringAttribute("tag", new []{tag}));
            DetailAttributes.Add(new StringAttribute("layer", new []{layer}));
        }
        
        Vector3 GetWorldPosition(Vector3 localPosition)
        {
            return _meshRenderer.transform.TransformPoint(localPosition);
        }
        Vector3 GetWorldDirection(Vector3 localDirection)
        {
            return _meshRenderer.transform.TransformDirection(localDirection);
        }
        private int MaterialQueue(Material material)
        {
            if (material == null) return 2000;
            var renderQueue = material.renderQueue;
            if (renderQueue <= 0)
            {
                renderQueue = 2000;
                // if (material.HasProperty("_QueueOffset") && material.HasProperty("_Surface"))
                // {
                //     var queueOffset = material.GetFloat("_QueueOffset");
                //     var surfaceType = material.GetFloat("_Surface");
                // }
            }
            return renderQueue;
        }
        private float MaterialFloat(Material material, string[] propertyNames)
        {
            if (material == null) return 0;
            foreach (var propertyName in propertyNames)
            {
                if (!material.HasProperty(propertyName)) continue;
                return material.GetFloat(propertyName);
            }
            return 0;
        }
        Color MaterialColor(Material material, string[] propertyNames)
        {
            if (material == null) return Color.white;
            foreach (var propertyName in propertyNames)
            {
                if (!material.HasProperty(propertyName)) continue;
                return material.GetColor(propertyName);
            }
            return Color.white;
        }
        string MaterialTextureAbsolutePath(Material material, string[] propertyNames)
        {
            if (material == null) return "";
            foreach (var propertyName in propertyNames)
            {
                if (!material.HasProperty(propertyName)) continue;
                var texture = material.GetTexture(propertyName);
                return TextureAbsolutePath(texture);
            }
            return "";
        }
        string TextureAbsolutePath(Texture texture)
        {
            if (texture == null) return "";
            var path = AssetDatabase.GetAssetPath(texture);
            return AssetPathToAbsolutePath(path);
        }
        string AssetPathToAbsolutePath(string assetPath)
        {
            if(assetPath.Length < 7) return "";
            assetPath = assetPath[7..];
            return $"{Application.dataPath}/{assetPath}";
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
                
                if (0 < DetailAttributes.Count)
                {
                    sb.AppendLine("\"globalattributes\",[");
                    MakeAttributeContent(sb, DetailAttributes);
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