#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitriy Yukhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

using System;
using System.IO;
using CodeStage.AntiCheat.Common;
using CodeStage.AntiCheat.Detectors;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEngine;

namespace CodeStage.AntiCheat.EditorCode.Processors
{
	public abstract class BaseLinkerProcessor : IUnityLinkerProcessor
	{
		public int callbackOrder { get; }
		protected string path;
		private static string linkData;

		public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
		{
			try
			{
				linkData ??= ConstructLinkData();
				File.WriteAllText(path, linkData);
			}
			catch (Exception e)
			{
				ACTk.PrintExceptionForSupport("Couldn't write link.xml!", e);
			}
			
			Debug.Log($"{ACTk.LogPrefix}Additional link.xml generated:\n{path}");
			return path;
		}

		protected abstract string ConstructLinkData();
		
#if !UNITY_2021_2_OR_NEWER
		public void OnBeforeRun(BuildReport report, UnityLinkerBuildPipelineData data)
		{
			// ignoring since it was deprecated in Unity 2021.2
		}

		public void OnAfterRun(BuildReport report, UnityLinkerBuildPipelineData data)
		{
			// ignoring since it was deprecated in Unity 2021.2
		}
#endif
	}
}