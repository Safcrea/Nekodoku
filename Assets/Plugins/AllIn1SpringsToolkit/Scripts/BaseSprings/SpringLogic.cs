using UnityEngine;
using System.Runtime.CompilerServices;

namespace AllIn1SpringsToolkit
{
	public static class SpringLogic
	{
		public static void UpdateSpring(float deltaTime,Spring spring, float forceThreshold = 7500f)
		{
			for(int i = 0; i < spring.springValues.Length; i++)
			{
				if(!spring.springValues[i].IsEnabled()) { continue; }

				float force = spring.unifiedForceAndDrag ? spring.unifiedForce : spring.springValues[i].GetForce();
				float drag = spring.unifiedForceAndDrag ? spring.unifiedDrag : spring.springValues[i].GetDrag();

				CheckTargetClamping(spring.springValues[i], spring.clampingEnabled);
				
				// Use fast semi-implicit for normal forces, analytical solution for extreme forces
				// We should hardly ever use the analytical solution, but it's there for stability
				if(force > forceThreshold)
				{
					// Fall back to stable analytical solution for extreme forces
					CoefficientBasedIntegration(deltaTime, force, drag, spring.springValues[i]);
				}
				else
				{
					// Use fast semi-implicit integration for normal forces (most common case)
					SemiImplicitIntegration(deltaTime, force, drag, spring.springValues[i]);
				}
				
				CheckCurrentValueClamping(spring.springValues[i], spring.clampingEnabled);
			}
		}

		private static void SemiImplicitIntegration(float deltaTime, float force, float drag, SpringValues springValues)
		{
			float distanceToTarget = springValues.GetTarget() - springValues.GetCurrentValue();
			float currentVelocity = springValues.GetVelocity();
			
			// Semi-implicit integration step 1: Apply drag to velocity first
			float dragFactor = 1.0f / (1.0f + drag * deltaTime);
			currentVelocity *= dragFactor;
			
			// Semi-implicit integration step 2: Calculate spring force
			float springForce = force * distanceToTarget;
			
			// Semi-implicit integration step 3: Update velocity with spring force
			currentVelocity += springForce * deltaTime;
			
			// Semi-implicit integration step 4: Update position with new velocity
			float newCandidateValue = springValues.GetCurrentValue() + currentVelocity * deltaTime;
			
			#if UNITY_2021_2_OR_NEWER
			// Use the built-in IsFinite in newer Unity versions
			if(!float.IsFinite(newCandidateValue) || !float.IsFinite(currentVelocity))
			{
				springValues.ReachEquilibrium();
				return;
			}
			#else
			// Use the alternative approach for Unity 2019.4 and earlier
			if(float.IsNaN(newCandidateValue) || float.IsInfinity(newCandidateValue) || 
			   float.IsNaN(currentVelocity) || float.IsInfinity(currentVelocity))
			{
			    springValues.ReachEquilibrium();
			    return;
			}
			#endif
			
			springValues.SetVelocity(currentVelocity);
			springValues.SetCandidateValue(newCandidateValue);
		}
		
		private static void CheckCurrentValueClamping(SpringValues springValues, bool clampingEnabled)
		{
			if(clampingEnabled)
			{
				if(springValues.IsOvershot() && springValues.GetClampCurrentValue())
				{
					springValues.Clamp();

					if(springValues.GetStopSpringOnCurrentValueClamp())
					{
						springValues.Stop();
					}
				}
			}
		}

		private static void CheckTargetClamping(SpringValues springValues, bool clampingEnabled)
		{
			if(clampingEnabled && springValues.GetClampTarget())
			{
				springValues.ClampTarget();
			}
		}

		#region Backup Solid But Expenisve Integration Method
		
		// Only used for extreme force values (>1000)
		// Holds coefficients for analytical spring solution
		private readonly struct SpringAnalyticalSolution
		{
			// Position update coefficients
			// newPosition = (currentPosition * positionFactor) + (currentVelocity * velocityFactor) + targetPosition
			public readonly float positionFactor;    // Multiplier for current position
			public readonly float velocityFactor;    // Multiplier for current velocity
			
			// Velocity update coefficients
			// newVelocity = (currentPosition * positionToVelocityFactor) + (currentVelocity * velocityDecayFactor)
			public readonly float positionToVelocityFactor;  // How position affects new velocity
			public readonly float velocityDecayFactor;       // How velocity decays over time
			
			public SpringAnalyticalSolution(
				float positionFactor, 
				float velocityFactor, 
				float positionToVelocityFactor, 
				float velocityDecayFactor)
			{
				this.positionFactor = positionFactor;
				this.velocityFactor = velocityFactor;
				this.positionToVelocityFactor = positionToVelocityFactor;
				this.velocityDecayFactor = velocityDecayFactor;
			}
		}
		
