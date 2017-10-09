/*
* This stealth plugin tooklit was made by:
* Joshua Tanner
* Dylan Meissner
* Zac Watson
* Media Design School 2017
*/


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using PathBuilder;

namespace StealthPlugin
{
    public enum StealthState { CHASE, PATROL, SEARCH };       

    [RequireComponent(typeof(NavMeshAgent))]
    public class Stealth : MonoBehaviour
    {
        /*-------STEALTH PROPERTIES------*/

        //for player tracking
        [Header("Player settings")]
        [Tooltip("Object with tag 'Player' will be found automatically")]
        public GameObject player;
        private PlayerDetection playerDetection;
        private Vector3 directionToPlayer;
        [Tooltip("If the NPC has reached the player while in chase state")]
        public bool playerCaught;
        [Tooltip("Offset head position of NPC for vision and hearing checks")]
        public Transform headTransform;

        // Member variables for detecting percentages.      
        private float percentageVisible = 0.0f;
        Transform[] playerComponents;

        //for detection 
        [Header("Detection settings")]
        [Range(0.0f, 100.0f)]
        [Tooltip("Furthest distance the NPC can see")]
        public float visionDistance = 50.0f;

        [Range(0.0f, 5.0f)]
        [Tooltip("How fast the NPC can recognise the player")]
        public float visualPerception = 1.0f; 

        [Range(0.0f, 1000.0f)]
        [Tooltip("Furthest distance the NPC can hear")]
        public float audialDistance = 5.0f;

        [Range(0.0f, 1000.0f)]
        [Tooltip("How far away the NPC will alert other NPCs when chasing the player")]
        public float allyAlertRange = 5.0f;      

        [Range(0.0f, 1.0f)]
        [Tooltip("How fast the NPC detection level decreases")]
        public float detectionDecreaseRate = 1.0f;

        [Range(0.0f, 1.0f)]
        [Tooltip("The level at which the NPC will switch from suspicious to alert")]
        public float suspicionThreshold;

        [Header("Field of view")]
        [Tooltip("Field of view of the NPC in the patrol state")]
        [Range(0.0f, 360.0f)]
        public float patrolFOV = 70.0f;
        [Tooltip("Field of view of the NPC in the chase state")]
        [Range(0.0f, 360.0f)]      
        public float chaseFOV = 90.0f;
        [Tooltip("Field of view of the NPC in the search state")]
        [Range(0.0f, 3600.0f)]
        public float searchFOV = 90.0f;
        private float currentFOV = 70.0f;

        [Header("Speed")]
        [Tooltip("Speed the NPC will travel at while following patrol path")]
        public float patrolSpeed = 2.0f;
        [Tooltip("Speed the NPC will travel at while chasing player")]
        public float chaseSpeed = 2.0f;
        [Tooltip("Speed the NPC will travel at while searching for player")]
        public float searchSpeed = 2.0f;
         
        private float detectionLevel;
        private DetectionMeter detection;

        //for NPC state              
        private StealthState  currentState;

        //for pathfinding
        private NavMeshAgent navigator;
        [Tooltip("The parent of the NPC's path points")]
        public GameObject pathParent;
        private PathController pathController;
        private PathPoint currentPathPoint;
        private Vector3 searchPosition;        
        private static Vector3 lastKnownPlayerPosition;
        private Vector3 lastHeardSoundPos = Vector3.zero;       
        private int direction = 1; //used for ping pong      
    
        [Header("Searching settings")]  
        [Tooltip("Speed the NPC will rotate at while searching for player")]
        public float rotateSpeed = 30.0f;
        [Tooltip("Sweep angle the NPC will rotate while searching for player")]
        public float searchAngle = 90.0f;
        private Vector3 beginSearchRotation;
        private bool clockWiseDirection;
        private bool reachedSearchPoint = false;
        //a delay so NPC doesn't immediately leave search
        private const float minimumSearchTime = 3.0f;   
  
        //for moving through list of points
        private int currentListIndex;
        private float currentWaitingTime;    

        //checks for NPC ally shout notifying
        private const float timeBetweenShouts = 3.0f;
        float timeToNextShout = timeBetweenShouts;

