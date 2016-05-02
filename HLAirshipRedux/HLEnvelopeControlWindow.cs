using KSPPluginFramework;
using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;
using System.ComponentModel;

namespace HLEnvelope
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class HLEnvelopeControlWindowFlight : HLEnvelopeControlWindow
	{
		public override string MonoName { get { return this.name; } }
		//public override bool ViewAlarmsOnly { get { return false; } }
	}

	internal enum ButtonStyleEnum
	{
		[Description("Basic button")]
		Basic,
		[Description("Common Toolbar (by Blizzy78)")]
		Toolbar,
		[Description("KSP App Launcher Button")]
		Launcher,
	}

	public class HLEnvelopeControlWindow : MonoBehaviourExtended
	{

		public bool ControlWindowVisible { get; set; }
		private IButton ToolbarButton { get; set; }
		public virtual String MonoName { get; set; }
		public static HLEnvelopeControlWindow Instance { get; set; }
		public float TargetBuoyantVessel { get; set; }
		public float TargetVerticalVelocity { get; set; }
		public bool ToggleAltitudeControl { get; set; }
		public bool ToggleAutoPitch { get; set; }
		public float TotalBuoyancy { get; set; }
		public Vessel CurrentVessel { get; set; }
		public float MakeStationarySpeedMax { get; set; }
		public bool TogglePersistenceControl { get; set; }
		public Vector3 MaxBuoyancy { get; set; }
		public HLEnvelopePartModule.Direction StabilizeDirection { get; set; }
		public bool StabilizeInvert { get; set; }
		public float PitchAngle { get; set; }
		public bool DisplayHologram { get; set; }
		public float LineOffsetMultiplier { get; set; }

		public List<HLEnvelopePartModule> Envelopes = new List<HLEnvelopePartModule>();

		private Rect windowPos;
		private int airshipWindowID;
		private bool activeGUI;
		private float windowWidth;
		private bool resetGUIsize = false;
		private bool willReset1 = false;
		private bool willReset2 = false;
		// private bool willReset3 = false;
		private bool willReset4 = false;

		internal override void Awake()
		{
			//setup the Toolbar button if necessary
			if (ToolbarManager.ToolbarAvailable)
			{
				ToolbarButton = InitToolbarButton();
			}
			Instance = this;
			InitVariables();
		}
		private void InitVariables()
		{
			airshipWindowID = UnityEngine.Random.Range(1000, 2000000) + _AssemblyName.GetHashCode();
		}

		internal override void OnGUIEvery()
		{
			if (ControlWindowVisible)
			{
				if(!activeGUI)
				{
					initGUI();
					activeGUI = true;
				}

				drawGUI();
			}
			else
			{
				if (activeGUI)
				{
					activeGUI = false;
				}
			}
		}

		private void drawGUI()
		{
			windowPos = GUILayout.Window(airshipWindowID, windowPos, WindowGUI, "HooliganLabs", GUILayout.MinWidth(200));
		}

		protected void initGUI()
		{
			windowWidth = 300;

			if ((windowPos.x == 0) && (windowPos.y == 0))
			{
				windowPos = new Rect(Screen.width - windowWidth, Screen.height * 0.25f, 10, 10);
			}
		}

		// GUI
		private void WindowGUI(int windowID)
		{
			float targetBoyantForceFractionCompressorTemp;
			targetBoyantForceFractionCompressorTemp = TargetBuoyantVessel;

			float targetVerticalVelocityTemp;
			targetVerticalVelocityTemp = TargetVerticalVelocity;


			#region General GUI
			// General GUI window information
			GUIStyle mySty = new GUIStyle(GUI.skin.button);
			mySty.normal.textColor = mySty.focused.textColor = Color.white;
			mySty.hover.textColor = mySty.active.textColor = Color.yellow;
			mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
			mySty.padding = new RectOffset(2, 2, 2, 2);

			// Buoyancy -, current %, and + buttons
			GUILayout.BeginHorizontal();
			if (GUILayout.RepeatButton("-", mySty))
			{
				TargetBuoyantVessel -= 0.002f;
				ToggleAltitudeControl = false;
			}
			TargetBuoyantVessel = Mathf.Clamp01(TargetBuoyantVessel);

			GUILayout.Label("        " + Mathf.RoundToInt(TargetBuoyantVessel * 100) + "%");

			if (GUILayout.RepeatButton("+", mySty))
			{
				TargetBuoyantVessel += 0.002f;
				ToggleAltitudeControl = false;
			}
			GUILayout.EndHorizontal();

			// Slider control.  Also is set by the other controls.
			GUILayout.BeginHorizontal();
			float temp = TargetBuoyantVessel;
			TargetBuoyantVessel = GUILayout.HorizontalSlider(TargetBuoyantVessel, 0f, 1f);
			if (temp != TargetBuoyantVessel)
			{
				ToggleAltitudeControl = false;
			}
			GUILayout.EndHorizontal();

			TargetBuoyantVessel = Mathf.Clamp01(TargetBuoyantVessel);
			#endregion

			#region Toggle Altitude
			// Altitude control.  Should be deactivated when pressing any other unrelated control.
			GUILayout.BeginHorizontal();
			string toggleAltitudeControlString = "Altitude Control Off";
			if (ToggleAltitudeControl) toggleAltitudeControlString = "Altitude Control On";
			ToggleAltitudeControl = GUILayout.Toggle(ToggleAltitudeControl, toggleAltitudeControlString);
			GUILayout.EndHorizontal();
			#endregion

			if (ToggleAltitudeControl)
			{
				#region Altitude Control
				willReset1 = true;

				// Vertical Velocity -, target velocity, and + buttons
				GUILayout.BeginHorizontal();
				GUILayout.Label("Target Vertical Velocity");
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				if (GUILayout.RepeatButton("--", mySty)) TargetVerticalVelocity -= 0.1f;
				if (GUILayout.Button("-", mySty)) TargetVerticalVelocity -= 0.1f;
				if (GUILayout.Button(TargetVerticalVelocity.ToString("00.0") + " m/s", mySty)) TargetVerticalVelocity = 0;
				if (GUILayout.Button("+", mySty)) TargetVerticalVelocity += 0.1f;
				if (GUILayout.RepeatButton("++", mySty)) TargetVerticalVelocity += 0.1f;
				GUILayout.EndHorizontal();
				#endregion

				#region Persistence
				// Allows "landing" (persistence) by slowing the vehicle, as long as the this.vessel has buoyancy
				// dunclaw: not sure what this was for, but it appears to cause a crash and isn't necessary
				//if (TotalBuoyancy > 0f)
				//{
				//	if (Mathf.Abs((int)CurrentVessel.horizontalSrfSpeed) < MakeStationarySpeedMax && Mathf.Abs((int)CurrentVessel.verticalSpeed) < MakeStationarySpeedMax && (CurrentVessel.Landed || CurrentVessel.Splashed))
				//	{
				//		GUILayout.BeginHorizontal();
				//		TogglePersistenceControl = GUILayout.Toggle(TogglePersistenceControl, "Make Slow to Save", mySty);
				//		GUILayout.EndHorizontal();
				//	}
				//	else
				//	{
				//		GUILayout.BeginHorizontal();
				//		if (this.CurrentVessel.Landed || this.CurrentVessel.Splashed)
				//		{
				//			GUILayout.Label("Reduce Speed to Save");
				//		}
				//		else if (MaxBuoyancy.magnitude > 0)
				//		{
				//			GUILayout.Label("Touch Ground to Save");
				//			TogglePersistenceControl = false;
				//		}
				//		GUILayout.EndHorizontal();
				//	}
				//}
				#endregion
			}
			else
			{
				TargetVerticalVelocity = 0;
				if (willReset1)
				{
					resetGUIsize = true;
					willReset1 = false;
				}
			}

			#region Toggle Pitch
			if (Envelopes.Count > 1)
			{
				/*
				GUILayout.BeginHorizontal();
				// if (toggleAutoPitch) toggleManualPitch = false;
				string toggleManualPitchString = "Manual Pitch Off";
				if (toggleManualPitch) toggleManualPitchString = "Manual Pitch On";
				toggleManualPitch = GUILayout.Toggle(toggleManualPitch, toggleManualPitchString);
				GUILayout.EndHorizontal();
				*/

				// As Auto Pitch is not working, it has been commented out

				/*
				GUILayout.BeginHorizontal();
				// if (toggleManualPitch) toggleAutoPitch = false;
				string symmetricalPitchString = "Symetrical Pitch Off";
				if (symmetricalPitch) symmetricalPitchString = "Symetrical Pitch On";
				symmetricalPitch = GUILayout.Toggle(symmetricalPitch, symmetricalPitchString);
				GUILayout.EndHorizontal();

				if (symmetricalPitch)
				{
					GUILayout.BeginHorizontal();
					// if (toggleManualPitch) toggleAutoPitch = false;
					string toggleAutoPitchString = "Auto Pitch Off";
					if (toggleAutoPitch) toggleAutoPitchString = "Auto Pitch On";
					toggleAutoPitch = GUILayout.Toggle(toggleAutoPitch, toggleAutoPitchString);
					GUILayout.EndHorizontal();
				}
				else toggleAutoPitch = false;
				 */
			}
			#endregion

			if (ToggleAutoPitch)
			{
				willReset4 = true;
				if (GUILayout.Button("Up is " + StabilizeDirection))
				{
					switch (StabilizeDirection)
					{
						case HLEnvelopePartModule.Direction.VesselUp:
							StabilizeDirection = HLEnvelopePartModule.Direction.VesselForward;
							StabilizeInvert = true;
							break;
						case HLEnvelopePartModule.Direction.VesselForward:
							StabilizeDirection = HLEnvelopePartModule.Direction.VesselRight;
							StabilizeInvert = false;
							break;
						case HLEnvelopePartModule.Direction.VesselRight:
							StabilizeDirection = HLEnvelopePartModule.Direction.VesselUp;
							StabilizeInvert = false;
							break;
					}
				}
			}
			else
			{
				if (willReset4)
				{
					resetGUIsize = true;
					willReset4 = false;
				}
			}

			if (ToggleAutoPitch)
			{
				willReset2 = true;
				/*
				string stabilizeStrengthString = stabilizeMultiplier.ToString("0.00");
				if (stabilizeInvert) stabilizeStrengthString = "-" + stabilizeMultiplier.ToString("0.00");
				if (GUILayout.Button("Stabilizing Strength " + stabilizeStrengthString))
					stabilizeMultiplier = 1f;
				stabilizeMultiplier = GUILayout.HorizontalSlider(stabilizeMultiplier, 0.1f, 10f);
				stabilizeInvert = GUILayout.Toggle(stabilizeInvert, "Invert Direction");
				 */

				// Pitch angle setting
				GUILayout.BeginHorizontal();
				if (GUILayout.Button("-")) PitchAngle -= 1f;
				if (GUILayout.Button("Pitch Angle " + PitchAngle.ToString("0"))) PitchAngle = 0f;
				if (GUILayout.Button("+")) PitchAngle += 1f;
				GUILayout.EndHorizontal();

				PitchAngle = GUILayout.HorizontalSlider(PitchAngle, -90f, 90f);

				DisplayHologram = GUILayout.Toggle(DisplayHologram, "Display Hologram at " + LineOffsetMultiplier.ToString("F1"));
				if (DisplayHologram)
				{
					LineOffsetMultiplier = GUILayout.HorizontalSlider(LineOffsetMultiplier, -20f, 20f);
				}
			}
			else
			{
				DisplayHologram = false;
				if (willReset2)
				{
					resetGUIsize = true;
					willReset2 = false;
				}
			}

			#region Manual Pitch Control Buttons
			/*
			// Manual pitch control.  Works alongside other controls.
			if(toggleManualPitch)
			{
				willReset3 = true;
				GUILayout.BeginHorizontal();
				if (GUILayout.Button("Pitch Up", mySty))
				{
					targetBuoyancyP += 0.01f;
					targetBuoyancyN -= 0.01f;
				}
				if (GUILayout.Button("Pitch Down", mySty))
				{
					targetBuoyancyP -= 0.01f;
					targetBuoyancyN += 0.01f;
				}
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.Label("Forward \t\t\t Rear");
				GUILayout.EndHorizontal();
				GUILayout.BeginHorizontal();
				if (GUILayout.Button("-", mySty))
				{
					targetBuoyancyP -= 0.01f;
				}
				targetBuoyancyP = Mathf.Clamp(targetBuoyancyP, -1f, 1f);
				GUILayout.Label(Mathf.RoundToInt(targetBuoyancyP * 100) + "%");
				if (GUILayout.Button("+", mySty))
				{
					targetBuoyancyP += 0.01f;
				}
				if (GUILayout.Button("-", mySty))
				{
					targetBuoyancyN -= 0.01f;
				}
				targetBuoyancyN = Mathf.Clamp(targetBuoyancyN, -1f, 1f); 
				GUILayout.Label(Mathf.RoundToInt(targetBuoyancyN * 100) + "%");
				if (GUILayout.Button("+", mySty))
				{
					targetBuoyancyN += 0.01f;
				}
				GUILayout.EndHorizontal();

				foreach (HLEnvelopePartModule envelope in Envelopes)
				{
					envelope.targetBuoyancyN = targetBuoyancyN;
					envelope.targetBuoyancyP = targetBuoyancyP;
				}
			}
			else if (willReset3)
			{
				resetGUIsize = true;
				willReset3 = false;
			}
			*/
			#endregion


			if (resetGUIsize)
			{
				// Reset window size
				windowPos.Set(windowPos.x, windowPos.y, 10, 10);
				resetGUIsize = false;
			}

			#region Debug
			// Debug info

			// if (Mathf.Abs(targetBoyantForceFractionCompressorTemp - targetBoyantForceFractionCompressor) > 0.002)
			// debugHLAirship("targetBoyantForceFractionCompressor", targetBoyantForceFractionCompressor.ToString("0.000"));

			// if (targetVerticalVelocityTemp != targetVerticalVelocity)
			// debugHLAirship("targetVerticalVelocityTemp", targetVerticalVelocity.ToString("00.0"));

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Buoyancy: " + totalBuoyancy.ToString("0.0"));
			//GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Gravity: " + totalGravityForce.ToString("0.0"));
			//GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Buoyancy - Weight: " + (TotalBuoyancy - (CurrentVessel.GetTotalMass() * FlightGlobals.getGeeForceAtPosition(CurrentVessel.findWorldCenterOfMass()).magnitude)).ToString("0.00"));
			GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Angle from Up: " + (ContAngle(heading, up, up)).ToString("0.0"));
			//GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Front Torque: " + (totalTorqueP).ToString("0.0"));
			//GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Rear Torque: " + (totalTorqueN).ToString("0.0"));
			//GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Front B: " + (targetBuoyancyP).ToString("0.00"));
			//GUILayout.EndHorizontal();

			//GUILayout.BeginHorizontal();
			//GUILayout.Label("Rear B: " + (targetBuoyancyN).ToString("0.00"));
			//GUILayout.EndHorizontal();

			//int x = 0;
			//foreach (HLEnvelopePart envelope in Envelopes)
			//{
			//    GUILayout.BeginHorizontal();
			//    GUILayout.Label("Env" + x + " Location: " + (envelope.eDistanceFromCoM).ToString("0.00"));
			//    GUILayout.EndHorizontal();
			//    GUILayout.BeginHorizontal();
			//    GUILayout.Label("Env" + x + " Buoyancy: " + (envelope.buoyantForce.magnitude).ToString("0.00"));
			//    GUILayout.EndHorizontal();
			//    GUILayout.BeginHorizontal();
			//    GUILayout.Label("Env" + x + " Specific Volume: " + (envelope.specificVolumeFractionEnvelope).ToString("0.00"));
			//    GUILayout.EndHorizontal();
			//    GUILayout.BeginHorizontal();
			//    GUILayout.Label("Env" + x + " targetPitchBuoyancy: " + (envelope.targetPitchBuoyancy).ToString("0.00"));
			//    GUILayout.EndHorizontal();
			//    GUILayout.BeginHorizontal();
			//    GUILayout.Label("Env" + x + " targetBoyantForceFractionCompressor: " + (envelope.targetBoyantForceFractionCompressor).ToString("0.00"));
			//    GUILayout.EndHorizontal();

			//    x += 1;
			//}

			GUI.DragWindow(new Rect(0, 0, 500, 20));
			#endregion
		}


		/// <summary>
		/// initialises a Toolbar Button for this mod
		/// </summary>
		/// <returns>The ToolbarButtonWrapper that was created</returns>
		internal IButton InitToolbarButton()
		{
			IButton btnReturn = null;
			try
			{
				LogFormatted("Initialising the Toolbar Icon");
				btnReturn = ToolbarManager.Instance.add("HooliganLabs", "HooliganLabsGUI");
				btnReturn.TexturePath = "HooliganLabs/Icons/HLOffIcon";
				btnReturn.ToolTip = "HooliganLabs";
				btnReturn.OnClick += (e) =>
				{
					btnReturn.TexturePath = ControlWindowVisible ? "HooliganLabs/Icons/HLOffIcon" : "HooliganLabs/Icons/HLOnIcon";
					ControlWindowVisible = !ControlWindowVisible;
				};
			}
			catch (Exception ex)
			{
				DestroyToolbarButton(btnReturn);
				LogFormatted("Error Initialising Toolbar Button: {0}", ex.Message);
			}
			return btnReturn;
		}

		/// <summary>
		/// Destroys theToolbarButtonWrapper object
		/// </summary>
		/// <param name="btnToDestroy">Object to Destroy</param>
		internal void DestroyToolbarButton(IButton btnToDestroy)
		{
			if (btnToDestroy != null)
			{
				LogFormatted("Destroying Toolbar Button");
				btnToDestroy.Destroy();
			}
			btnToDestroy = null;
		}
	}
}
