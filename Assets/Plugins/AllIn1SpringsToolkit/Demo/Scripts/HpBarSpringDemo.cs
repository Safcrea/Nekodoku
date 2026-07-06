using System;
using UnityEngine;
using UnityEngine.UI;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    //This script is a demo of how to use several springs to juice up a health bar
    //Code explanation in the Update method
    public class HpBarSpringDemo : DemoElement
    {
        [Space, Header("Position Springs")]
        [SerializeField] private AnchoredPositionSpringComponent leftHpBarSpringComponent;
        [SerializeField] private AnchoredPositionSpringComponent rightHpBarSpringComponent;
        [SerializeField] private float forcePerFullHp;
        [SerializeField] private RectTransform hpBar, hpBarHolder;

        [Space, Header("Fill Springs")]
        [SerializeField]private Image hpGreenFillImage;
        [SerializeField] private Image hpWhiteFillImage;
        [SerializeField] private UiSliderSpringComponent hpGreenSliderSpringComponent, hpWhiteSliderSpringComponent;

        [Space, Header("Green Bar Color Spring")]
        [SerializeField] private Image hpGreenImage;
        [SerializeField] private Gradient hpColorGradient;
        [SerializeField] private ColorSpringComponent hpGreenColorSpringComponent;

        [Space, Header("Scale Spring")]
        [SerializeField] private TransformSpringComponent scaleSpringComponent;
        [SerializeField] private float scaleVelOnHealthChange, scaleVelOnHealthLimits;
        
        [Space, Header("Limits white and red colors")]
        [SerializeField] private ColorSpringComponent backBarColorSpringComponent;
        [SerializeField] private Image backBarImage;
        [SerializeField] private float whiteFlashVel, redFlashVel;

        private RectTransform leftHpBarRectTransform, rightHpBarRectTransform;
        private float currentHp = 1f;
        private Vector2 currentPivot;

		public override void Initialize(bool hideUi)
		{
			base.Initialize(hideUi);

			currentPivot = new Vector2(0.5f, 0.5f);
			leftHpBarRectTransform = (RectTransform)leftHpBarSpringComponent.transform;
			rightHpBarRectTransform = (RectTransform)rightHpBarSpringComponent.transform;
			hpGreenFillImage.fillAmount = currentHp;
			hpWhiteFillImage.fillAmount = currentHp;

			hpGreenColorSpringComponent.SetTarget(hpColorGradient.Evaluate(currentHp));
			hpGreenColorSpringComponent.ReachEquilibrium();

			backBarColorSpringComponent.SetTarget(backBarImage.color);
			backBarColorSpringComponent.ReachEquilibrium();

			leftHpBarSpringComponent.SetTarget(leftHpBarRectTransform.anchoredPosition);
			leftHpBarSpringComponent.ReachEquilibrium();

			rightHpBarSpringComponent.SetTarget(rightHpBarRectTransform.anchoredPosition);
			rightHpBarSpringComponent.ReachEquilibrium();
		}

		private void Update()
        {
            //We change the pivot based on the hp bar. Hp can be from 0 to 1. We use that to change the pivot from left to right too
            currentPivot.x = GetCurrentPivotX();
            ChangePivotAndPreservePosition(hpBar, currentPivot);

            //We use 2 position springs to move the bar and use them as pivots
            //Depending on how low or high the hp is we'll affect more one side or the other
            //We then change the rotation of the bar to match the orientation of the springs
            Vector3 hpBarOrientation = leftHpBarRectTransform.position - rightHpBarRectTransform.position;

            //We want to move the bar up and down too the closer the hp is to half the bar
            //Otherwise we just rotate it
            Vector3 midPoint = rightHpBarRectTransform.position + (hpBarOrientation * 0.5f);
            float midPointInfluence = 1f - (Mathf.Abs(0.5f - currentPivot.x) * 2f);
            midPoint = Vector3.Lerp(hpBarHolder.position, midPoint, midPointInfluence);
            hpBarHolder.position = midPoint;

            //Now that the pivot and position are set we can rotate the bar so that it aligns with the 2 AnchoredPositionSpring
            hpBar.right = -hpBarOrientation.normalized;

            //We set the colors that the 2 ColorSprings are animating
            hpGreenImage.color = hpGreenColorSpringComponent.GetCurrentValue();
            backBarImage.color = backBarColorSpringComponent.GetCurrentValue();
        }

        private float GetCurrentPivotX() => RemapFloat(Mathf.Clamp01(1f - currentHp), 0f, 1f, 0.1f, 0.9f);

        public void AddRemoveHp(float amount)
        {
            if(!isOpen)
            {
                return;
            }
            
            float sign = Mathf.Sign(amount);

            float rightInfluence = 1f - currentPivot.x;
            rightHpBarSpringComponent.AddVelocity(Vector2.up * (forcePerFullHp * rightInfluence * sign));
            
            float leftInfluence = currentPivot.x;
            leftHpBarSpringComponent.AddVelocity(Vector2.up * (forcePerFullHp * leftInfluence * sign));

            currentHp = Mathf.Clamp01(currentHp + amount);
            currentPivot.x = GetCurrentPivotX();

            if(sign < 0)
            {
				hpGreenSliderSpringComponent.SetTarget(currentHp);
				hpGreenSliderSpringComponent.ReachEquilibrium();

				hpWhiteSliderSpringComponent.SetTarget(currentHp);

			}
            else
            {
				hpWhiteSliderSpringComponent.SetTarget(currentHp);
				hpWhiteSliderSpringComponent.ReachEquilibrium();

				hpGreenSliderSpringComponent.SetTarget(currentHp);
			}

            hpGreenColorSpringComponent.SetTarget(hpColorGradient.Evaluate(currentHp));
            if(currentHp < 0.01f)
            {
                scaleSpringComponent.AddVelocityScale(new Vector3(sign * scaleVelOnHealthLimits, 0f, 0f));
                backBarColorSpringComponent.AddVelocity(new Vector3(1f, 0f, 0f) * redFlashVel);
            }
            else if(currentHp > 0.99f)
            {
                scaleSpringComponent.AddVelocityScale(new Vector3(sign * scaleVelOnHealthLimits, 0f, 0f));
                hpGreenColorSpringComponent.AddVelocity(Vector3.one * whiteFlashVel);
            }
            else scaleSpringComponent.AddVelocityScale(new Vector3(1f, 2f, 0f) * (amount * scaleVelOnHealthChange));
        }

        private void ChangePivotAndPreservePosition(RectTransform rectTransform, Vector2 newPivot)
        {
            Vector2 oldPivot = rectTransform.pivot;
            Vector3 oldPosition = rectTransform.localPosition;

            Rect rect = rectTransform.rect;
            Vector3 offset = new Vector3(
                (newPivot.x - oldPivot.x) * rect.width,
                (newPivot.y - oldPivot.y) * rect.height,
                0.0f
            );

            rectTransform.pivot = newPivot;
            rectTransform.localPosition = oldPosition + offset;
        }

        private float RemapFloat(float inValue, float inMin, float inMax, float outMin, float outMax)
        {
            return outMin + (inValue - inMin) * (outMax - outMin) / (inMax - inMin);
        }
    }
}