		// Special case integration for extreme force values
		// This method is rarely called but provides stability when needed
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void CoefficientBasedIntegration(float deltaTime, float force, float drag, SpringValues springValues)
		{
			// Skip integration if deltaTime is zero
			if(deltaTime <= 0f)
			{
				return;
			}
			
			// Handle potential divide-by-zero for extreme forces
			if(force <= 0f)
			{
				// Simple drag-only case with no spring force
				float dragFactor = Mathf.Exp(-drag * deltaTime);
				float newVel = springValues.GetVelocity() * dragFactor;
				float newValue = springValues.GetCurrentValue() + springValues.GetVelocity() * (1f - dragFactor) / Mathf.Max(0.0001f, drag);
				
				springValues.SetVelocity(newVel);
				springValues.SetCandidateValue(newValue);
				return;
			}
			
			// Calculate spring parameters
			float angularFrequency = Mathf.Sqrt(force);
			float dampingRatio = drag / (2f * angularFrequency);
			
			// Calculate analytical solution for this timestep
			SpringAnalyticalSolution solution = CalculateSpringAnalyticalSolution(deltaTime, angularFrequency, dampingRatio);
			
			// Current position and velocity (relative to target)
			float currentPosRelative = springValues.GetCurrentValue() - springValues.GetTarget();
			float currentVelocity = springValues.GetVelocity();
			
			// Apply analytical solution to get new position and velocity
			float newPosition = (currentPosRelative * solution.positionFactor) + 
			                    (currentVelocity * solution.velocityFactor) + 
			                    springValues.GetTarget();
							   
			float newVelocity = (currentPosRelative * solution.positionToVelocityFactor) + 
			                    (currentVelocity * solution.velocityDecayFactor);
			
			// Safety check for non-finite values
			#if UNITY_2021_2_OR_NEWER
			// Use the built-in IsFinite in newer Unity versions
			if(!float.IsFinite(newPosition) || !float.IsFinite(newVelocity))
			{
				springValues.ReachEquilibrium();
				return;
			}
			#else
			// Use the alternative approach for Unity 2019.4 and earlier
			if(float.IsNaN(newPosition) || float.IsInfinity(newPosition) || 
			   float.IsNaN(newVelocity) || float.IsInfinity(newVelocity))
			{
			    springValues.ReachEquilibrium();
			    return;
			}
			#endif
			
			// Update spring state
			springValues.SetVelocity(newVelocity);
			springValues.SetCandidateValue(newPosition);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static SpringAnalyticalSolution CalculateSpringAnalyticalSolution(float deltaTime, float angularFrequency, float dampingRatio)
		{
			const float epsilon = 0.0001f;
			
			// Force values into legal range
			if(dampingRatio < 0.0f) dampingRatio = 0.0f;
			if(angularFrequency < 0.0f) angularFrequency = 0.0f;
			
			// If there is no angular frequency, the spring will not move
			if(angularFrequency < epsilon)
			{
				return new SpringAnalyticalSolution(1.0f, 0.0f, 0.0f, 1.0f);
			}
			
			float positionFactor, velocityFactor, positionToVelocityFactor, velocityDecayFactor;
			
			if(dampingRatio > 1.0f + epsilon)
			{
				// Over-damped case - no oscillation, smooth return to equilibrium
				float za = -angularFrequency * dampingRatio;
				float zb = angularFrequency * Mathf.Sqrt(dampingRatio * dampingRatio - 1.0f);
				float z1 = za - zb;
				float z2 = za + zb;
				
				float e1 = Mathf.Exp(z1 * deltaTime);
				float e2 = Mathf.Exp(z2 * deltaTime);
				
				float invTwoZb = 1.0f / (2.0f * zb); // = 1 / (z2 - z1)
				
				float e1_Over_TwoZb = e1 * invTwoZb;
				float e2_Over_TwoZb = e2 * invTwoZb;
				
				float z1e1_Over_TwoZb = z1 * e1_Over_TwoZb;
				float z2e2_Over_TwoZb = z2 * e2_Over_TwoZb;
				
				positionFactor = e1_Over_TwoZb * z2 - z2e2_Over_TwoZb + e2;
				velocityFactor = -e1_Over_TwoZb + e2_Over_TwoZb;
				
				positionToVelocityFactor = (z1e1_Over_TwoZb - z2e2_Over_TwoZb + e2) * z2;
				velocityDecayFactor = -z1e1_Over_TwoZb + z2e2_Over_TwoZb;
			}
			else if(dampingRatio < 1.0f - epsilon)
			{
				// Under-damped case - oscillation with decreasing amplitude
				float omegaZeta = angularFrequency * dampingRatio;
				float alpha = angularFrequency * Mathf.Sqrt(1.0f - dampingRatio * dampingRatio);
				
				float expTerm = Mathf.Exp(-omegaZeta * deltaTime);
				float cosTerm = Mathf.Cos(alpha * deltaTime);
				float sinTerm = Mathf.Sin(alpha * deltaTime);
				
				float invAlpha = 1.0f / alpha;
				
				float expSin = expTerm * sinTerm;
				float expCos = expTerm * cosTerm;
				float expOmegaZetaSin_Over_Alpha = expTerm * omegaZeta * sinTerm * invAlpha;
				
				positionFactor = expCos + expOmegaZetaSin_Over_Alpha;
				velocityFactor = expSin * invAlpha;
				
				positionToVelocityFactor = -expSin * alpha - omegaZeta * expOmegaZetaSin_Over_Alpha;
				velocityDecayFactor = expCos - expOmegaZetaSin_Over_Alpha;
			}
			else
			{
				// Critically damped case - fastest return to equilibrium without oscillation
				float expTerm = Mathf.Exp(-angularFrequency * deltaTime);
				float timeExp = deltaTime * expTerm;
				float timeExpFreq = timeExp * angularFrequency;
				
				positionFactor = timeExpFreq + expTerm;
				velocityFactor = timeExp;
				
				positionToVelocityFactor = -angularFrequency * timeExpFreq;
				velocityDecayFactor = -timeExpFreq + expTerm;
			}
			
			return new SpringAnalyticalSolution(positionFactor, velocityFactor, positionToVelocityFactor, velocityDecayFactor);
		}
		#endregion
	}
}