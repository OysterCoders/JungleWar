

using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour {

    //Assingables
    public Transform playerCam;
    public Transform orientation;
    
    //Other
    private Rigidbody rb;

    //Rotation and look
    private float xRotation;
    private float sensitivity = 100f;
    private float sensMultiplier = 1f;
    
    //Movement
    public float moveSpeed = 4500;
    public float maxSpeed = 20;
    public bool grounded;
    public LayerMask whatIsGround;
    
    public float counterMovement = 0.175f;
    private float threshold = 0.01f;
    public float maxSlopeAngle = 35f;

    //Crouch & Slide
    private Vector3 crouchScale = new Vector3(1, 0.5f, 1);
    private Vector3 playerScale;
    public float slideForce = 400;
    public float slideCounterMovement = 0.2f;

    //Jumping
    private bool readyToJump = true;
    private float jumpCooldown = 0.25f;
    public float jumpForce = 550f;
    
    //Input
    float x, y;
    bool jumping, sprinting, crouching;
    
    //Sliding
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;

    void Awake() {
        rb = GetComponent<Rigidbody>();
    }
    
    void Start() {
        playerScale =  transform.localScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    
    private void FixedUpdate() {
        Movement();
    }

    private void Update() {
        MyInput();
        Look();
    }

    /// <summary>
    /// Find user input. Should put this in its own class but im lazy
    /// </summary>
    private void MyInput() {
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        jumping = Input.GetButton("Jump");
        crouching = Input.GetKey(KeyCode.RightShift);
      
        //Crouching
        if (Input.GetKeyDown(KeyCode.RightShift))
            StartCrouch();
        if (Input.GetKeyUp(KeyCode.RightShift))
            StopCrouch();
        if (Input.GetKeyDown(KeyCode.LeftShift))
            StartCrouch();
        if (Input.GetKeyUp(KeyCode.LeftShift))
            StopCrouch();
    }

    private void StartCrouch() {
        transform.localScale = crouchScale;
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
        if (rb.velocity.magnitude > 0.5f) {
            if (grounded) {
                rb.AddForce(orientation.transform.forward * slideForce);
            }
        }
    }

    private void StopCrouch() {
        transform.localScale = playerScale;
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
    }

    private void Movement() {
        //Extra gravity
        rb.AddForce(Vector3.down * Time.deltaTime * 10);
        
        //Find actual velocity relative to where player is looking
        Vector2 mag = FindVelRelativeToLook();
        float xMag = mag.x, yMag = mag.y;

        //Counteract sliding and sloppy movement
        CounterMovement(x, y, mag);
        
        //If holding jump && ready to jump, then jump
        if (readyToJump && jumping) Jump();

        //Set max speed
        float maxSpeed = this.maxSpeed;
        
        //If sliding down a ramp, add force down so player stays grounded and also builds speed
        if (crouching && grounded && readyToJump) {
            rb.AddForce(Vector3.down * Time.deltaTime * 3000);
            return;
        }
        
        //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        if (x > 0 && xMag > maxSpeed) x = 0;
        if (x < 0 && xMag < -maxSpeed) x = 0;
        if (y > 0 && yMag > maxSpeed) y = 0;
        if (y < 0 && yMag < -maxSpeed) y = 0;

        //Some multipliers
        float multiplier = 1f, multiplierV = 1f;
        
        // Movement in air
        if (!grounded) {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }
        
        // Movement while sliding
        if (grounded && crouching) multiplierV = 0f;

        //Apply forces to move player
        rb.AddForce(orientation.transform.forward * y * moveSpeed * Time.deltaTime * multiplier * multiplierV);
        rb.AddForce(orientation.transform.right * x * moveSpeed * Time.deltaTime * multiplier);
    }

    private void Jump() {
        if (grounded && readyToJump) {
            readyToJump = false;

            //Add jump forces
            rb.AddForce(Vector2.up * jumpForce * 1.5f);
            rb.AddForce(normalVector * jumpForce * 0.5f);
            
            //If jumping while falling, reset y velocity.
            Vector3 vel = rb.velocity;
            if (rb.velocity.y < 0.5f)
                rb.velocity = new Vector3(vel.x, 0, vel.z);
            else if (rb.velocity.y > 0) 
                rb.velocity = new Vector3(vel.x, vel.y / 2, vel.z);
            
            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }
    
    private void ResetJump() {
        readyToJump = true;
    }
    
    private float desiredX;
    private void Look() {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime * sensMultiplier;

        //Find current look rotation
        Vector3 rot = playerCam.transform.localRotation.eulerAngles;
        desiredX = rot.y + mouseX;
        
        //Rotate, and also make sure we dont over- or under-rotate.
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        //Perform the rotations
        playerCam.transform.localRotation = Quaternion.Euler(xRotation, desiredX, 0);
        orientation.transform.localRotation = Quaternion.Euler(0, desiredX, 0);
    }

    private void CounterMovement(float x, float y, Vector2 mag) {
        if (!grounded || jumping) return;

        //Slow down sliding
        if (crouching) {
            rb.AddForce(moveSpeed * Time.deltaTime * -rb.velocity.normalized * slideCounterMovement);
            return;
        }

        //Counter movement
        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) || (mag.x > threshold && x < 0)) {
            rb.AddForce(moveSpeed * orientation.transform.right * Time.deltaTime * -mag.x * counterMovement);
        }
        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) || (mag.y > threshold && y < 0)) {
            rb.AddForce(moveSpeed * orientation.transform.forward * Time.deltaTime * -mag.y * counterMovement);
        }
        
        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        if (Mathf.Sqrt((Mathf.Pow(rb.velocity.x, 2) + Mathf.Pow(rb.velocity.z, 2))) > maxSpeed) {
            float fallspeed = rb.velocity.y;
            Vector3 n = rb.velocity.normalized * maxSpeed;
            rb.velocity = new Vector3(n.x, fallspeed, n.z);
        }
    }

    /// <summary>
    /// Find the velocity relative to where the player is looking
    /// Useful for vectors calculations regarding movement and limiting movement
    /// </summary>
    /// <returns></returns>
    public Vector2 FindVelRelativeToLook() {
        float lookAngle = orientation.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = rb.velocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);
        
        return new Vector2(xMag, yMag);
    }

    private bool IsFloor(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < maxSlopeAngle;
    }

    private bool cancellingGrounded;
    
    /// <summary>
    /// Handle ground detection
    /// </summary>
    private void OnCollisionStay(Collision other) {
        //Make sure we are only checking for walkable layers
        int layer = other.gameObject.layer;
        if (whatIsGround != (whatIsGround | (1 << layer))) return;

        //Iterate through every collision in a physics update
        for (int i = 0; i < other.contactCount; i++) {
            Vector3 normal = other.contacts[i].normal;
            //FLOOR
            if (IsFloor(normal)) {
                grounded = true;
                cancellingGrounded = false;
                normalVector = normal;
                CancelInvoke(nameof(StopGrounded));
            }
        }

        //Invoke ground/wall cancel, since we can't check normals with CollisionExit
        float delay = 3f;
        if (!cancellingGrounded) {
            cancellingGrounded = true;
            Invoke(nameof(StopGrounded), Time.deltaTime * delay);
        }
    }

    private void StopGrounded() {
        grounded = false;
    }
    

}

