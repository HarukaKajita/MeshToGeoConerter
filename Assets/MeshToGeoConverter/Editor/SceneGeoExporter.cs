using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeshToGeoConverter
{
	// 開いているシーンないのMeshRendererをGeometryファイルに変換するエディタウィンドウ
	public class SceneGeoExporter : EditorWindow
	{
		private string[] _sceneNames;
		private MeshRenderer[][] _meshRenderersPerScene;
		private bool _exportInWorldSpace = true;
		private bool _includeInactive = false;
		// ウィンドウを開く
		[MenuItem("Window/SceneGeoExporter")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow(typeof(SceneGeoExporter));
		}

		// ウィンドウのGUI処理
		void OnGUI()
		{
			// ボタンを表示
			_includeInactive = GUILayout.Toggle(_includeInactive, "Include Inactive Renderers");
			if (GUILayout.Button("Collect Scene MeshRenderers")) Init();
			if (_sceneNames != null)
			{
				for (var i = 0; i < _sceneNames.Length; i++)
				{
					EditorGUILayout.LabelField(_sceneNames[i]);
					GUILayout.Label($"MeshRenderers: {_meshRenderersPerScene?[i]?.Length}");
				}
			}
			_exportInWorldSpace = GUILayout.Toggle(_exportInWorldSpace, "Export In World Space");
			
			// 書き出しボタン
			if (GUILayout.Button("Export .geo Files")) Export();
		}

		void Init()
		{
			var sceneCount = SceneManager.sceneCount;
			_sceneNames = new string[sceneCount];
			_meshRenderersPerScene = new MeshRenderer[sceneCount][];
			for (var i = 0; i < sceneCount; i++)
			{
				var scene = SceneManager.GetSceneAt(i);
				_sceneNames[i] = scene.name;
				scene.GetRootGameObjects();
				_meshRenderersPerScene[i] = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MeshRenderer>()).ToArray();
				if (!_includeInactive)
				{
					_meshRenderersPerScene[i] = _meshRenderersPerScene[i].Where(mr => mr.gameObject.activeInHierarchy && mr.enabled).ToArray();
				}
			}
		}

		void Export()
		{
			// シーンごとにループ
			var sceneCount = _sceneNames.Length;
			var dataPath = Application.dataPath;
			var startTime = DateTime.Now;
			for (var i = 0; i < sceneCount; i++)
			{
				var sceneName = _sceneNames[i];
				var meshRenderers = _meshRenderersPerScene[i];
				var exportDirectory = $"{dataPath}/{sceneName}";
				if (!Directory.Exists(exportDirectory))
					Directory.CreateDirectory(exportDirectory);
				
				// メッシュレンダラーの数だけループ
				foreach (var meshRenderer in meshRenderers)
				{
					// メッシュレンダラーをGeometryファイルに変換
					var converter = new MeshToGeoConverter(meshRenderer, _exportInWorldSpace);
					var path = $"{dataPath}/{sceneName}/{meshRenderer.name}_{meshRenderer.GetInstanceID()}.geo";
					converter.SaveAsGeo(path);
				}
			}
			// 古いファイルを削除
			for (var i = 0; i < sceneCount; i++)
			{
				var sceneName = _sceneNames[i];
				var exportDirectory = $"{dataPath}/{sceneName}";
				var files = Directory.GetFiles(exportDirectory);
				files.Where(f => File.GetLastWriteTime(f) < startTime).ToList().ForEach(File.Delete);
			}
			// Unityエディタのプロジェクトビューを更新
			AssetDatabase.Refresh();
		}
	}
}
