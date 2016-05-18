// HLEnvelopePart
// by Hooligan Labs
// Every Envelope is controlled by the Compressor.  These envelopes provide lift.

using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using KSP;
using System.Linq;


namespace HLAirships
{

	public enum AnimationState : int
	{

		AtBeginning = -2,
		AtEnd = 2,
		PlayForwards = 1,
		PlayBackwards = -1

	}



	public class HLEnvelopePartModule : PartModule
	{
		// Private variables cannot be changed by the part.cfg file
		#region KSPFields
		// These public values can be overwritten in the part.cfg files.
		// The limit for buoyant force (0 to disable). Allows operation at low altitudes even with large envelope volumes, though perhaps gas could not be compressed this much.
		// The minimum atmospheric pressure the blimp can operate in.
		// Used for inflateable parachutes
		// The maximum speed at which the vessel can be made stationary

		[KSPField(isPersistant = true, guiActive = false)]
		public float limitBuoyantForce = 0f, minAtmPressure = -0.01f, dragDeployed = 0f, dragUndeployed = 0f, makeStationarySpeedMax = 1f, makeStationarySpeedClamp = 0.0f;

		[KSPField(isPersistant = true, guiActive = false, guiName = "GUI On")]
		public bool guiOn = true;

		[KSPField(isPersistant = true, guiActive = false, guiName = "Balance Envelope Adjusments")]
		public bool symmetricalPitch = false;

		// Compress and expand rate per second
		[KSPField(isPersistant = true, guiActive = false)]
		public float compressRate = 0.01f, expandRate = 0.01f;

		// The envelope volume. Not calculated from the size of the mesh because realistic values provide too much lift for stable operation, as said by Ludo
		// However, this has been recalculated based on model measurements done in Blender
		[KSPField(isPersistant = false, guiActive = true, guiName = "Max Envelope Volume", guiUnits = "m3", guiFormat = "F2")]
		public float envelopeVolume = 15f;

		// This multiplies the volume of the envelope.  Essentially, this is as if the envelope was X times larger when calculating buoyancy.  Note this does not affect mass.
		[KSPField(isPersistant = false, guiActive = false, guiName = "Envelope Volume Scale", guiFormat = "F2")]
		public float envelopeVolumeScale = 1f;

		// Target buoyant force (effectively specific volume) fraction of maximum possible, set by GUI
		[KSPField(isPersistant = true, guiActive = true, guiName = "Vessel %", guiFormat = "F2")]
		public float targetBuoyantVessel = 0.0f;
		// Is this the lead envelope?
		// Fraction of lifting gas specific volume the envelope
		[KSPField(isPersistant = true, guiActive = true, guiName = "Envelope %", guiFormat = "F3")]
		public float specificVolumeFractionEnvelope = 0.50f;
		[KSPField(isPersistant = true, guiActive = false, guiName = "Altitude Control")]
		private bool toggleAltitudeControl = false;
		[KSPField(isPersistant = true, guiActive = false, guiName = "Target Vertical Velocity", guiFormat = "F3")]
		public float targetVerticalVelocity = 0f;
		[KSPField(isPersistant = true, guiActive = false, guiName = "Make Stationary")]
		private bool togglePersistenceControl = false;
		[KSPField(isPersistant = true, guiActive = true, guiName = "Manual Pitch Control")]
		public bool toggleManualPitch = false;
		[KSPField(isPersistant = true, guiActive = false, guiName = "Automatic Pitch Control")]
		public bool toggleAutoPitch = false;
		[KSPField(isPersistant = true, guiActive = false, guiName = "Auto Pitch Adjust", guiFormat = "F5")]
		public float autoTarget = 0;
		[KSPField(isPersistant = true, guiActive = true, guiName = "Pitch %", guiFormat = "F3")]
		public float targetPitchBuoyancy = 0;
		[KSPField(isPersistant = true, guiActive = false, guiName = "Pitch Angle")]
		public float pitchAngle = 0;
		[KSPField(isPersistant = true, guiActive = false, guiName = "Deploy Animation")]
		public bool animationDeploy = false;

		[KSPField(isPersistant = true, guiActive = true, guiName = "Lead Envelope")]
		public bool isLeadEnvelope = false;

		public enum Direction { VesselRight, VesselForward, VesselUp };


		[KSPField(isPersistant = true, guiActive = false, guiName = "Auto Up")]
		public Direction stabilizeDirection = Direction.VesselUp;
		[KSPField(isPersistant = true, guiActive = false, guiName = "Stabilize Multi")]
		public float stabilizeMultiplier = 1f;
		[KSPField(isPersistant = true, guiActive = false, guiName = "Stabilize Invert")]
		public bool stabilizeInvert = false;

		[KSPField(isPersistant = true, guiActive = false)]
		public float targetBuoyancyP = 0f, targetBuoyancyN = 0f, lineOffsetMultiplier = 2f;

		[KSPField(isPersistant = false, guiActive = false, guiName = "Display Hologram")]
		public bool displayHologram = false;

