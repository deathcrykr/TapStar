#region copyright
// ------------------------------------------------------
// Copyright (C) Dmitriy Yukhanov [https://codestage.net]
// ------------------------------------------------------
#endregion

namespace CodeStage.AntiCheat.Examples
{
	using Time;
	using UnityEngine;

	// speed-hack resistant version of the InfiniteRotator.cs
	[AddComponentMenu("")]
	internal class InfiniteRotatorReliable : InfiniteRotator
	{
		protected override float GetDeltaTime()
		{
			return SpeedHackProofTime.unscaledDeltaTime;
		}
	}
}