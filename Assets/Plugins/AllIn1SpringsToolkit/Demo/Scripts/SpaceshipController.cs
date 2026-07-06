using UnityEngine;
using Random = UnityEngine.Random;
using AllIn1SpringsToolkit.Bones;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class SpaceshipController : DemoElement
    {
		//The objective of this demo is to use a FloatSpringComponent to make the spaceship hover over some rocks
		//Additionally, we use a TransformSpringComponent to make the spaceship rotate and shake when the player presses a button
		//We also use a ShaderFloatSpringComponent to change the color when shaking
		//But the important part is how we can also use springs to drive custom game logic, just like the spaceship hover
		[Space, Header("Spaceship")]
		[SerializeField] private SpringSkeleton spaceshipSkeleton;
        [SerializeField] private TransformSpringComponent spaceshipTransformSpring;
        [SerializeField] private float rotationPunchStrength;
        [SerializeField] private Transform spaceshipTransform, parentForwardTransform;
        [SerializeField] private FloatSpringComponent hoverFloatSpring;
        [SerializeField] private Vector3SpringComponent hoverForwardSpring;
        [SerializeField] private ShaderFloatSpringComponent shaderFloatSpring;
        [SerializeField] private float onShakeFloatSpringPunch;
        [SerializeField] private CamFovOrSizeSpringComponent camFovSpringComponent;
        [SerializeField] private float onShakeCamFovSpringPunch;
        [SerializeField] private float onShakeScaleSpringPunch;
        [SerializeField] private float desiredHoverDistance, maxHoverDistance;

        //The spaceship will hover over the rocks, that are pooled and will be activated when needed
        //Rocks scaling are also driven by springs
        [Space, Header("Rock Spawning")]
        [SerializeField] private Transform spawnOrigin;
        [SerializeField] private SpaceshipRockMove[] rockSprings;
        [SerializeField] private float spawnWidth;
        [SerializeField] private float spawnMinTime, spawnMaxTime;
        [SerializeField] private float rockOffScreenMoveSpeed;
        
        private float timeUntilNextSpawn;
        private int currentRockIndex;

		public override void Initialize(bool hideUi)
        {
            base.Initialize(hideUi);

			GetRandomNextSpawnTime();
			foreach (SpaceshipRockMove rockSpring in rockSprings)
            {
                rockSpring.gameObject.SetActive(false);
            }
            
            //Have a few rocks since the beginning
            int randomRockCount = Random.Range(5, 10);
            for (int i = 0; i < randomRockCount; i++)
            {
                SpawnRock(Random.Range(0f, 20f));
            }

			spaceshipTransformSpring.Initialize();
			spaceshipSkeleton.Initialize();
			StartCoroutine(EnableSkeletonAfterWait());
		}

		private IEnumerator EnableSkeletonAfterWait()
		{
			spaceshipSkeleton.SetSkeletonEnabled(false);

			yield return new WaitForSeconds(WAIT_TIME);

			spaceshipSkeleton.SetSkeletonEnabled(true);
		}

		private void Update()
        {
			if (isOpen)
            {
                SpaceshipSpringsUpdate();
                SpawnRocksUpdate();
            }
            else
            {
                //If not open we don't update. And instead we move the rocks out of the way when the demo is transitioning out
                //This way we prevent them from showing temporarily from offscreen
                Vector3 moveRockDirection = transform.position.x > 0f ? Vector3.left : Vector3.right;
                spawnOrigin.localPosition += moveRockDirection * (rockOffScreenMoveSpeed * Time.deltaTime);
            }
        }

        private void SpaceshipSpringsUpdate()
        {
            //Raycast down to find the ground so that we can set the float spring target and find the hit normal
            Vector3 hitNormal = Vector3.up;
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 100f))
            {
                float targetHoverDistance = Mathf.Min(maxHoverDistance, hit.point.y + desiredHoverDistance);
                hoverFloatSpring.SetTarget(targetHoverDistance);
                hitNormal = hit.normal;
            }
    
            //Position the hovercraft at the float spring value. It makes the hovercraft float
            Vector3 newHoverCraftPosition = spaceshipTransform.position;
            newHoverCraftPosition.y = hoverFloatSpring.GetCurrentValue();
            spaceshipTransform.position = newHoverCraftPosition;
        
            //Rotate the hovercraft to match the hitNormal with a spring
            hoverForwardSpring.SetTarget(Vector3.Cross(parentForwardTransform.right, hitNormal));
            Vector3 forwardDirection = hoverForwardSpring.GetCurrentValue();
            if (forwardDirection != Vector3.zero)
            {
                spaceshipTransform.forward = forwardDirection;
            }
        }
        
        //This method is called by the button to shake the spaceship
        public void ShakeRotation()
        {
            //We add velocity to rotation spring to make it shake from side to side. We also add velocity to scale spring to make it grow and shrink
            float randomRotationPunch = Random.value > 0.5f ? rotationPunchStrength : -rotationPunchStrength;
            spaceshipTransformSpring.AddVelocityRotation(new Vector3(0f, 0f, randomRotationPunch));
            spaceshipTransformSpring.AddVelocityScale(Vector3.one * onShakeScaleSpringPunch);
            
            //Here we change the color of the spaceship when shaking
            shaderFloatSpring.AddVelocity(onShakeFloatSpringPunch);
            
            //Here we do a camera FOV kick
            camFovSpringComponent.AddVelocity(onShakeCamFovSpringPunch);
        }
        
        //From this point on, the code is only for the rock spawning

        private void SpawnRocksUpdate()
        {
            if(timeUntilNextSpawn <= 0)
            {
                SpawnRock();
                GetRandomNextSpawnTime();
            }
            else
            {
                timeUntilNextSpawn -= Time.deltaTime;
            }
        }

        private void SpawnRock(float zOffset = 0f)
        {
            Vector3 randomOffset = new Vector3(Random.Range(-spawnWidth, spawnWidth), 0f, 0f);
            randomOffset = spawnOrigin.TransformDirection(randomOffset);
            Vector3 spawnPosition = spawnOrigin.position + randomOffset;
            
            int startIndex = currentRockIndex;
            while (rockSprings[currentRockIndex].gameObject.activeInHierarchy)
            {
                currentRockIndex = (currentRockIndex + 1) % rockSprings.Length;
        
                if (currentRockIndex == startIndex)
                {
                    return;
                }
            }
            SpaceshipRockMove spaceshipRockMove = rockSprings[currentRockIndex];
            
            spawnPosition += spawnOrigin.forward * -zOffset;
            spaceshipRockMove.Initialize(spawnPosition);
        }
        
        private void GetRandomNextSpawnTime()
        {
            timeUntilNextSpawn = Random.Range(spawnMinTime, spawnMaxTime);
        }
        
#if UNITY_EDITOR
        [ContextMenu("Get Spaceship References")]
        private void GetSpaceshipReferences()
        {
            int childCount = spawnOrigin.childCount;
            rockSprings = new SpaceshipRockMove[childCount];
            for(int i = 0; i < childCount; i++)
            {
                rockSprings[i] = spawnOrigin.GetChild(i).GetComponent<SpaceshipRockMove>();
            }
            
            EditorUtility.SetDirty(this);
        }
#endif
    }
}