using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class SpaceshipRockMove : MonoBehaviour
    {
        [SerializeField] private TransformSpringComponent rockTransformSpring;
        [SerializeField] private Transform rockTransform;
        [SerializeField] private float rockMinScale, rockMaxScale;
        [SerializeField] private float uniformXZScaleMin, uniformXZScaleMax;
        [SerializeField] private float startScaleDownTime;
        [SerializeField] private float moveSpeed;

        private float timeTillScaleDown;
        Vector3 moveDirection;
        
        public void Initialize(Vector3 spawnPosition)
        {
            gameObject.SetActive(true);
            timeTillScaleDown = startScaleDownTime;
            moveDirection = -transform.parent.forward;
            
            transform.position = spawnPosition;
            rockTransform.rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            rockTransform.localScale = Vector3.one * Random.Range(rockMinScale, rockMaxScale);

            float randomUniformScale = Random.Range(uniformXZScaleMin, uniformXZScaleMax);
            Vector3 flattenedScale = new Vector3(randomUniformScale, 1f, randomUniformScale);
            rockTransformSpring.SetTargetScale(flattenedScale);
            rockTransformSpring.ReachEquilibrium();
        }

        private void Update()
        {
            transform.position += moveDirection * (moveSpeed * Time.deltaTime);
            
            if(timeTillScaleDown > 0f)
            {
                timeTillScaleDown -= Time.deltaTime;
                return;
            }
            
            rockTransformSpring.SetTargetScale(Vector3.zero);
            if(transform.localScale.magnitude < 0.1f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}