        //stealth manager
        [Tooltip("The stealth manager in the scene")]
        public StealthManager manager;

        //gizmo toggle
        [Tooltip("Toggle gizmos drawing")]
        public bool drawGizmos;     

        /*-------STEALTH PROPERTIES------*/

        private void Awake()
        {
            //keep the reference to pathcontroller between play/edit
            if (GetComponentInChildren<PathController>())
                pathParent = GetComponentInChildren<PathController>().gameObject;

            DetectionLevel = 0.0f;

            if (manager == null)
            {
                manager = GameObject.FindObjectOfType<StealthManager>();
                if (manager == null)
                {
                    Debug.LogError("No stealth manager in scnene");
                }
            }           
        }

        private void Start()
        {
            //set components          
            navigator = GetComponent<NavMeshAgent>();
            if(GetComponent<DetectionMeter>())
                detection = GetComponent<DetectionMeter>();

            if(pathParent && pathParent.GetComponent<PathController>())
                pathController = pathParent.GetComponent<PathController>();

            //set search pos
            lastKnownPlayerPosition = Vector3.zero;
            SearchPosition = Vector3.zero;
            
            //set FOV
            CurrentFOV = patrolFOV;

            //set current point to first point
            if (pathController && pathController.listPoints.Count > 0)
            {
                currentPathPoint = pathController.listPoints[0];
            }
            currentListIndex = 0;

            //find the player
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    player = GameObject.FindGameObjectWithTag("Player");
                    if (player.transform.root.gameObject.tag == "Player")
                    {
                        player = player.transform.root.gameObject;
                    }
                }
            }

            //check if player has detection component
            if (player.GetComponent<PlayerDetection>())
            {
                playerDetection = player.GetComponent<PlayerDetection>();
                playerComponents = playerDetection.components;
            }

            if(headTransform == null)
            {
                headTransform = transform;
            }

