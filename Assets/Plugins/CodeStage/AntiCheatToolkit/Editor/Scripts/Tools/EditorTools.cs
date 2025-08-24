#region copyright
// ------------------------------------------------------
// Copyright (C) Dmitriy Yukhanov [https://codestage.net]
// ------------------------------------------------------
#endregion

using CodeStage.EditorCommon.Tools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CodeStage.AntiCheat.EditorCode
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using Common;
	using UnityEditor;
	using UnityEngine;
	using UnityEngine.Events;
	using Object = UnityEngine.Object;

	internal delegate bool ProcessSerializedProperty(Object target, SerializedProperty sp, AssetLocationData location, string type);
	
	internal static class EditorTools
	{
		private static SerializedObject audioManager;
		
		#region files and directories

		private static string directory;

		public static void DeleteFile(string path)
		{
			if (!File.Exists(path)) return;
			RemoveReadOnlyAttribute(path);
			File.Delete(path);
		}

		public static void RemoveDirectoryIfEmpty(string directoryName)
		{
			if (Directory.Exists(directoryName) && IsDirectoryEmpty(directoryName))
			{
				FileUtil.DeleteFileOrDirectory(directoryName);
				var metaFile = AssetDatabase.GetTextMetaFilePathFromAssetPath(directoryName);
				if (File.Exists(metaFile))
				{
					FileUtil.DeleteFileOrDirectory(metaFile);
				}
			}
		}

		public static bool IsDirectoryEmpty(string path)
		{
			var dirs = Directory.GetDirectories(path);
			var files = Directory.GetFiles(path);
			return dirs.Length == 0 && files.Length == 0;
		}

		public static string GetACTkDirectory()
		{
			if (!string.IsNullOrEmpty(directory))
			{
				return directory;
			}

			directory = ACTkMarker.GetAssetPath();

			if (!string.IsNullOrEmpty(directory))
			{
				if (directory.IndexOf("Editor/Scripts/ACTkMarker.cs", StringComparison.Ordinal) >= 0)
				{
					directory = directory.Replace("Editor/Scripts/ACTkMarker.cs", "");
				}
				else
				{
					directory = null;
					ACTk.PrintExceptionForSupport("Looks like Anti-Cheat Toolkit is placed in project incorrectly!");
				}
			}
			else
			{
				directory = null;
				ACTk.PrintExceptionForSupport("Can't locate the Anti-Cheat Toolkit directory!");
			}
			return directory;
		}
		
		#endregion

		public static bool CheckUnityEventHasActivePersistentListener(SerializedProperty unityEvent)
		{
			if (unityEvent == null) return false;

			var calls = unityEvent.FindPropertyRelative("m_PersistentCalls.m_Calls");
			if (calls == null)
			{
				ACTk.PrintExceptionForSupport("Can't find Unity Event calls!");
				return false;
			}
			if (!calls.isArray)
			{
				ACTk.PrintExceptionForSupport("Looks like Unity Event calls are not array anymore!");
				return false;
			}

			var result = false;

			var callsCount = calls.arraySize;
			for (var i = 0; i < callsCount; i++)
			{
				var call = calls.GetArrayElementAtIndex(i);

				var targetProperty = call.FindPropertyRelative("m_Target");
				var methodNameProperty = call.FindPropertyRelative("m_MethodName");
				var callStateProperty = call.FindPropertyRelative("m_CallState");

				if (targetProperty != null && methodNameProperty != null && callStateProperty != null &&
                    targetProperty.propertyType == SerializedPropertyType.ObjectReference &&
					methodNameProperty.propertyType == SerializedPropertyType.String &&
					callStateProperty.propertyType == SerializedPropertyType.Enum)
				{
					var target = targetProperty.objectReferenceValue;
					var methodName = methodNameProperty.stringValue;
					var callState = (UnityEventCallState)callStateProperty.enumValueIndex;

					if (target != null && !string.IsNullOrEmpty(methodName) && callState != UnityEventCallState.Off)
					{
						result = true;
						break;
					}
				}
				else
				{
					ACTk.PrintExceptionForSupport("Can't parse Unity Event call!");
				}
			}
			return result;
		}

		public static void RemoveReadOnlyAttribute(string path)
		{
			var attributes = File.GetAttributes(path);
			if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
		}

		public static string[] FindLibrariesAt(string folder, bool recursive = true)
		{
			folder = folder.Replace('\\', '/');

			if (!Directory.Exists(folder))
			{
				return Array.Empty<string>();
			}

			var allFiles = Directory.GetFiles(folder, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
			var result = new List<string>();

			foreach (var file in allFiles)
			{
				var extension = Path.GetExtension(file);
				if (string.IsNullOrEmpty(extension))
				{
					continue;
				}

				if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
				{
					var path = file.Replace('\\', '/');
					result.Add(path);
				}
			}

			return result.ToArray();
		}

		public static void OpenReadme()
		{
			var defaultReadmePath = Path.Combine(GetACTkDirectory(), "Readme.pdf");
			var loadedReadme = AssetDatabase.LoadMainAssetAtPath(defaultReadmePath);
			AssetDatabase.OpenAsset(loadedReadme);
		}
		
		public static Object GetPingableObject(Object target)
		{
			if (!AssetDatabase.Contains(target))
				return target;

			if (!(target is Component component))
				return target;

			target = component.gameObject;
			
			if (PrefabUtility.IsPartOfAnyPrefab(target))
			{
				var asset = PrefabUtility.GetCorrespondingObjectFromSource(target);
				if (asset != null)
					target = asset;
			}
			
			return target;
		}

		public static bool IsAudioManagerEnabled()
		{
			return !GetUpdatedAudioManagerDisableAudioProperty().boolValue;
		}

#if UNITY_2021_3_OR_NEWER
		public static string GetAudioManagerEnabledPropertyPath()
		{
			return GetUpdatedAudioManagerDisableAudioProperty().propertyPath;
		}
#endif
		
		private static SerializedProperty GetUpdatedAudioManagerDisableAudioProperty()
		{
			if (audioManager == null)
			{
				var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0];
				audioManager = new SerializedObject(asset);
			}

			audioManager.Update();
			return audioManager.FindProperty("m_DisableAudio");
		}

		#region Trversal
		
		public static void TraverseSerializedScriptsAssets(string[] assetPaths, ProcessSerializedProperty itemCallback, string[] typesFilter)
		{
			var touchedObjectsCount = 0;
			var scannedObjectsCont = 0;
			var scannedAssetsCont = 0;
			
			try
			{
				const string progressHeader = "ACTk: Looking through assets";
				var targets = new Dictionary<Object, AssetLocationData>();

				EditorUtility.DisplayProgressBar(progressHeader, "Collecting data...", 0);
				
				AssetDatabase.SaveAssets();
				AssetDatabase.StartAssetEditing();

				if (assetPaths == null || assetPaths.Length == 0)
				{
					var guids = AssetDatabase.FindAssets("t:ScriptableObject t:Prefab");
					assetPaths = new string[guids.Length];
					for (var i = 0; i < guids.Length; i++)
					{
						var guid = guids[i];
						assetPaths[i] = AssetDatabase.GUIDToAssetPath(guid);
					}
				}

				var count = assetPaths.Length;
				foreach (var assetPath in assetPaths)
				{
					if (EditorUtility.DisplayCancelableProgressBar(progressHeader,
							"Asset " + (scannedAssetsCont + 1) + " from " + count,
							scannedAssetsCont / (float)count))
					{
						Debug.Log(ACTk.LogPrefix + "operation canceled by user.");
						break;
					}
					
					if (!assetPath.StartsWith("assets", StringComparison.OrdinalIgnoreCase)) continue;
					
					var isPrefab = Path.GetExtension(assetPath) == ".prefab";
					targets.Clear();
					
					if (!isPrefab)
					{
						var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
						foreach (var unityObject in objects)
						{
							if (unityObject == null) continue;
							if (unityObject.name == "Deprecated EditorExtensionImpl") continue;
							if (targets.ContainsKey(unityObject)) continue;
							targets.Add(unityObject, AssetTools.GetLocation(assetPath, unityObject));
						}
					}
					else
					{
						var root = AssetDatabase.LoadMainAssetAtPath(assetPath) as GameObject;
						if (!root) continue;
						var components = root.GetComponentsInChildren<Component>(true);
						foreach (var component in components)
						{
							if (!component) continue;
							if (targets.ContainsKey(component)) continue;
							targets.Add(component, AssetTools.GetLocation(assetPath, component));
						}
					}
					
					foreach (var target in targets)
					{
						var unityObject = target.Key;

						var so = new SerializedObject(unityObject);
						var modified = ProcessObject(unityObject, so, target.Value, typesFilter, itemCallback);

						if (modified)
						{
							touchedObjectsCount++;
							so.ApplyModifiedProperties();
						}

						scannedObjectsCont++;
					}

					scannedAssetsCont++;
				}
			}
			catch (Exception e)
			{
				ACTk.PrintExceptionForSupport("Something went wrong while traversing objects!", e);
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
				AssetDatabase.SaveAssets();
				EditorUtility.ClearProgressBar();
			}

			Debug.Log($"{ACTk.LogPrefix}Objects modified: {touchedObjectsCount}, scanned: {scannedObjectsCont}");
		}

		
		public static void TraverseSerializedScriptsInOpenedScenes(ProcessSerializedProperty itemCallback, string[] typesFilter, bool skipSave = false)
		{
			var openedScenesPaths = CSSceneTools.GetOpenedValidScenesPaths();
			TraverseSerializedScriptsInScenes(openedScenesPaths, itemCallback, typesFilter, skipSave);
		}
		
		public static void TraverseSerializedScriptsInBuildProfilesScenes(ProcessSerializedProperty itemCallback, string[] typesFilter, bool skipSave = false)
		{
			var buildProfilesScenesPaths = CSSceneTools.GetBuildProfilesScenesPaths();
			var originalScenes = CSSceneTools.GetOpenedScenes();
			TraverseSerializedScriptsInScenes(buildProfilesScenesPaths, itemCallback, typesFilter, skipSave, originalScenes);
		}
		
		private static void TraverseSerializedScriptsInScenes(string[] scenePaths, ProcessSerializedProperty itemCallback, string[] typesFilter, bool skipSave, Scene[] originalScenes = null)
		{
			var touchedCount = 0;
			var scannedCont = 0;
			
			try
			{
				const string progressHeader = "ACTk: Looking through scenes";

				EditorUtility.DisplayProgressBar(progressHeader, "Collecting data...", 0);

				foreach (var scenePath in scenePaths)
				{
					var scene = SceneManager.GetSceneByPath(scenePath);
					if (!scene.IsValid())
						scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
					
					var roots = scene.GetRootGameObjects();
					var count = roots.Length;

					for (var j = 0; j < count; j++)
					{
						var root = roots[j];
						if (EditorUtility.DisplayCancelableProgressBar(progressHeader,
								"Item " + (j + 1) + " from " + count,
								j / (float)count))
						{
							Debug.Log(ACTk.LogPrefix + "operation canceled by user.");
							break;
						}

						var components = root.GetComponentsInChildren<Component>(true);

						foreach (var component in components)
						{
							if (!component) continue;
							var so = new SerializedObject(component);
							var modified = ProcessObject(component, so, AssetTools.GetLocation(scene.path, component), typesFilter, itemCallback);
							if (modified)
							{
								EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
								touchedCount++;
								so.ApplyModifiedProperties();
							}

							scannedCont++;
						}
					}
					
					if (scene.isDirty && !skipSave) 
						EditorSceneManager.SaveScene(scene);

					if (originalScenes != null && !Array.Exists(originalScenes,item => item == scene))
					{
						EditorSceneManager.CloseScene(scene, true);
					}
				}
			}
			catch (Exception e)
			{
				ACTk.PrintExceptionForSupport("Something went wrong while traversing objects!", e);
			}
			finally
			{
				AssetDatabase.SaveAssets();
				EditorUtility.ClearProgressBar();
			}

			Debug.Log($"{ACTk.LogPrefix}Objects modified: {touchedCount}, scanned: {scannedCont}");
		}
		
		private static bool ProcessObject(Object target, SerializedObject so, AssetLocationData location, string[] typesFilter,
			ProcessSerializedProperty callback)
		{
			var modified = false;

			var sp = so.GetIterator();
			if (sp == null) 
				return false;

			while (sp.NextVisible(true))
			{
				if (sp.propertyType != SerializedPropertyType.Generic) 
					continue;
				var type = sp.type;
				if (Array.IndexOf(typesFilter, type) == -1) 
					continue;
				if (sp.isArray)
					continue;
				
				modified |= callback(target, sp, location, type);
			}

			return modified;
		}

		#endregion
	}
}