		// Animation
		[KSPField(isPersistant = true)]
		public bool envelopeHasAnimation = false;

		[KSPField(isPersistant = true)]
		public string animationName = "Default Take";

		// Used if animation is triggered by key
		// [KSPField]
		// public string activationKey;

		[KSPField(isPersistant = false)]
		public float animationState = 0;
		#endregion

		#region Other Variables

		// Basic vectors
		private Vector3 heading;
		public Vector3 buoyantForce, maxBuoyancy;

		// For automatic pitch control
		float previousRotation;

		public float eDistanceFromCoM;

		private float totalBuoyancy;
		private float totalBuoyancyMax;
		private float totalMass;
		private float totalGravityForce;
		private float atmosDensity;

		public float totalTorqueP;
		public float totalTorqueN;
		public float totalBuoyancyMaxP;
		public float totalBuoyancyMaxN;
		public float totalTorqueMaxP;
		public float totalTorqueMaxN;

		private List<HLEnvelopePartModule> Envelopes = new List<HLEnvelopePartModule>();
		private Part leadEnvelope;
		private HLEnvelopePartModule envelope;

		// Debug
		// private int activeVessel = 0;

		// Debug lines
		private LineRenderer lineGravity = null;
		private LineRenderer lineCorrect = null;
		private LineRenderer linePosition = null;
		private LineRenderer lineCorrectProjected = null;
		private LineRenderer linePositionProjected = null;
		private GameObject objGravity = new GameObject();
		private GameObject objUp = new GameObject();
		private GameObject objPosition = new GameObject();
		private GameObject objUpProjected = new GameObject();
		private GameObject objPositionProjected = new GameObject();

		// Animation
		private Animation m_Animation;

		// Hackish workaround to avoid having to convert from the float KSPField and integer enum values
		private AnimationState AniState
		{
			get
			{
				return (AnimationState)((int)animationState);
			}
			set
			{
				animationState = (float)((int)value);
			}
		}
		#endregion