//the rest is not being used.

// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class PlayerMovementbyDaniel : MonoBehaviour
// {
//     [System.Serializable]
//     public class MoveSettings
//     {
//         public float forwardVel = 12;
//         public float rotateVel = 100;
//         public float jumpVel = 25;
//         public float distToGrounded = 0.5f;
//         public LayerMask ground;
//     }

//     [System.Serializable]
//     public class PhysSettings
//     {
//         public float downAccel = 0.75f;
//     }

//     [System.Serializable]
//     public class InputSettings
//     {
//         public float inputDelay = 0.1f;
//         public string FORWARD_AXIS = "Vertical";
//         public string TURN_AXIS = "Horizontal";
//         public string JUMP_AXIS = "Jump";
//     }

//     public MoveSettings moveSetting = new MoveSettings();
//     public PhysSettings physSetting = new PhysSettings();
//     public InputSettings inputSetting = new InputSettings();

//     Vector3 velocity = Vector3.zero;
//     Quaternion targetRotation;
//     Rigidbody rb;
//     float forwardInput, turnInput, jumpInput;

//     public Quaternion TargetRotation
//     {
//         get { return targetRotation; }
//     }

//     bool Grounded()
//     {
//         return Physics.Raycast(transform.position, Vector3.down, moveSetting.distToGrounded, moveSetting.ground);
//     }
//     // Start is called before the first frame update
//     void Start()
//     {
//         targetRotation = transform.rotation;
//         if (GetComponent<Rigidbody>()) {
//             rb = GetComponent<Rigidbody>();
//         } else {
//             Debug.LogError("The character needs a rigid body");
//         }
//         forwardInput = turnInput = jumpInput = 0;
//     }

