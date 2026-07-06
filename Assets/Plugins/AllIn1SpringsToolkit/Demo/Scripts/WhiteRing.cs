using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class WhiteRing : MonoBehaviour
    {
        [SerializeField] private TransformSpringComponent transformSpring;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float maxScaleVelocityMagnitude;

        private float maxScaleVelocity;
        private Color ringColor;
        
        private void Start()
        {
            Vector3 iniScale = transform.localScale;
            transformSpring.SetCurrentValueScale(Vector3.zero);
            transformSpring.SetTargetScale(iniScale);
            
            ringColor = spriteRenderer.color;
        }

        private void Update()
        {
            float scaleVelocityMagnitude = transformSpring.GetVelocityScale().magnitude;
            scaleVelocityMagnitude = Mathf.Clamp(scaleVelocityMagnitude, 0f, maxScaleVelocityMagnitude);
            float scaleVelocityNormalized = scaleVelocityMagnitude / maxScaleVelocityMagnitude;
            ringColor.a = scaleVelocityNormalized;
            spriteRenderer.color = ringColor;

            maxScaleVelocity = Mathf.Max(maxScaleVelocity, scaleVelocityMagnitude);
            if(maxScaleVelocity > 0.5f && scaleVelocityNormalized < 0.01f)
            {
                Destroy(gameObject);
            }
        }
    }
}