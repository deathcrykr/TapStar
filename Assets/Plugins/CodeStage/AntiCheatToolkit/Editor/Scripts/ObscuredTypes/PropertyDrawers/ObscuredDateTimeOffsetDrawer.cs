#region copyright
// ------------------------------------------------------
// Copyright (C) Dmitriy Yukhanov [https://codestage.net]
// ------------------------------------------------------
#endregion

using System;
using System.Globalization;
using CodeStage.AntiCheat.ObscuredTypes;
using CodeStage.AntiCheat.ObscuredTypes.EditorCode;
using CodeStage.AntiCheat.Utils;
using UnityEditor;
using UnityEngine;

namespace CodeStage.AntiCheat.EditorCode.PropertyDrawers
{
	[CustomPropertyDrawer(typeof(ObscuredDateTimeOffset))]
	internal class ObscuredDateTimeOffsetDrawer : ObscuredTypeDrawer<SerializedObscuredDateTimeOffset, DateTimeOffset>
	{
		private string input;

		protected override void DrawProperty(Rect position, SerializedProperty sp, GUIContent label)
		{
			var dateString = plain.ToString("o", DateTimeFormatInfo.InvariantInfo);
			input = EditorGUI.DelayedTextField(position, label, dateString);
		}

		protected override void ApplyChanges()
		{
			DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out plain);

			serialized.Hidden = ObscuredDateTimeOffset.Encrypt(plain, serialized.Key);
			serialized.Hash = HashUtils.CalculateHash(plain.UtcTicks);
		}
	}
} 