//     void GetInput()
//     {
//         forwardInput = Input.GetAxis(inputSetting.FORWARD_AXIS); //interpolated
//         turnInput = Input.GetAxis(inputSetting.TURN_AXIS); //interpolated
//         jumpInput = Input.GetAxisRaw(inputSetting.JUMP_AXIS); //non-interpolated
//     }
//     // Update is called once per frame
//     void Update()
//     {
//         GetInput();
//         Turn();
//     }

//     void FixedUpdate()
//     {
//         Run();
//         Jump();

//         rb.velocity = transform.TransformDirection(velocity);
//     }

//     void Run()
//     {
//         if (Mathf.Abs(forwardInput) > inputSetting.inputDelay) 
//         {
//             //move
//             velocity.z = moveSetting.forwardVel * forwardInput;
//         } else {
//             //zero velocity
//             velocity.z = 0;
//         }
//     }

//     void Turn()
//     {
//         if (Mathf.Abs(turnInput) > inputSetting.inputDelay){
//             targetRotation *= Quaternion.AngleAxis(moveSetting.rotateVel * turnInput * Time.deltaTime, Vector3.up);
//             transform.rotation = targetRotation;
//         }
//     }
//     void Jump()
//     {
//         if(jumpInput > 0 && Grounded())
//         {
//             //jump
//             velocity.y = moveSetting.jumpVel;
//         } else if(jumpInput == 0 && Grounded()) 
//         {
//             //zero out our velocity.y
//             velocity.y = 0;
//         } else 
//         {
//             // deacrease velocity.y
//             velocity.y -= physSetting.downAccel;
//         }
//     }
// }
//using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class CameraMovement : MonoBehaviour
// {
//     public Transform target;
//     public float lookSmooth = 0.09f;
//     public Vector3 offsetFromTarget = new Vector3(0, 6, -8);
//     public float xTilt = 14;

//     Vector3 destination = Vector3.zero;
//     PlayerMovementbyDaniel charController;
//     float rotateVel = 0;

//     void Start()
//     {
//         SetCameraTarget(target);
//     } 
//     void SetCameraTarget(Transform t)
//     {
//         target = t;

//         if (target != null)
//         {
//             if (target.GetComponent<PlayerMovementbyDaniel>())
//             {
//                 charController = target.GetComponent<PlayerMovementbyDaniel>();
//             } else {
//                 Debug.LogError("The camera's target needs a character controller.");
//             }
//         } else {
//             Debug.LogError("Your camera needs a target.");
//         }
//     }

//     void LateUpdate()
//     {
//         //moving
//         MoveToTarget();
//         //rotating
//         LookAtTarget();
//     }

//     void MoveToTarget()
//     {
//         destination = charController.TargetRotation * offsetFromTarget;
//         destination += target.position;
//         transform.position = destination;
//     }

//     // Update is called once per frame
//     void LookAtTarget()
//     {
//         float eulerYAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, target.eulerAngles.y, ref rotateVel, lookSmooth);
//         transform.rotation = Quaternion.Euler(xTilt, eulerYAngle, 0);
//     }
// }


