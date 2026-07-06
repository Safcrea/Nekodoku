using UnityEngine;
using Random = UnityEngine.Random;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class SnakeDemoController : DemoElement
    {
        [Space, Header("Snake References")]
        [SerializeField] private Transform snakeTargetTransform;
        
        [Space, Header("Auto Rotate")]
        [SerializeField] private float rotationSpeed = 30f;
        [SerializeField] private float noiseScale = 0.5f;
        [SerializeField] private float timeScale = 0.1f;
        
        [Space, Header("Tongue movement")]
        [SerializeField] private TransformSpringComponent tongueSpringComponent;
        [SerializeField] private float minTimeBetweenTonguePunches = 0.5f, maxTimeBetweenTonguePunches = 1.5f;
        [SerializeField] private float tonguePunchVelocity = 1f;

        private float timeUntilNextTonguePunch;
        private float seedY, seedZ, currentTime;
        private bool isRotating;

        private void Start()
        {
            timeUntilNextTonguePunch = Random.Range(minTimeBetweenTonguePunches, maxTimeBetweenTonguePunches);
            seedY = Random.Range(0f, 100f);
            seedZ = Random.Range(0f, 100f);
            isRotating = true;
        }
        
        private void Update()
        {
            SnakeAutoRotate();
            TongueMovement();
        }

        //We automatically and randomly rotate the snake. The snake's skeleton will then react in an interesting way
        private void SnakeAutoRotate()
        {
            if(isRotating) currentTime += Time.deltaTime;
            float time = currentTime * timeScale;

            float yRotation = (Mathf.PerlinNoise(seedY, time) * 2f - 1f) * noiseScale;
            float zRotation = (Mathf.PerlinNoise(seedZ, time) * 2f - 1f) * noiseScale;
            
            Vector3 rotationVector = new Vector3(0f, yRotation, zRotation);
            transform.Rotate(rotationVector * (rotationSpeed * Time.deltaTime * (isRotating ? 1f : 0f)));
        }
        
        //The tongue will also move automatically by punching a the position every so often
        private void TongueMovement()
        {
            if(timeUntilNextTonguePunch <= 0f)
            {
                tongueSpringComponent.AddVelocityPosition(-Vector3.up * tonguePunchVelocity);
                timeUntilNextTonguePunch = Random.Range(minTimeBetweenTonguePunches, maxTimeBetweenTonguePunches);
            }
            else
            {
                timeUntilNextTonguePunch -= Time.deltaTime;
            }
        }
        
        public void ToggleSnakeRotation()
        {
            isRotating = !isRotating;
        }
    }
}