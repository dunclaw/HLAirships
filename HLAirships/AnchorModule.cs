using System;
using System.Collections.Generic;
using UnityEngine;

namespace HLAirships
{
	class AnchorModule : PartModule
	{
		[KSPField(isPersistant = true, guiActive = true, guiName = "Anchor")]
		Boolean anchored;
		[KSPField(isPersistant = true, guiActive = true, guiName = "AutoAnchor")]
		Boolean autoAnchor;
		[KSPField(isPersistant = true, guiActive = false)]
		Vector3 anchorPosition = new Vector3(0f, 0f, 0f);
		[KSPField(isPersistant = true, guiActive = false)]
		Vector3 anchorVelocity = new Vector3(0f, 0f, 0f);
		[KSPField(isPersistant = true, guiActive = false)]
		Vessel.Situations previousState = Vessel.Situations.LANDED;

		private static Vector3 zeroVector = new Vector3(0f, 0f, 0f);

		public override void OnStart(StartState state)
		{
			if (state != StartState.Editor)
			{
				anchorPosition = vessel.GetWorldPos3D();
				part.force_activate();
				previousState = vessel.situation;
				if(HLEnvelopeControlWindow.Instance != null)
				{
					HLEnvelopeControlWindow.Instance.AnchorPresent = true;
					HLEnvelopeControlWindow.Instance.AnchorOn = anchored;
					HLEnvelopeControlWindow.Instance.AutoAnchor = autoAnchor;
				}
			}
		}

		public override void OnFixedUpdate()
		{
			// can't anchor if we're orbiting
			if (vessel.situation == Vessel.Situations.SUB_ORBITAL || vessel.situation == Vessel.Situations.ORBITING)
			{
				autoAnchor = false;
				anchored = false;
			}
			// if we're inactive, see if we want to anchor 
			if (!vessel.isActiveVessel && autoAnchor)
			{
				anchorPosition = vessel.GetWorldPos3D();
				// if we're less than 1.5km from the active vessel and anchored, then wake up
				if ((vessel.GetWorldPos3D() - FlightGlobals.ActiveVessel.GetWorldPos3D()).magnitude < 1500.0f && anchored)
				{
					anchored = false;
					vessel.GoOffRails();
					vessel.Landed = false;
					vessel.situation = previousState;
					vessel.SetWorldVelocity(anchorVelocity - Krakensbane.GetFrameVelocity());
				}
				// if we're farther than 2km, auto anchor if needed
				if ((vessel.GetWorldPos3D() - FlightGlobals.ActiveVessel.GetWorldPos3D()).magnitude > 2000.0f && (!anchored))
				{
					anchored = true;
					AnchorVessel();
				}
			}
			// if we're not anchored, and not active and flying, then go off rails
			if ( !anchored && !vessel.isActiveVessel && vessel.situation == Vessel.Situations.FLYING)
			{
				vessel.GoOffRails();
			}
			if (anchored)
			{
				vessel.SetPosition(anchorPosition);
				vessel.SetWorldVelocity(zeroVector);
				vessel.acceleration = zeroVector;
				vessel.angularVelocity = zeroVector;
				vessel.geeForce = 0.0;
				vessel.situation = Vessel.Situations.LANDED;
				vessel.Landed = true;
			}
			if(HLEnvelopeControlWindow.Instance != null)
			{
				// save the previous state if needed
				if(HLEnvelopeControlWindow.Instance.AnchorOn && !anchored)
				{
					previousState = vessel.situation;
					AnchorVessel();
				}
				anchored = HLEnvelopeControlWindow.Instance.AnchorOn;
				autoAnchor = HLEnvelopeControlWindow.Instance.AutoAnchor;
			}
		}

		[KSPEvent(guiActive = true, guiName = "Toggle Anchor")]
		public void ToggleAnchor()
		{
			// cannot anchor in orbit or sub-orbit
			if (vessel.situation != Vessel.Situations.SUB_ORBITAL && vessel.situation != Vessel.Situations.ORBITING)
			{
				if (!anchored)
				{
					AnchorVessel();
				}
				else
				{
					vessel.SetWorldVelocity(anchorVelocity);
					vessel.situation = previousState;
				}
				anchored = !anchored;
				HLEnvelopeControlWindow.Instance.AnchorOn = anchored;
			}
		}


		[KSPEvent(guiActive = true, guiName = "Toggle AutoAnchor")]
		public void ToggleAutoAnchor()
		{
			autoAnchor = !autoAnchor;
			HLEnvelopeControlWindow.Instance.AutoAnchor = autoAnchor;
		}

		private void AnchorVessel()
		{
			anchorPosition = vessel.GetWorldPos3D();
			anchorVelocity = vessel.GetSrfVelocity();
			previousState = vessel.situation;
		}
	}
}
