#region copyright
// ------------------------------------------------------
// Copyright (C) Dmitriy Yukhanov [https://codestage.net]
// ------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using CodeStage.AntiCheat.Common;
using CodeStage.AntiCheat.ObscuredTypes;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodeStage.AntiCheat.EditorCode
{
	public delegate void InvalidPropertyFound(Object target, SerializedProperty sp, string location, string type);
	
	/// <summary>
	/// Class with utility functions to help with ACTk migrations after updates.
	/// </summary>
	public static class ObscuredTypesValidator
	{
		internal const string ModuleName = "Obscured Types Validator";
		
		private static InvalidPropertyFound lastPassedCallback;
		private static HashSet<string> invalidAssetPaths;
		private static int invalidPropertiesFound;

		/// <summary>
		/// Traverses all prefabs and scriptable objects in the project and checks if they contain any Obscured types with anomalies.
		/// </summary>
		/// <param name="assetPaths">Specify target asset(s) or pass null to scan whole project.</param>
		/// <param name="callback">Pass callback if you wish to process invalid objects.</param>
		/// <param name="skipInteraction">Skips intro confirmation.</param>
		/// <returns>Array of paths that require migration.</returns>
		public static string[] ValidateProjectAssets(string[] assetPaths = null, InvalidPropertyFound callback = null, bool skipInteraction = false)
		{
			if (!skipInteraction)
			{
				if (assetPaths == null || assetPaths.Length == 0)
				{
					if (!EditorUtility.DisplayDialog(ModuleName,
							"Are you sure you wish to scan ALL Prefabs and Scriptable Objects?\n" +
							"This can take some time to complete.",
							"Yes", "No"))
					{
						Debug.Log(ACTk.LogPrefix + ModuleName + ": canceled by user.");
						return null;
					}
				}
				else if (assetPaths.Length > 1000 && !EditorUtility.DisplayDialog(ModuleName,
							 $"Are you sure you wish to scan {assetPaths.Length} Prefabs and Scriptable Objects?",
							 "Yes", "No"))
				{
					Debug.Log(ACTk.LogPrefix + ModuleName + ": canceled by user.");
					return null;
				}
			}

			invalidAssetPaths = new HashSet<string>();

			var types = TypeCache.GetTypesDerivedFrom<ISerializableObscuredType>().Where(t => !t.IsAbstract && !t.IsInterface)
				.Select(type => type.Name).ToArray();
			lastPassedCallback = callback;
			EditorTools.TraverseSerializedScriptsAssets(assetPaths, ValidateProperty, types);
			lastPassedCallback = null;
			
			Debug.Log(ACTk.LogPrefix + ModuleName + ": complete.");

			return invalidAssetPaths.ToArray();
		}

		
		/// <summary>
		/// Traverse all currently opened scenes and checks if they contain any non-valid Obscured types.
		/// </summary>
		/// <param name="callback">Pass callback if you wish to process invalid objects.</param>
		/// <param name="skipInteraction">Skips intro confirmation.</param>
		/// <returns>Number of invalid properties.</returns>
		public static int ValidateOpenedScenes(InvalidPropertyFound callback = null, bool skipInteraction = false)
		{
			if (!skipInteraction && !ConfirmValidation("all opened Scenes"))
				return invalidPropertiesFound;

			return ValidateScenes(EditorTools.TraverseSerializedScriptsInOpenedScenes, callback);
		}
		
		/// <summary>
		/// Traverses all scenes added to all BuildProfiles (ex BuildSettings) and checks if they contain any non-valid Obscured types.
		/// </summary>
		/// <param name="callback">Pass callback if you wish to process invalid objects.</param>
		/// <param name="skipInteraction">Skips intro confirmation.</param>
		/// <returns>Number of invalid properties.</returns>
		public static int ValidateBuildProfilesScenes(InvalidPropertyFound callback = null, bool skipInteraction = false)
		{
			if (!skipInteraction && !ConfirmValidation("all scenes in " + ACTkMenuItems.BuildProfilesLabel))
				return invalidPropertiesFound;

			return ValidateScenes(EditorTools.TraverseSerializedScriptsInBuildProfilesScenes, callback);
		}
		
		private static bool ConfirmValidation(string target)
		{
			return EditorUtility.DisplayDialog(ModuleName,
				$"Are you sure you wish to scan {target} and validate all found obscured types?",
				"Yes", "No");
		}
		
		private static int ValidateScenes(Action<ProcessSerializedProperty, string[], bool> traverseAction, InvalidPropertyFound callback)
		{
			invalidPropertiesFound = 0;

			var types = TypeCache.GetTypesDerivedFrom<ISerializableObscuredType>().Where(t => !t.IsAbstract && !t.IsInterface)
				.Select(type => type.Name).ToArray();
			lastPassedCallback = callback;
			traverseAction(ValidateProperty, types, true);
			lastPassedCallback = null;

			Debug.Log(ACTk.LogPrefix + ModuleName + ": complete.");

			return invalidPropertiesFound;
		}
		
		private static bool ValidateProperty(Object target, SerializedProperty sp, AssetLocationData location, string type)
		{
			var obscured = sp.GetValue<ISerializableObscuredType>();
			if (obscured != null && !obscured.IsDataValid)
			{
				lastPassedCallback?.Invoke(target, sp, location.ToString(), type);
				target = EditorTools.GetPingableObject(target);
				Debug.LogWarning($"{ACTk.LogPrefix}{ModuleName} found invalid property [{sp.displayName}] of type [{type}] at:\n{location}", target);
				invalidPropertiesFound++;
				invalidAssetPaths?.Add(location.AssetPath);
			}

			return false;
		}
	}
}