            //set state
            currentState = StealthState.PATROL;
            EnterPatrol();        
        }

        #region GETSET

        public void SetSpeed(float speed)
        {
            navigator.speed = speed;
        }

        public void SetWaitingTime(float time)
        {
            CurrentWaitingTime = 0.0f;
        }

        public void SetDestination(Vector3 destination)
        {
            navigator.destination = destination;
        }

        public PathController GetPathController()
        {
            return pathController;
        }

        public Vector3 GetCurrentPathPosition()
        {
            return pathController.listPoints[currentListIndex].position;
        }

        public float GetCurrentPathWait()
        {
            return pathController.listPoints[currentListIndex].wait;
        }

        public void SetNextPointIndex()
        {
            if (pathController.pathType == PathController.PathType.LOOP)
            {
                direction = 1;
            }

            currentListIndex = (currentListIndex + direction) % pathController.listPoints.Count;
            
            if (pathController.pathType == PathController.PathType.PINGPONG)
            {             
                //check for direction reverse
                if(currentListIndex == 0 || currentListIndex == pathController.listPoints.Count - 1)
                {                   
                    direction *= -1;
                }
            }            
        }

        public bool GetDestinationReached()
        {
            if(Vector3.Distance(transform.position, SearchPosition) < 2.5f)
            {
                return true;
            }

            return false;
        }

        public void SetState(StealthState state)
        {
            switch (currentState)
            {
                case StealthState.CHASE:
                    ExitChase();
                    break;
                case StealthState.PATROL:
                    ExitPatrol();
                    break;
                case StealthState.SEARCH:
                    ExitSearch();
                    break;
            }

            currentState = state;

            switch (state)
            {
                case StealthState.CHASE:
                    EnterChase();
                    break;
                case StealthState.PATROL:
                    EnterPatrol();
                    break;
                case StealthState.SEARCH:
                    EnterSearch();
                    break;
            }
        }

        // getter for current state
        public StealthState GetCurrentState()
        {
            return currentState;
        }

        public float CurrentWaitingTime { get => currentWaitingTime; set => currentWaitingTime = value; }
        public float DetectionLevel { get => detectionLevel; set => detectionLevel = value; }
        public float CurrentFOV { get => currentFOV; set => currentFOV = value; }
        public Vector3 SearchPosition { get => searchPosition; set => searchPosition = value; }
        public Vector3 LastHeardSoundPos { get => lastHeardSoundPos; set => lastHeardSoundPos = value; }

        #endregion


        private void OnCollisionEnter(Collision collision)
        {
            if(collision.transform.tag == "Player")
            {
                //search for player if player colliders with NPC
                searchPosition = player.transform.position;
                SetState(StealthState.SEARCH);
            }
        }         


        #region StateHandling

        private void EnterPatrol()
        {      
            currentFOV = patrolFOV;
            navigator.speed = patrolSpeed;
            currentWaitingTime = 0.0f;
        }
        private void UpdatePatrol()
        {
            //move through path points
            if (GetPathController() && GetPathController().listPoints.Count > 0)
            {
                SetDestination(GetCurrentPathPosition());

                //Check if destination reached
                if (Vector3.Distance(transform.position, GetCurrentPathPosition()) < 1.5f)
                {
                    if (CurrentWaitingTime >= GetCurrentPathWait())
                    {
                        SetNextPointIndex();
                        currentWaitingTime = 0.0f;
                    }
                    else
                    {
                        currentWaitingTime += Time.deltaTime;
                        gameObject.transform.forward = Vector3.Lerp(gameObject.transform.forward,
                            Quaternion.AngleAxis(pathController.listPoints[currentListIndex].rotation, Vector3.up) * Vector3.forward, 2.0f * Time.deltaTime);
                    }
                }
            }

            //null check
            float disguiseValue = playerDetection == null ? 1.0f : playerDetection.currentDisguise.detectionModifier;

            //chase the player
            if (detectionLevel >= 1.0f && percentageVisible > 0)
            {
                SetState(StealthState.CHASE);
            }
            else if((disguiseValue == 1 && detectionLevel > suspicionThreshold) || (disguiseValue < 1.0f && detectionLevel == 1.0f))
            {              
                searchPosition = lastKnownPlayerPosition;
                SetState(StealthState.SEARCH);
            }
        }
        private void ExitPatrol()
        {

        }

        private void EnterChase()
        {        
            //alert nearby guards
            if (allyAlertRange > 0)
                StealthManager.AlertNearbyGuards(this);

            timeToNextShout = timeBetweenShouts;
            CurrentWaitingTime = 0.0f;  
            navigator.speed = chaseSpeed;
            CurrentFOV = chaseFOV;          
        }
        private void UpdateChase()
        {
            //alert buddies
            timeToNextShout -= Time.deltaTime;
            if(timeToNextShout <= 0.0f)
            {
                if (allyAlertRange > 0)
                    StealthManager.AlertNearbyGuards(this);
                timeToNextShout = timeBetweenShouts;
            }

            //check if player caught
            if(Vector3.Distance(transform.position, player.transform.position) < 1.5f)
            {
                navigator.speed = 0;
                playerCaught = true;
            }
            else
            {
                navigator.speed = chaseSpeed;
                playerCaught = false;
            }
           
            //chase the player
            if (percentageVisible > 0.0)
            {
                navigator.destination = player.transform.position;
            }
            else //change to search state
            {
                SearchPosition = lastKnownPlayerPosition;
                SetState(StealthState.SEARCH);
            }
        }
        private void ExitChase()
        {

        }

        private void EnterSearch()
        {     
            CurrentWaitingTime = 0.0f;
            currentFOV = searchFOV;
            navigator.speed = searchSpeed;
            navigator.destination = SearchPosition;
            reachedSearchPoint = false;
        }
        private void UpdateSearch()
        {
            CurrentWaitingTime += Time.deltaTime;

            //null check
            float disguiseValue = playerDetection == null ? 1.0f : playerDetection.currentDisguise.detectionModifier;

            // chase the player
            if (percentageVisible > 0.0f && disguiseValue >= 0.1f)
            {
                StartCoroutine(quickDetectionFill());
                SetState(StealthState.CHASE);
            }
            else if (DetectionLevel < Mathf.Epsilon && CurrentWaitingTime > minimumSearchTime)
            {
                // go back to patrol     
                SetState(StealthState.PATROL);
            }
            else if (GetDestinationReached())
            {
                if (!reachedSearchPoint)
                {
                    reachedSearchPoint = true;
                    beginSearchRotation = transform.forward;
                }

                // Perform sweep search    
                if (clockWiseDirection)
                {
                    transform.RotateAround(transform.position, transform.up, Time.deltaTime * rotateSpeed);

                    if (Vector3.Angle(beginSearchRotation, transform.forward) > searchAngle)
                    {
                        clockWiseDirection = !clockWiseDirection;
                    }
                }
                else
                {
                    transform.RotateAround(transform.position, transform.up, Time.deltaTime * -rotateSpeed);

                    if (Vector3.Angle(beginSearchRotation, transform.forward) > searchAngle)
                    {
                        clockWiseDirection = !clockWiseDirection;
                    }
                }
            }
        }
        private void ExitSearch()
        {
           
        }

        #endregion
        
        private void UpdateState()
        {
            //Check percentage of player in view for every state
            UpdatePlayerView();

            switch (currentState)
            {
                case StealthState.CHASE:
                    UpdateChase();
                    break;
                case StealthState.PATROL:
                    UpdatePatrol();
                    break;
                case StealthState.SEARCH:
                    UpdateSearch();
                    break;
            }
        }
            
        private void Update()
        {
            if (Application.isPlaying)
            {
                UpdateState();           
            }
        }

        #region VisionChecking

        //percentage visible check
        private float CheckPlayerComponents()
        {
            percentageVisible = 0.0f;

            if (Vector3.Distance(headTransform.position, player.transform.position) > visionDistance)
            {
                return 0.0f;
            }

            if (playerDetection && playerComponents.Length > 0)
            {
                for (int i = 0; i < playerComponents.Length; i++)
                {
                    directionToPlayer = playerComponents[i].position - transform.position;

                    //Check if angle is too large
                    float angle = Mathf.Abs(Vector3.Angle(headTransform.forward, directionToPlayer));
                    if (angle < CurrentFOV / (2.0f))
                    {
                        Vector3 componentDirection = playerComponents[i].position - headTransform.position;
                        RaycastHit[] hits = Physics.RaycastAll(headTransform.position, componentDirection, componentDirection.magnitude);

                        bool hitOtherObject = false;

                        foreach (RaycastHit hit in hits)
                        {
                            if (hit.collider.tag != manager.transparencyTag &&
                                hit.collider.tag != manager.soundBlockTransparencyTag)
                            {
                                if (hit.collider.tag != "Player")
                                {
                                    hitOtherObject = true;
                                    break;
                                }
                            }
                        }

                        if (!hitOtherObject)
                            percentageVisible += 1.0f;

                    }
                }            

                return percentageVisible /= playerComponents.Length;
            }
            else if (CheckPlayerInView())
            {
                //default detection value      
                percentageVisible = 1.0f;
                return percentageVisible;
            }
            else
            {
                return 0.0f;
            }
        }

        //Check if player is in view (not a percetange)
        private bool CheckPlayerInView()
        {
            directionToPlayer = player.transform.position - transform.position;
      
            //Check if angle is too large
            float angle = Mathf.Abs(Vector3.Angle(transform.forward, directionToPlayer));
            if (angle > CurrentFOV / 2.0f)
            {
                return false;
            }
            else
            {
                RaycastHit[] hits = Physics.RaycastAll(headTransform.position, directionToPlayer, directionToPlayer.magnitude);

                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.tag == "Player")
                    {
                        return true;
                    }
                    else if (hit.collider.tag == manager.transparencyTag)
                    {
                        continue;
                    }
                    else
                    {
                        return false;
                    }
                }              
              
            }

            return false;
        }

        //calls the view checks 
        public void UpdatePlayerView()
        {
            //detection fills slower, the further away the enemy is
            float distScale = 1.0f - (Vector3.Distance(gameObject.transform.position, player.transform.position) / visionDistance);
            if (distScale < 0.0f)
                distScale = 0.0f;

            if (CheckPlayerComponents() > 0.0f)
            {
                lastKnownPlayerPosition = player.transform.position;

                if (playerDetection)
                {
                    DetectionLevel += (playerDetection.currentDisguise.detectionModifier / 2) * percentageVisible * distScale * visualPerception * Time.deltaTime;
                }
                else
                {
                    DetectionLevel += percentageVisible * distScale * visualPerception * Time.deltaTime;
                }

                if (DetectionLevel > 1.0f)
                    DetectionLevel = 1.0f;
            }

            else if (DetectionLevel > 0.0f)
            {
                DetectionLevel -= detectionDecreaseRate * Time.deltaTime;             
            }

            if (detection && detection.IsMeterSetup())
            {
                detection.SetFill(DetectionLevel, suspicionThreshold);
            }


        }

        //quicky fill up the meter when the NPC becomes alert
        IEnumerator quickDetectionFill()
        {
            while (DetectionLevel < 1.0f)
            {
                if(!playerDetection)
                {
                    DetectionLevel = Mathf.Lerp(DetectionLevel, 1.0f, 2.0f * visualPerception * Time.deltaTime);
                }
                else
                {
                    DetectionLevel = Mathf.Lerp(DetectionLevel, 1.0f, 2.0f * visualPerception * playerDetection.currentDisguise.detectionModifier * Time.deltaTime);
                }
                yield return 1;
            }
        }

        #endregion

        //helpful gizmos
        void OnDrawGizmos()
        {
            if (drawGizmos)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(gameObject.transform.position, audialDistance);

                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(gameObject.transform.position, allyAlertRange);

                Vector3 leftVision = Quaternion.AngleAxis(Application.isPlaying ? -CurrentFOV / 2.0f : -patrolFOV / 2.0f, transform.up) * transform.forward;
                Vector3 rightVision = Quaternion.AngleAxis(Application.isPlaying ? CurrentFOV / 2.0f : patrolFOV / 2.0f, transform.up) * transform.forward;
                Gizmos.DrawLine(headTransform.position, headTransform.position + leftVision * visionDistance);
                Gizmos.DrawLine(headTransform.position, headTransform.position + rightVision * visionDistance);
            }
        }

        
        }


    //for advanced vision detection and disguise selecting
    public class PlayerDetection : MonoBehaviour
    {    
        [Header("Advanced player detection")]
        [Tooltip("Specify the body parts of the player for more accurate vision detection")]
        public Transform[] components;
    
        [Tooltip("This is the currently equipped disguise.")]
        public Disguise currentDisguise;              

        public void Start()
        {          
            if(currentDisguise.name == "")
                currentDisguise.detectionModifier = 1.0f;
        }     

    }

    //disguise struct
    [System.Serializable]
    public struct Disguise
    {
        public string name;
        [Range(0.0f, 1.0f)]
        public float detectionModifier;
    }

    // handles NPC buddy alerts
    // handles sound creation
    // handles application and removal of disguises
    public class StealthManager : MonoBehaviour
    {      
        [Header("Disguise system")]
        [Tooltip("Enter Accepted Disguises:")]
        public Disguise[] disguises;          

        public string transparencyTag;
        public string soundBlockTag;
        public string soundBlockTransparencyTag;

        static List<Stealth> stealthList;

        // manager instance
        private static StealthManager instance = null;

        public static StealthManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType(typeof(StealthManager)) as StealthManager;
                }

                // If it is still null, create a new instance
                if (instance == null)
                {
                    GameObject newStealthManager = new GameObject("Stealth Manager");
                    instance = newStealthManager.AddComponent(typeof(StealthManager)) as StealthManager;
                }

                return instance;
            }
        }

        private void Start()
        {          
            stealthList = new List<Stealth>();

            Stealth[] stealthArray = FindObjectsOfType<Stealth>();

            foreach (Stealth stealth in stealthArray)
            {
                stealthList.Add(stealth);
            }
        }

        public void CreateSoundOneShot(Vector3 soundPosition, float volumePriority, bool penetratesWall)
        {
            foreach (Stealth stealth in stealthList)
            {
                // update the sound search location if the Ai is searching or patrolling and not if he is chasing
                if (stealth.GetCurrentState() == StealthState.PATROL || stealth.GetCurrentState() == StealthState.SEARCH)
                {
                    // assign the enemy object
                    GameObject enemy = stealth.gameObject;

                    // calculate the distance between the enemy and the sound
                    float distance = Vector3.Distance(stealth.headTransform.position, soundPosition);

                    // if the distance is smaller than the audial radius then the sound can be detected else ignore the sound
                    if (distance < stealth.audialDistance)
                    {
                        // within range so Raycast to the position and check whether the volume is enough to penetrate walls hit
                        RaycastHit[] hits;
                        hits = Physics.RaycastAll(stealth.headTransform.position, soundPosition - stealth.headTransform.position, distance);

                        bool soundDetected = false;

                        foreach (RaycastHit hit in hits)
                        {
                            if (hit.transform.tag == soundBlockTag ||
                                hit.transform.tag == soundBlockTransparencyTag)
                            {
                                return;
                            }
                        }

                        // No objects between player and enemy
                        if (hits.Length == 1 && hits[0].transform.tag == "Player")
                        {
                            // Alert Ai                   
                            soundDetected = true;
                        }

                        // if the sound is blocked by 1 wall and the sound can penetrate walls
                        else if (hits.Length == 2 && penetratesWall)
                        {
                            // Alert Ai                 
                            soundDetected = true;
                        }

                        // otherwise calculate the distance around corners between the enemy and the sound
                        else
                        {
                            float pathDist = CalculatePathLength(soundPosition, enemy);

                            // if the path distance is less than the occlusion distance of the enemy
                            if (pathDist <= stealth.audialDistance)
                            {
                                soundDetected = true;
                            }
                        }
                        // set the position and search bool to true
                        if (soundDetected)
                        {
                            stealth.LastHeardSoundPos = soundPosition;
                            stealth.SearchPosition = soundPosition;
                            stealth.SetState(StealthState.SEARCH);
                        }
                    }

                }
            }
        }

        public void CreatePlayerStepSound(Vector3 playerPosition, bool penetratesWall)
        {
            foreach (Stealth stealth in stealthList)
            {
                // update the sound search location if the Ai is searching or patrolling and not if he is chasing
                if (stealth.GetCurrentState() == StealthState.PATROL || stealth.GetCurrentState() == StealthState.SEARCH)
                {
                    // calculate the distance between the enemy and the sound
                    float distance = Vector3.Distance(stealth.headTransform.position, playerPosition);

                    // if the distance is smaller than the audial radius then the sound can be detected else ignore the sound
                    if (distance < stealth.audialDistance)
                    {
                        RaycastHit[] hits;
                        hits = Physics.RaycastAll(stealth.headTransform.position, playerPosition - stealth.headTransform.position, distance);

                        bool soundDetected = false;

                        // ignore the sound
                        foreach (RaycastHit hit in hits)
                        {
                            if (hit.transform.tag == soundBlockTag ||
                                hit.transform.tag == soundBlockTransparencyTag)
                            {
                                return;
                            }
                        }                        

                        // No objects between player and enemy
                        if (hits.Length == 1 && hits[0].transform.tag == "Player")
                        {
                            // Alert Ai                      
                            soundDetected = true;
                        }

                        // if the sound is blocked by 1 wall and the sound can penetrate walls
                        else if (hits.Length == 2 && penetratesWall)
                        {
                            // Alert Ai                      
                            soundDetected = true;
                        }

                        // set the position and search bool to true
                        if (soundDetected)
                        {
                            stealth.LastHeardSoundPos = playerPosition;
                            stealth.SearchPosition = playerPosition;
                            stealth.SetState(StealthState.SEARCH);
                        }
                    }
                }
            }
        }

        // used for advanced sound occlusion 
        private float CalculatePathLength(Vector3 targetPos, GameObject Enemy)
        {
            float distance = 0.0f;
            NavMeshAgent navigator = Enemy.GetComponent<NavMeshAgent>();

            NavMeshPath path = new NavMeshPath();

            if (navigator.enabled)
            {
                navigator.CalculatePath(targetPos, path);
            }

            Vector3[] allWayPoints = new Vector3[path.corners.Length + 2]; // plus 2 because of enemy and player positions

            // set the enemy and player positions in the array
            allWayPoints[0] = Enemy.transform.position;
            allWayPoints[allWayPoints.Length - 1] = targetPos;

            // assign each corner along the path as a way point
            for (int i = 0; i < path.corners.Length; i++)
            {
                allWayPoints[i + 1] = path.corners[i];
            }

            // add the distances between each waypoint
            for (int i = 0; i < allWayPoints.Length - 1; i++)
            {
                distance += Vector3.Distance(allWayPoints[i], allWayPoints[i + 1]);
            }

            return distance;
        }

        // apply disguise to the player
        public void ApplyDisguise(PlayerDetection player, string disguiseName)
        {         
            foreach (Disguise disguise in disguises)
            {
                if(disguise.name == disguiseName)
                {
                    //apply this disguise
                    player.currentDisguise = disguise;                   
                    return;
                }
            }

            Debug.LogError(disguiseName + " is not a valid disguise name");          
        }

        // remove the player's disguise
        public void RemoveDisguise(PlayerDetection player)
        {
            //remove
            player.currentDisguise.name = "";
            player.currentDisguise.detectionModifier = 1;  
        }

        //Register enemies that are created after awake
        public static void RegisterEnemy(Stealth stealth)
        {
            if (!stealthList.Contains(stealth))
            {
                stealthList.Add(stealth);
            }
        }

        //Unregister enemies that are destroyed after awake
        public static void UnregisterEnemy(Stealth stealth)
        {
            if (stealthList.Contains(stealth))
            {
                stealthList.Remove(stealth);
            }
        }

        // let guard alert nearby guards
        public static void AlertNearbyGuards(Stealth stealthCaller)
        {        
            foreach (Stealth stealth in stealthList)
            {
                if (stealth == stealthCaller || stealth.GetCurrentState() == StealthState.CHASE)
                    continue;

                // make guard seach for player's postion
                if (Vector3.Distance(stealthCaller.transform.position, stealth.transform.position) < stealthCaller.allyAlertRange)
                {                  
                    stealth.SearchPosition = stealth.player.transform.position;
                    stealth.SetState(StealthState.SEARCH);
                }
            }
        }

    }

    [ExecuteInEditMode]
    public class DetectionMeter : MonoBehaviour
    {
        /*----DETECTION METER PROPERTIES----*/

        [Header("Detection meter settings")]
        public Sprite unawareMeter;
        public Sprite suspiciousMeter;
        public Sprite alertMeter;
               
        public float detectionPosY = 2.2f;      
        public float detectionScaleX = 0.4f;      
        public float detectionScaleY = 0.8f;

        private Image alertImage;
        private Image unawareImage;
        private Canvas NPCCanvas;
        
        /*----DETECTION METER PROPERTIES----*/

        //Used to reset canvas and image references when
        //switching between play and edit modes
        private void SetImages()
        {
            NPCCanvas = GetComponentInChildren<Canvas>();
            Image[] detectionImages = GetComponentsInChildren<Image>();    

            foreach (Image img in detectionImages)
            {              
                if (img.sprite == unawareMeter)
                {
                    unawareImage = img;
                }
                else if (img.sprite == alertMeter)
                {
                    alertImage = img;
                    alertImage.fillAmount = 0.0f;
                }
            }
        }

        // check if meter is setting up
        public bool IsMeterSetup()
        {
            return alertImage != null && unawareImage != null && NPCCanvas != null;
        }

        private void Awake()
        {
            SetImages();
        }

        // fill the detection meter from stealth
        public void SetFill(float detectionLevel, float suspicionThreshold)
        {    
            if(detectionLevel > suspicionThreshold)
            {               
                alertImage.sprite = alertMeter;
            }
            else
            {               
                alertImage.sprite = suspiciousMeter;
            }

            alertImage.fillAmount = detectionLevel;
        }


        void LateUpdate()
        {
            if (!Application.isPlaying && IsMeterSetup())
            {
                unawareImage.sprite = unawareMeter;
                alertImage.sprite = alertMeter;
            }
            //Update the images so they can be edited in real time in the editor
            if (NPCCanvas != null)
            {           
                NPCCanvas.gameObject.transform.LookAt(Camera.main.transform);

                //reposition and resize empty meter
                unawareImage.transform.localPosition = new Vector3(0.0f, detectionPosY, 0.0f);
                RectTransform ImageRT = unawareImage.GetComponent<RectTransform>();
                ImageRT.sizeDelta = new Vector2(detectionScaleX, detectionScaleY);

                //reposition and resize full meter
                alertImage.transform.localPosition = new Vector3(0.0f, detectionPosY, 0.0f);
                ImageRT = alertImage.GetComponent<RectTransform>();
                ImageRT.sizeDelta = new Vector2(detectionScaleX, detectionScaleY);
            }            
        }

        //builds and sets up the canvas and images used by detection meter
        public void CreateDetectionMeter()
        {
            //Check if NPC has a canvas
            NPCCanvas = GetComponentInChildren<Canvas>();
            
            //If not, add one
            if (NPCCanvas == null || NPCCanvas.name != "Detection Meter")
            {
                GameObject newNPCCanvas = new GameObject();
                NPCCanvas = newNPCCanvas.AddComponent<Canvas>();
                newNPCCanvas.name = "Detection Meter";
                newNPCCanvas.transform.SetParent(gameObject.transform, false);
            }

            //Check that there are no images already
            //TODO: make this check more robust as users might already have other images 
            //for different purposes
            Image[] detectionImages = NPCCanvas.GetComponentsInChildren<Image>();
            if (detectionImages.Length == 0)
            {
                //Set canvas to worldspace
                NPCCanvas.renderMode = RenderMode.WorldSpace;
                NPCCanvas.transform.position = Vector3.zero;

                RectTransform CanvasRT = NPCCanvas.GetComponent<RectTransform>();
                CanvasRT.sizeDelta = new Vector2(10, 10);
                CanvasRT.localPosition = Vector3.zero;

                //Add the images  
                GameObject EmptyImageGO, FullImageGO;
                EmptyImageGO = new GameObject();
                FullImageGO = new GameObject();

                //Add empty image
                unawareImage = EmptyImageGO.AddComponent<Image>();
                unawareImage.name = "Empty Detection Meter";

                //use default sprite
                if (unawareMeter == null)
                {
                    unawareMeter = Resources.Load<Sprite>("Unaware");
                    if (unawareMeter == null)
                        Debug.LogError("Failed to find sprite 'Unaware' in resource folder");
                }

                unawareImage.sprite = unawareMeter;
                unawareImage.transform.SetParent(NPCCanvas.transform, false);
                unawareImage.transform.localPosition = new Vector3(0.0f, detectionPosY, 0.0f);

                //Set the width/height of the image
                RectTransform ImageRT = unawareImage.GetComponent<RectTransform>();
                ImageRT.sizeDelta = new Vector2(detectionScaleX, detectionScaleY);


                //Add filling image
                alertImage = FullImageGO.AddComponent<Image>();
                alertImage.name = "Filled Detection Meter";

                //use default sprite
                if (alertMeter == null)
                {
                    alertMeter = Resources.Load<Sprite>("Alert");
                    if (alertMeter == null)
                        Debug.LogError("Failed to find sprite 'Alert' in resource folder");
                }

                //use default sprite
                if (suspiciousMeter == null)
                {
                    suspiciousMeter = Resources.Load<Sprite>("Suspicious");
                    if (suspiciousMeter == null)
                        Debug.LogError("Failed to find sprite 'Suspicious' in resource folder");
                }

                alertImage.sprite = alertMeter;
                alertImage.transform.SetParent(NPCCanvas.transform, false);
                alertImage.transform.localPosition = new Vector3(0.0f, detectionPosY, 0.0f);

                //Set the width/height of the image
                ImageRT = alertImage.GetComponent<RectTransform>();
                ImageRT.sizeDelta = new Vector2(detectionScaleX, detectionScaleY);

                //Fill type set to vertical
                alertImage.type = Image.Type.Filled;
                alertImage.fillMethod = Image.FillMethod.Vertical;
                alertImage.fillAmount = 0.0f;
            }
        }
    }


}
 