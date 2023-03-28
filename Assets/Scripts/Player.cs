using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public Transform highlightBlock;
    public Transform placeBlock;
    public Toolbar toolbar;
    public float checkIncrement = 0.1f;
    public float reach = 8f;

    public bool front
    {
        get => World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x, transform.position.y, transform.position.z + playerWidth)) ||
            World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x, transform.position.y + 1f, transform.position.z + playerWidth));
    }

    public bool back
    {
        get => World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x, transform.position.y, transform.position.z - playerWidth)) ||
                World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x, transform.position.y + 1f, transform.position.z - playerWidth));
    }

    public bool left
    {
        get => World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x - playerWidth, transform.position.y, transform.position.z)) ||
                World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x - playerWidth, transform.position.y + 1f, transform.position.z));
    }

    public bool right
    {
        get => World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x + playerWidth, transform.position.y, transform.position.z)) ||
                World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x + playerWidth, transform.position.y + 1f, transform.position.z));
    }

    [SerializeField] private Transform cameraTransform;

    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isSprinting;

    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float rotateSpeed = 60f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -9.8f;
    [SerializeField] private float playerWidth = 0.15f;
    [SerializeField] private float boundsTolerance = 0.1f;

    private float horizontal;
    private float vertical;
    private float mouseHorizontal;
    private float mouseVertical;
    private float verticalMomentum;
    private Vector3 velocity;
    private bool jumpRequest;

    private void Awake()
    {
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void FixedUpdate()
    {
        if (!World.instance.InUI)
        {
            CalculateVelocity();
            if (jumpRequest)
                Jump();

            transform.Rotate(Vector3.up * mouseHorizontal * rotateSpeed * Time.fixedDeltaTime);
            cameraTransform.Rotate(Vector3.right * -mouseVertical * rotateSpeed * Time.fixedDeltaTime);
            transform.Translate(velocity, Space.World);
        }
    }

    private void Update()
    {
        if (!World.instance.InUI)
        {
            GetPlayerInputs();
            placeCursorBlocks();
        }
    }

    private void Jump()
    {
        verticalMomentum = jumpForce;
        isGrounded = false;
        jumpRequest = false;
    }

    private void CalculateVelocity()
    {
        // Affect vertical momentum with gravity.
        if (verticalMomentum > gravity)
            verticalMomentum += Time.fixedDeltaTime * gravity;

        // if we're sprinting, use the sprint multiplier.
        if (isSprinting)
            velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * sprintSpeed;
        else
            velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * walkSpeed;

        // Apply vertical momentum (falling/jumping)
        velocity += Vector3.up * verticalMomentum * Time.fixedDeltaTime;

        if ((velocity.z > 0 && front) || (velocity.z < 0 && back))
            velocity.z = 0;
        if ((velocity.x > 0 && right) || (velocity.x < 0 && left))
            velocity.x = 0;

        if (velocity.y < 0)
            velocity.y = checkDownSpeed(velocity.y);
        else if (velocity.y > 0)
            velocity.y = checkUpSpeed(velocity.y);
    }

    private void GetPlayerInputs()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        mouseHorizontal = Input.GetAxis("Mouse X");
        mouseVertical = Input.GetAxis("Mouse Y");

        if (Input.GetKeyDown(KeyCode.LeftShift))
            isSprinting = true;
        if (Input.GetKeyUp(KeyCode.LeftShift))
            isSprinting = false;
        if (isGrounded && Input.GetKeyDown(KeyCode.Space))
            jumpRequest = true;

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (highlightBlock.gameObject.activeSelf)
        {
            // Destroy block.
            if (Input.GetMouseButtonDown(0))
                World.instance.GetChunkFromVector3(highlightBlock.position).EditVoxel(ConvertVector3ToVector3int(highlightBlock.position), 0);

            // Place block.
            if (Input.GetMouseButtonDown(1))
            {
                if (toolbar.Slots[toolbar.SlotIndex].HasItem)
                {
                    World.instance.GetChunkFromVector3(placeBlock.position).EditVoxel(ConvertVector3ToVector3int(placeBlock.position), toolbar.Slots[toolbar.SlotIndex].stack.id);
                    toolbar.Slots[toolbar.SlotIndex].Take(1);
                }
            }
        }
    }

    private void placeCursorBlocks()
    {
        float step = checkIncrement;
        Vector3 lastPos = new Vector3();

        while(step < reach)
        {
            Vector3 pos = cameraTransform.position + (cameraTransform.forward * step);
            Vector3Int posInt = new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));

            if (World.instance.CheckForVoxel(posInt))
            {
                highlightBlock.position = posInt;
                placeBlock.position = lastPos;

                highlightBlock.gameObject.SetActive(true);
                placeBlock.gameObject.SetActive(true);

                return;
            }

            lastPos = posInt;
            step += checkIncrement;
        }

        highlightBlock.gameObject.SetActive(false);
        placeBlock.gameObject.SetActive(false);
    }

    private float checkDownSpeed(float downSpeed)
    {
        if(World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)) ||
            World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)) ||
            World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth)) ||
            World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth)))
        {
            isGrounded = true;
            return 0;
        }
        else
        {
            isGrounded = false;
            return downSpeed;
        }
    }

    private float checkUpSpeed(float upSpeed)
    {
        if (World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x - playerWidth, transform.position.y + 2f + upSpeed, transform.position.z - playerWidth)) ||
            World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x + playerWidth, transform.position.y + 2f + upSpeed, transform.position.z - playerWidth)) ||
            World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x + playerWidth, transform.position.y + 2f + upSpeed, transform.position.z + playerWidth)) ||
            World.instance.CheckForVoxel(ConvertFloat3ToVector3int(transform.position.x - playerWidth, transform.position.y + 2f + upSpeed, transform.position.z + playerWidth)))
            return 0;
        else
            return upSpeed;
    }

    private Vector3Int ConvertFloat3ToVector3int(float x, float y, float z) => new Vector3Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y), Mathf.FloorToInt(z));
    private Vector3Int ConvertVector3ToVector3int(Vector3 vector3) => new Vector3Int(Mathf.FloorToInt(vector3.x), Mathf.FloorToInt(vector3.y), Mathf.FloorToInt(vector3.z));
}