		#region KSPEvents

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, unfocusedRange = 2000, guiName = "Envelope Buoyancy ++")]
		public void BuoyancyPP_Event()
		{
			foreach (HLEnvelopePartModule envelope in Envelopes)
			{
				toggleAutoPitch = false;
			}
			targetPitchBuoyancy += 0.01f;
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, unfocusedRange = 2000, guiName = "Envelope Buoyancy +")]
		public void BuoyancyP_Event()
		{
			foreach (HLEnvelopePartModule envelope in Envelopes)
			{
				toggleAutoPitch = false;
			}
			targetPitchBuoyancy += 0.001f;
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, unfocusedRange = 2000, guiName = "Envelope Buoyancy -")]
		public void BuoyancyN_Event()
		{
			foreach (HLEnvelopePartModule envelope in Envelopes)
			{
				toggleAutoPitch = false;
			}
			targetPitchBuoyancy -= 0.001f;
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, unfocusedRange = 2000, guiName = "Envelope Buoyancy --")]
		public void BuoyancyNN_Event()
		{
			foreach (HLEnvelopePartModule envelope in Envelopes)
			{
				toggleAutoPitch = false;
			}
			targetPitchBuoyancy -= 0.01f;
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, unfocusedRange = 2000, guiName = "Envelope Buoyancy Max")]
		public void BuoyancyM_Event()
		{
			foreach (HLEnvelopePartModule envelope in Envelopes)
			{
				toggleAutoPitch = false;
			}
			targetPitchBuoyancy = 1f;
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, unfocusedRange = 2000, guiName = "Envelope Buoyancy Min")]
		public void BuoyancyZ_Event()
		{
			foreach (HLEnvelopePartModule envelope in Envelopes)
			{
				toggleAutoPitch = false;
			}
			targetPitchBuoyancy = -1f;
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, unfocusedRange = 2000, guiName = "Envelope Clear Adjustments")]
		public void BuoyancyC_Event()
		{
			foreach (HLEnvelopePartModule envelope in Envelopes)
			{
				toggleAutoPitch = false;
			}
			targetPitchBuoyancy = 0f;
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, unfocusedRange = 2000, guiName = "Toggle GUI")]
		public void guiToggle_Event()
		{
			part.SendEvent("guiToggle");
		}

		[KSPEvent(guiActive = false, guiActiveUnfocused = true, unfocusedRange = 2000)]
		public void guiToggle()
		{
			if(HLEnvelopeControlWindow.Instance)
			{
				HLEnvelopeControlWindow.Instance.ControlWindowVisible = !HLEnvelopeControlWindow.Instance.ControlWindowVisible;
			}
		}

		#endregion

		#region KSPActions

		[KSPAction("GUI Toggle")]
		public void guiToggle_Action(KSPActionParam param)
		{
			guiToggle_Event();
		}

		[KSPAction("Buoyancy ++")]
		public void BuoyancyP_Action(KSPActionParam param)
		{
			BuoyancyPP_Event();
		}

		[KSPAction("Buoyancy +")]
		public void BuoyancyPP_Action(KSPActionParam param)
		{
			BuoyancyP_Event();
		}

		[KSPAction("Buoyancy -")]
		public void BuoyancyN_Action(KSPActionParam param)
		{
			BuoyancyN_Event();
		}

		[KSPAction("Buoyancy --")]
		public void BuoyancyNN_Action(KSPActionParam param)
		{
			BuoyancyNN_Event();
		}

		[KSPAction("Buoyancy Max")]
		public void BuoyancyM_Action(KSPActionParam param)
		{
			BuoyancyM_Event();
		}

		[KSPAction("Buoyancy Zero")]
		public void BuoyancyZ_Action(KSPActionParam param)
		{
			BuoyancyZ_Event();
		}

		[KSPAction("Clear Adjustments")]
		public void BuoyancyC_Action(KSPActionParam param)
		{
			BuoyancyC_Event();
		}

		#endregion

		public override void OnAwake()
		{
			/// Constructor style setup. 
			/// Called in the Part\'s Awake method.  
			/// The model may not be built by this point. 

			// Set starting animation state
			Debug.Log("Set Envelope Animation");
			if (animationState == 0 || targetBuoyantVessel == 0)
			{
				// If it does not have animation start it "opened"
				if (!envelopeHasAnimation)
					animationState = 2;
				else
					animationState = -2;
			}
			else
				// Start opened if it is buoyant
				animationState = 2;

		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			if (HLBuildAidWindow.Instance != null && part.parent != null)
			{
				float treeVolume = 0;
				HLBuildAidWindow.Instance.TotalMass = FindTotalMass(this.part, out treeVolume);
				HLBuildAidWindow.Instance.TotalEnvelopeVolume = treeVolume;
			}
		}


		public override void OnUpdate()
		{
			/// Per-frame update 
			/// Called ONLY when Part is ACTIVE! 

			// Freeze in place if requested
			if (togglePersistenceControl)
				persistentParts();

			// Sets one envelope to run the GUI and control logic
			determineLeadEnvelope();


			// if (this.part != leadEnvelope) Events["guiToggle_Event"].active = false;

			// Try to animate if there is no animation
			if (envelopeHasAnimation)
				animateEnvelope();
		}


		internal static float FindTotalMass(Part part, out float envelopeVolume)
		{
			envelopeVolume = 0;
			Part rootPart = part;
			while(rootPart.parent != null)
			{
				rootPart = rootPart.parent;
			}
			return FindMassTree(rootPart, ref envelopeVolume);
		}

		private static float FindMassTree(Part part, ref float envelopeVolume)
		{
			float mass = part.mass + part.GetResourceMass();
			var envelope = part.Modules.OfType<HLEnvelopePartModule>().FirstOrDefault();
			if (envelope != null)
			{
				envelopeVolume += envelope.envelopeVolume * envelope.envelopeVolumeScale;
			}
			// recurse children
			foreach (Part childPart in part.children)
			{
				mass += FindMassTree(childPart, ref envelopeVolume);
			}
			return mass;
		}

		public void animateEnvelope()
		{
			// Beginning and end animations should be mostly removed
			switch (this.AniState)
			{
				case AnimationState.AtBeginning:
					this.m_Animation[this.animationName].speed = 0;
					if (this.m_Animation[this.animationName].time != 0.0f)
						this.m_Animation[this.animationName].time = 0.0f;
					if (animationDeploy)
					{
						this.AniState = AnimationState.PlayForwards;
						// Debug.Log("Play Forwards");
					}
					break;

				case AnimationState.AtEnd:
					this.m_Animation[this.animationName].speed = 0;
					if (this.m_Animation[this.animationName].time !=
							this.m_Animation[this.animationName].length)
						this.m_Animation[this.animationName].time =
							this.m_Animation[this.animationName].length;
					if (!animationDeploy)
					{
						this.AniState = AnimationState.PlayBackwards;
						// Debug.Log("Play Backwards");
					}
					break;

				case AnimationState.PlayForwards:
					this.m_Animation[this.animationName].speed = 1;
					/*
					if (!this.m_Animation.IsPlaying(this.animationName))
					{
						//Debug.Log("At End");
						this.AniState = AnimationState.AtEnd;
					}
					 */
					if (!animationDeploy)
					{
						this.AniState = AnimationState.PlayBackwards;
						//Debug.Log("Play Backwards");
					}
					// Should stop animation near end
					if (this.m_Animation[this.animationName].time >= this.m_Animation[this.animationName].length * 0.99) this.m_Animation[this.animationName].speed = 0;
					break;

				case AnimationState.PlayBackwards:
					this.m_Animation[this.animationName].speed = -1;
					/*
					if (!this.m_Animation.IsPlaying(this.animationName))
					{
						//Debug.Log("At Beginning");
						this.AniState = AnimationState.AtBeginning;
					}
					 */
					if (animationDeploy)
					{
						this.AniState = AnimationState.PlayForwards;
						//Debug.Log("Play Forwards");
					}
					// Should stop animation near beginning
					if (this.m_Animation[this.animationName].time <= this.m_Animation[this.animationName].length * 0.01) this.m_Animation[this.animationName].speed = 0;
					break;
			}
			// Debug.Log("Animation Time = " + this.m_Animation[this.animationName].time.ToString() + ", Animation State = " + animationState);
			this.m_Animation.Play(this.animationName);
		}

		public override void OnStart(StartState state)
		{
			// OnFlightStart seems to have been removed
			/// Called during the Part startup. 
			/// StartState gives flag values of initial state
			Debug.Log("HL Airship Plugin Start");
			specificVolumeFractionEnvelope = Mathf.Clamp01(specificVolumeFractionEnvelope);

			Debug.Log("Check for animation");
			if (this.GetComponentInChildren<Animation>() != null) m_Animation = this.GetComponentInChildren<Animation>();

			Debug.Log("Set maximum drag");
			dragUndeployed = part.maximum_drag;

			// activeVessel = FlightGlobals.ActiveVessel.GetInstanceID();
			Debug.Log("Set lead envelope");
			if (this.part == leadEnvelope) { targetBuoyantVessel = Mathf.Clamp01(targetBuoyantVessel); }

			// Debug lines
			Debug.Log("Create Lines");
			lineCorrect = objUp.AddComponent<LineRenderer>();
			linePosition = objPosition.AddComponent<LineRenderer>();
			linePositionProjected = objPositionProjected.AddComponent<LineRenderer>();
			lineCorrectProjected = objUpProjected.AddComponent<LineRenderer>();
			lineGravity = objGravity.AddComponent<LineRenderer>();

			Debug.Log("Parameterize Lines");
			try { makeLines(linePosition, Color.red); makeLines(linePositionProjected, Color.green); makeLines(lineCorrect, Color.blue); makeLines(lineCorrectProjected, Color.magenta); makeLines(lineGravity, Color.white); }
			catch (Exception ex) { print("makeLines Exception!"); print(ex.Message); }
		}

		public void makeLines(LineRenderer line, Color color)
		{
			line.transform.parent = null; // ...child to our part...
			line.useWorldSpace = true; // ...and moving along with it (rather 
									   // than staying in fixed world coordinates)
									   // line.transform.localPosition = Vector3.zero;
									   // line.transform.localEulerAngles = Vector3.zero;

			// Make it render a red to yellow triangle, 1 meter wide and 2 meters long
			line.material = new Material(Shader.Find("Particles/Additive"));
			line.SetColors(color, color);
			line.SetWidth(0.5f, 0);
			line.SetVertexCount(2);
			line.SetPosition(0, Vector3.zero);
			line.SetPosition(1, Vector3.zero);
		}

		public override void OnFixedUpdate()
		{
			/// Per-physx-frame update 
			/// Called ONLY when Part is ACTIVE!
			/// 

			if (leadEnvelope == this.part) leadEnvelopeUpdate();

			// Update buoyancy properties
			envelopeUpdate();
		}

		private void determineLeadEnvelope()
		{
			// Finds all 
			try { findEnvelopes(); }
			catch (Exception ex) { print("findEnvelopes Exception!"); print(ex.Message); }
		}


		public void leadEnvelopeUpdate()
		{
			// Direction in which the ship currently points
			heading = (Vector3d)this.vessel.transform.up;

			// Up, relative to the center of the nearest celestial body
			Vector3d up = (this.vessel.findWorldCenterOfMass() - this.vessel.mainBody.position).normalized;

			// Finds all parts within the vessel (specifically mass)
			try { findParts(); }
			catch (Exception ex) { print("findParts Exception!"); print(ex.Message); }

			// Keeps the airship at a steady altitude
			try { altitudeControl(); }
			catch (Exception ex) { print("altitudeControl Exception!"); print(ex.Message); }

			// Tells each envelope what its buoyancy should be
			try { setBuoyancy(); }
			catch (Exception ex) { print("setBuoyancy Exception!"); print(ex.Message); }
		}

		private void SyncControlUIValues()
		{
			if (HLEnvelopeControlWindow.Instance && vessel == FlightGlobals.ActiveVessel)
			{
				HLEnvelopeControlWindow.Instance.MakeStationarySpeedMax = makeStationarySpeedMax;
				HLEnvelopeControlWindow.Instance.MaxBuoyancy = maxBuoyancy;
				HLEnvelopeControlWindow.Instance.TotalBuoyancy = totalBuoyancy;

				toggleAltitudeControl = HLEnvelopeControlWindow.Instance.ToggleAltitudeControl;
				if (toggleAltitudeControl)
				{
					// if altitude control is on, send calculated buoyancy value to GUI
					HLEnvelopeControlWindow.Instance.TargetBuoyantVessel = targetBuoyantVessel;
				}
				else
				{
					// otherwise we just read the setting
					targetBuoyantVessel = HLEnvelopeControlWindow.Instance.TargetBuoyantVessel;
				}
				targetVerticalVelocity = HLEnvelopeControlWindow.Instance.TargetVerticalVelocity;
				if(toggleAutoPitch != HLEnvelopeControlWindow.Instance.ToggleAutoPitch ||
					symmetricalPitch != HLEnvelopeControlWindow.Instance.SymmetricalPitch ||
					stabilizeDirection != HLEnvelopeControlWindow.Instance.StabilizeDirection ||
					stabilizeInvert != HLEnvelopeControlWindow.Instance.StabilizeInvert )
				{
					foreach(var envelope in Envelopes)
					{
						envelope.targetPitchBuoyancy = 0;
					}
				}
				toggleAutoPitch = HLEnvelopeControlWindow.Instance.ToggleAutoPitch;
				toggleManualPitch = HLEnvelopeControlWindow.Instance.ToggleManualPitch;
				symmetricalPitch = HLEnvelopeControlWindow.Instance.SymmetricalPitch;
				togglePersistenceControl = HLEnvelopeControlWindow.Instance.TogglePersistenceControl;
				stabilizeDirection = HLEnvelopeControlWindow.Instance.StabilizeDirection;
				stabilizeInvert = HLEnvelopeControlWindow.Instance.StabilizeInvert;
				pitchAngle = HLEnvelopeControlWindow.Instance.PitchAngle;
				displayHologram = HLEnvelopeControlWindow.Instance.DisplayHologram;
				lineOffsetMultiplier = HLEnvelopeControlWindow.Instance.LineOffsetMultiplier;
			}
		}

		private void InitControlUIValues()
		{
			if (HLEnvelopeControlWindow.Instance && vessel == FlightGlobals.ActiveVessel)
			{
				HLEnvelopeControlWindow.Instance.MakeStationarySpeedMax = makeStationarySpeedMax;
				HLEnvelopeControlWindow.Instance.MaxBuoyancy = maxBuoyancy;
				HLEnvelopeControlWindow.Instance.TotalBuoyancy = totalBuoyancy;
				HLEnvelopeControlWindow.Instance.CurrentVessel = vessel;

				HLEnvelopeControlWindow.Instance.TargetBuoyantVessel = targetBuoyantVessel;
				HLEnvelopeControlWindow.Instance.TargetVerticalVelocity = targetVerticalVelocity;
				HLEnvelopeControlWindow.Instance.ToggleAltitudeControl = toggleAltitudeControl;
				HLEnvelopeControlWindow.Instance.ToggleAutoPitch = toggleAutoPitch;
				HLEnvelopeControlWindow.Instance.ToggleManualPitch = toggleManualPitch;
				HLEnvelopeControlWindow.Instance.SymmetricalPitch = symmetricalPitch;
				HLEnvelopeControlWindow.Instance.TogglePersistenceControl = togglePersistenceControl;
				HLEnvelopeControlWindow.Instance.StabilizeDirection = stabilizeDirection;
				HLEnvelopeControlWindow.Instance.StabilizeInvert = stabilizeInvert;
				HLEnvelopeControlWindow.Instance.PitchAngle = pitchAngle;
				HLEnvelopeControlWindow.Instance.DisplayHologram = displayHologram;
				HLEnvelopeControlWindow.Instance.LineOffsetMultiplier = lineOffsetMultiplier;
			}
		}

		private void altitudeControl()
		{
			// Zero Net Force = Gravity / Max Possible Buoyancy
			if (toggleAltitudeControl)
			{
				// Tosh's PID Controller
				// Two attempts are used here, neither worked as well as our basic method.
				// float ControlAmount = brakePid.Control(targetVerticalVelocity - (float)this.vessel.verticalSpeed);
				// targetBoyantForceFractionCompressor = Mathf.Clamp01(brakePid.Control(this.vessel.verticalSpeed);

				// My method
				// Previous method, when used during onUpdate
				// targetBoyantForceFractionCompressor = Mathf.Clamp01(totalGravityForce / totalBuoyancyMax + (float)((targetVerticalVelocity - this.vessel.verticalSpeed) * 0.02));
				// New method
				targetBuoyantVessel = Mathf.Clamp01(totalGravityForce / totalBuoyancyMax + (float)((targetVerticalVelocity - this.vessel.verticalSpeed) * Time.deltaTime));

			}
			else
				togglePersistenceControl = false;
		}

		// Gathers information on the envelopes
		public void findEnvelopes()
		{
			Envelopes.Clear();
			totalBuoyancy = 0;
			totalTorqueP = 0;
			totalTorqueN = 0;
			totalTorqueMaxP = 0;
			totalTorqueMaxN = 0;
			totalBuoyancyMax = 0;
			totalBuoyancyMaxP = 0;
			totalBuoyancyMaxN = 0;

			// More info: http://www.grc.nasa.gov/WWW/k-12/WindTunnel/Activities/balance_of_forces.html

			foreach (Part part in this.vessel.parts)
			{
				if (part.isAttached)
				{
					// Skip if not an envelope
					if (part.Modules.OfType<HLEnvelopePartModule>().FirstOrDefault() == null)
					{
						// Debug.Log("Did not find an envelope on " + part.name);
						continue;
					}
					envelope = part.Modules.OfType<HLEnvelopePartModule>().FirstOrDefault();

					// Envelopes have to be active to work.
					if (part.State != PartStates.ACTIVE)
						part.force_activate();

					Envelopes.Add(envelope);
					totalBuoyancy += envelope.buoyantForce.magnitude;
					totalBuoyancyMax += envelope.maxBuoyancy.magnitude;

				
					// Legacy pitch control
					if (envelope.eDistanceFromCoM > 0)
					{
						totalTorqueP += envelope.buoyantForce.magnitude * envelope.eDistanceFromCoM;
						totalBuoyancyMaxP += envelope.maxBuoyancy.magnitude;
						totalTorqueMaxP += envelope.maxBuoyancy.magnitude * envelope.eDistanceFromCoM;
					}
					if (envelope.eDistanceFromCoM < 0)
					{
						totalTorqueN += envelope.buoyantForce.magnitude * envelope.eDistanceFromCoM;
						totalBuoyancyMaxN += envelope.maxBuoyancy.magnitude;
						totalTorqueMaxN += envelope.maxBuoyancy.magnitude * envelope.eDistanceFromCoM * -1;
					}
					

				}

				// If this is the lead envelope...
				if (leadEnvelope == envelope.part)
				{
					// Debug.Log("Adding GUI!");
					envelope.isLeadEnvelope = true;


				}// If not, then check to see if this envelope should be promoted to lead envelope
				else if (!vessel.parts.Contains(leadEnvelope))
				{
					leadEnvelope = envelope.part;
				}
				else envelope.isLeadEnvelope = false;
			}
			if(HLEnvelopeControlWindow.Instance)
			{
				HLEnvelopeControlWindow.Instance.Envelopes = Envelopes;
				if(FlightGlobals.ActiveVessel != HLEnvelopeControlWindow.Instance.CurrentVessel)
				{
					InitControlUIValues();
				}
				SyncControlUIValues();
			}
		}

		private void findParts()
		{
			// Hey, a method for finding mass!
			totalMass = vessel.GetTotalMass();

			totalGravityForce = (float)(totalMass * FlightGlobals.getGeeForceAtPosition(part.WCoM).magnitude);
		}

		public void setBuoyancy()
		{
			float sumPitch = 0;

			// Find out the sum of all the pitches
			foreach (HLEnvelopePartModule envelope in Envelopes)
			{
				sumPitch += envelope.targetPitchBuoyancy;
			}

			float averagePitch = sumPitch / Envelopes.Count;
			Vector3 vCoM = vessel.findWorldCenterOfMass();
			Vector3 gravity = FlightGlobals.getGeeForceAtPosition(vCoM).normalized;

			// The lead envelope sets the buoyancy for every envelope, including itself
			foreach (HLEnvelopePartModule envelope in Envelopes)
			{
				envelope.targetBuoyantVessel = this.targetBuoyantVessel;


				if (toggleManualPitch)
				{
					envelope.targetPitchBuoyancy = manualPitchControl(envelope.targetBuoyantVessel, envelope.eDistanceFromCoM);
				}
				else
				{
					if (toggleAutoPitch)
					{
						envelope.targetPitchBuoyancy += autoPitchControl(envelope.part.WCoM, envelope, vCoM, gravity);
					}
					else
					{
						envelope.targetPitchBuoyancy = 0;
						envelope.autoTarget = 0;
					}
				}


				envelope.targetPitchBuoyancy = Mathf.Clamp(envelope.targetPitchBuoyancy, -0.25f, 0.25f);

				float adjustBy = envelope.targetPitchBuoyancy;
				// Balance out pitches to 0
				if (symmetricalPitch)
				{
					adjustBy -= averagePitch;
				}

				// Use total
				envelope.updateTargetSpecificVolumeFraction(Mathf.Clamp01(adjustBy + targetBuoyantVessel), envelope);
			}
		}

		
		public float manualPitchControl(float target, float distance)
		{
			// Envelopes in front and back are separately affected by pitch controls
			if (distance > 0)
			{
				target = targetBuoyancyP * eDistanceFromCoM;
			}
			else
			{
				target = targetBuoyancyN * eDistanceFromCoM;
			}
			return target;
		}

		private float altitudePGain = 1/500000.0f;
		private float altitudeDGain = 1/10.0f;
		private float smallThreshold = 0.0000005f;

		public float autoPitchControl(Vector3 position, HLEnvelopePartModule envelope, Vector3 vCoM, Vector3 gravity)
		{
			Vector3 stabilizeVector = Vector3.one;
			Vector3 pitchVector = Vector3.one;
			switch (stabilizeDirection)
			{
				case Direction.VesselForward:
					stabilizeVector = vessel.transform.forward;
					pitchVector = -1 * vessel.transform.up;
					// rotation = vessel.ctrlState.pitch;
					break;
				case Direction.VesselRight:
					stabilizeVector = vessel.transform.right;
					pitchVector = -1 * vessel.transform.up;
					// rotation = vessel.ctrlState.roll;
					break;
				case Direction.VesselUp:
					stabilizeVector = vessel.transform.up;
					pitchVector = -1 * vessel.transform.forward;
					// rotation = vessel.ctrlState.pitch;
					break;
			}

			// Factor in multiplier
			stabilizeVector *= stabilizeMultiplier;
			if (stabilizeInvert)
			{
				stabilizeVector *= -1;
				pitchVector *= -1;
			}

			// Pitch angle
			if (pitchAngle < 0)
			{
				pitchVector *= -1;
			}

			// Slerp (yummy!) is a spherical rotation of a vector towards something
			stabilizeVector = Vector3.Slerp(stabilizeVector, pitchVector, Mathf.Abs(pitchAngle) / 90);

			// Removes vertical component from the vector we are stabilizing to and the envelope's position
			Vector3 correctVector = ProjectVectorOnPlane(gravity, stabilizeVector);

			Vector3 positionVector = ProjectVectorOnPlane(gravity, position - vCoM);

			Vector3 lineOffset = gravity * -lineOffsetMultiplier;

			// Are we already rotating towards the correct position?
			float rotation = Vector3.Angle(stabilizeVector, -1 * gravity);
			float correctionFactor = Vector3.Dot(correctVector * altitudePGain, positionVector);
			if ((envelope.previousRotation - rotation) < 0.001)
			{
				// P Term
				// Project correction onto relative position to get correction amount
				envelope.autoTarget = correctionFactor;
			}
			else
			{
				// if we're moving in the right direction, don't add any more P term
				envelope.autoTarget = 0;
			}

			// D Term
			// Try to dampen rotation
			envelope.autoTarget += Math.Sign(correctionFactor) * Math.Abs(envelope.targetPitchBuoyancy) * (rotation - envelope.previousRotation) * altitudeDGain;

			// Save this rotation
			envelope.previousRotation = rotation;

			// Do not send small adjustments
			if (Math.Abs(envelope.autoTarget) < smallThreshold)
			{
				envelope.autoTarget = 0;
			}

			if (displayHologram)
			{
				envelope.lineGravity.SetPosition(0, vCoM + lineOffset);
				envelope.lineGravity.SetPosition(1, vCoM + gravity + lineOffset);

				envelope.linePosition.SetPosition(0, vCoM + lineOffset);
				envelope.linePosition.SetPosition(1, envelope.part.WCoM + lineOffset);

				envelope.linePositionProjected.SetPosition(0, position + lineOffset);
				envelope.linePositionProjected.SetPosition(1, position + (maxBuoyancy * envelope.targetPitchBuoyancy * 2) + lineOffset);

				envelope.lineCorrect.SetPosition(0, vCoM + lineOffset);
				envelope.lineCorrect.SetPosition(1, vCoM + stabilizeVector * 2.0f + lineOffset);

				envelope.lineCorrectProjected.SetPosition(0, vCoM + lineOffset);
				envelope.lineCorrectProjected.SetPosition(1, vCoM + correctVector + lineOffset);
			}
			else
			{
				envelope.lineGravity.SetPosition(0, Vector3.zero);
				envelope.lineGravity.SetPosition(1, Vector3.zero);

				envelope.linePosition.SetPosition(0, Vector3.zero);
				envelope.linePosition.SetPosition(1, Vector3.zero);

				envelope.linePositionProjected.SetPosition(0, Vector3.zero);
				envelope.linePositionProjected.SetPosition(1, Vector3.zero);

				envelope.lineCorrect.SetPosition(0, vCoM + Vector3.zero);
				envelope.lineCorrect.SetPosition(1, vCoM + Vector3.zero);

				envelope.lineCorrectProjected.SetPosition(0, Vector3.zero);
				envelope.lineCorrectProjected.SetPosition(1, Vector3.zero);
			}

			// Return result
			return envelope.autoTarget;
		}

		// From http://wiki.unity3d.com/index.php?title=3d_Math_functions
		//Projects a vector onto a plane. The output is not normalized.
		public static Vector3 ProjectVectorOnPlane(Vector3 planeNormal, Vector3 vector)
		{
			return vector - (Vector3.Dot(vector, planeNormal) * planeNormal);
		}

		// I talked to Mu and it sounds like saving and loading is going to be redone in 0.19 or 0.20, so I'm not going to worry about mid-air saving until then.
		// Update:  0.19 and 0.20 have come and gone an no word on this being redone still.  :P
		private void persistentParts()
		{
			// this.vessel.SetWorldVelocity(new Vector3d(0,0,0));

			// This code causes vessel to explode when executed
			//if (this.vessel.srf_velocity.magnitude > makeStationarySpeedClamp)
			//{
			//	this.vessel.SetWorldVelocity(vessel.srf_velocity.normalized * makeStationarySpeedClamp * 0.9);
			//}

			vessel.Landed = true;
			// vessel.ctrlState.mainThrottle = 0;
		}

		// Envelopes determine their positions and effect buoyant force on themselves
		public void envelopeUpdate()
		{
			Vector3 stabilizeVector = Vector3.one;
			switch (stabilizeDirection)
			{
				case Direction.VesselForward:
					stabilizeVector = vessel.transform.forward;
					break;
				case Direction.VesselRight:
					stabilizeVector = vessel.transform.right;
					break;
				case Direction.VesselUp:
					stabilizeVector = vessel.transform.up;
					break;
			}
			try { eDistanceFromCoM = DistanceFromCoM(part.WCoM, vessel.findWorldCenterOfMass(), vessel.mainBody.position, stabilizeVector); }
			catch (Exception ex) { print("eDistanceFromCoM Exception!"); print(ex.Message); }

			float pressure = (float)FlightGlobals.getStaticPressure();

			// Compute the buoyant force

			// KSP provides the density based on local atmospheric pressure.
			atmosDensity = (float)FlightGlobals.getAtmDensity(pressure, FlightGlobals.getExternalTemperature());

			// The maximum buoyancy of the envelope is equal to the weight of the displaced air
			// Force (Newtons) = - Gravitational Acceleration (meters per second squared) * Density (kilograms / meter cubed) * Volume (meters cubed)
			maxBuoyancy = -FlightGlobals.getGeeForceAtPosition(part.WCoM) * atmosDensity * envelopeVolume;
			// As force in KSP is measured in kilonewtons, we divide by 1000
			maxBuoyancy /= 1000;
			// Then we apply the "volume scale", for gameplay purposes if needed.
			maxBuoyancy *= envelopeVolumeScale;
			// The final force is dependent on how filled the envelope is.
			buoyantForce = maxBuoyancy * specificVolumeFractionEnvelope;

			// If set, clamp the magnitude of the buoyant force to perhaps prevent overly straining the vessel at low altitude...
			// Written by Ludo, not tested by Hooligan Labs.
			if (limitBuoyantForce > 0)
				if (buoyantForce.magnitude > limitBuoyantForce)
					buoyantForce = buoyantForce.normalized * limitBuoyantForce;

			if (specificVolumeFractionEnvelope < 0.0001 || pressure < minAtmPressure)
			{
				animationDeploy = false;
				if (dragDeployed != 0)
				{
					part.minimum_drag = dragUndeployed;
					part.maximum_drag = dragUndeployed;
				}
			}
			else
			{
				animationDeploy = true;
				// If deployed, the drag is a function of how deployed the balloon is
				if (dragDeployed != 0 && this.m_Animation[this.animationName].length != 0)
				{
					part.minimum_drag = dragDeployed * (this.m_Animation[this.animationName].time / this.m_Animation[this.animationName].length);
					part.maximum_drag = dragDeployed * (this.m_Animation[this.animationName].time / this.m_Animation[this.animationName].length);
				}
			}

			// TODO - scale anguglar drag based on volume
			this.part.angularDrag = envelopeVolume / 10.0f;
			// New method is to add force based on render bounds.
			this.part.Rigidbody.AddForceAtPosition(buoyantForce, part.WCoM, ForceMode.Force);
			// limit angular velocity to avoid dangerous occilations
			if (buoyantForce.magnitude > 30)
			{
				this.part.Rigidbody.angularDrag = 1.0f;
				this.part.Rigidbody.maxAngularVelocity = 1.0f;
			}
			if (part.Splashed || part.WaterContact)
			{
				//part.waterAngularDragMultiplier
				this.part.Rigidbody.angularDrag = 5.1f;
				this.part.Rigidbody.maxDepenetrationVelocity = 10;
				this.part.Rigidbody.maxAngularVelocity = 0.5f;
				// TODO - reduce force based on water penetration to avoid double counting buoyancy
				float reducedMagnitude = buoyantForce.magnitude - part.buoyancy;
			}

		}

		// Changes envelope buoyancy targets
		public void updateTargetSpecificVolumeFraction(float fraction, HLEnvelopePartModule envelope)
		{
			// Difference between specific volume and target specific volume
			float delta = fraction - envelope.specificVolumeFractionEnvelope;

			// Clamp the change to compress and expand rates
			delta = Mathf.Clamp(delta, -compressRate, expandRate);

			envelope.specificVolumeFractionEnvelope += delta * Time.deltaTime;
		}

		// Distance from center of mass
		public static float DistanceFromCoM(Vector3 position, Vector3 CoM, Vector3 mainBody, Vector3 direction)
		{
			// project the vectors on the plane with up as a normal
			return Vector3.Dot((position - mainBody) - (CoM - mainBody), direction);
		}


	}


}