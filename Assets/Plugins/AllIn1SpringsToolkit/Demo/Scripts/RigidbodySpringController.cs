using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class RigidbodySpringController : DemoElement
    {
        //This script is a simple example of how to use the RigidbodySpringComponent
        //The RigidbodySpringComponent is similar to the TransformSpringComponent but it works with Rigidbodies
        [Space, Header("Rigidbody Spring")]
        [SerializeField] private RigidbodySpringComponent rigidbodySpring;
        [SerializeField] private float positionPunchForce, rotationPunchForce, initialWaitTime;
        [SerializeField] private Transform springTargetT;
        
        //The ColorSpringComponent is used to change the color of the object when we interact with it
        [Space, Header("Color Spring")]
        [SerializeField] private ColorSpringComponent colorSpring;
        [SerializeField] private Color punchColor;

        //The balls are spawned from the top of the screen and fall down. They are pooled and reused
        [Space, Header("Balls")]
        [SerializeField] private bool spawnIsOn;
        [SerializeField] private Transform ballSpawnPointT;
        [SerializeField] private Transform[] balls;
        [SerializeField] private MeshRenderer[] ballMeshRenderers;
        [SerializeField] private Rigidbody[] ballRigidbodies;
        [SerializeField] private Color[] ballColors;
        [SerializeField] private float spawnTime, spawnRadius, heightForDeactivation;

        private Coroutine spawnCoroutine;
        private int currentBallIndex;

        //Initial setup disabling all the balls and starting the spawn coroutine that will loop and spawn balls until stopped
        private void Start()
        {
            currentBallIndex = 0;
            rigidbodySpring.enabled = false;
            springTargetT.SetParent(transform);
            
            for(int i = 0; i < balls.Length; i++)
            {
                balls[i].gameObject.SetActive(false);
            }

            spawnCoroutine = StartCoroutine(SpawnBallsCR());
        }

        //Spawn balls forever with a delay between each spawn
        private IEnumerator SpawnBallsCR()
        {
            yield return new WaitForSeconds(initialWaitTime);
            //We enable the spring after the initial wait time, we do this to avoid movement while the demo is transitioning into the scene
            rigidbodySpring.enabled = true;
            
            while(true)
            {
                SpawnBall();
                yield return new WaitForSeconds(spawnTime);
                CheckHeightForDeactivation();
            }
        }
        
        //We punch the rigidbody position spring upwards and it will move while also interacting with other rigidbodies
        //We set the color spring to the punch color immediately and it will slowly go back to the original color
        public void PositionPunch()
        {
            rigidbodySpring.AddVelocityPosition(Vector3.up * positionPunchForce);
            colorSpring.SetCurrentValue(punchColor);
        }

        //We punch the rigidbody rotation randomly. While doing that it will interact with other rigidbodies
        //We set the color spring to the punch color immediately and it will slowly go back to the original color
        public void RotationPunch()
        {
            float randomSign = Random.Range(0, 2) == 0 ? -1 : 1;
            Vector3 randomRotationAxis = Random.Range(0, 2) == 0 ? Vector3.forward : Vector3.right;
            rigidbodySpring.AddVelocityRotation(randomSign * randomRotationAxis * rotationPunchForce);
            colorSpring.SetCurrentValue(punchColor);
        }

        //From this point on we handle the ball spawning and deactivation. Not much to see here
        
        private void SpawnBall()
        {
            if(!spawnIsOn)
            {
                return;
            }
            
            int startIndex = currentBallIndex;
            while (balls[currentBallIndex].gameObject.activeInHierarchy)
            {
                currentBallIndex = (currentBallIndex + 1) % balls.Length;
        
                if (currentBallIndex == startIndex)
                {
                    return;
                }
            }
            
            Vector3 randomPosition = ballSpawnPointT.position + Random.insideUnitSphere * spawnRadius;
            balls[currentBallIndex].position = randomPosition;
            int randomColorIndex = Random.Range(0, ballColors.Length);
            ballMeshRenderers[currentBallIndex].material.color = ballColors[randomColorIndex];
            ballRigidbodies[currentBallIndex].linearVelocity = Vector3.zero;
            balls[currentBallIndex].gameObject.SetActive(true);
            currentBallIndex = (currentBallIndex + 1) % (balls.Length - 1);
        }
        
        private void CheckHeightForDeactivation()
        {
            for(int i = 0; i < balls.Length; i++)
            {
                if(balls[i].position.y < heightForDeactivation)
                {
                    balls[i].gameObject.SetActive(false);
                    continue;
                }

                if(balls[i].position.y > ballSpawnPointT.position.y)
                {
                    balls[i].gameObject.SetActive(false);
                }
            }
        }

        public override void Close()
        {
            base.Close();
            rigidbodySpring.enabled = false;
            if(spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
            }
        }

#if UNITY_EDITOR
        //Way for the demo to get the references of the balls from the editor without having to do it manually
        [ContextMenu("Get Ball References")]
        private void GetBallReferences()
        {
            int childCount = ballSpawnPointT.childCount;
            balls = new Transform[childCount];
            ballMeshRenderers = new MeshRenderer[childCount];
            ballRigidbodies = new Rigidbody[childCount];
            for(int i = 0; i < ballSpawnPointT.childCount; i++)
            {
                balls[i] = ballSpawnPointT.GetChild(i);
                ballMeshRenderers[i] = balls[i].GetComponent<MeshRenderer>();
                ballRigidbodies[i] = balls[i].GetComponent<Rigidbody>();
            }
            EditorUtility.SetDirty(this);
        }
#endif
    }
}