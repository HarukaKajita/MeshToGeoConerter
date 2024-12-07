using System.Collections.Generic;
using MeshToGeoConverter;
using UnityEngine;

public class Tester : MonoBehaviour
{
    [SerializeField] private MeshRenderer meshRenderer;
	[SerializeField] private bool outputNaively = false;
    [ContextMenu("Convert Mesh To Geometry")]
    void SaveAsGeometryFile()
    {
        var converter = new MeshToGeoConverter.MeshToGeoConverter(meshRenderer, outputNaively, 0.001f);
        var dataPath = Application.dataPath;
        var path = $"{dataPath}/{meshRenderer.name}.geo";
        converter.SaveAsGeo(path);
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif 
    }
}
