using UnityEngine;
using Mirror;
using Steamworks;
using TMPro;
public enum PlayerState
{
    Normal,
    Captured,
    CarryingObject,
    Damaged,
    Finish
}

public class PlayerController : NetworkBehaviour
{
    [Header("Player Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;
    public float forwardJumpForce = 2f;
    public float pickupYpos = 1.1f;
    public float throwForce = 10f;
    public LayerMask groundLayer;

    [Header("Capture Settings")]
    public float captureDuration = 1f;
    public BoxCollider2D captureCollider;

    public TMP_Text nameText;

    private Vector2 movement;
    private bool isGrounded;
    private GameObject carriedObject;

    [SyncVar] private bool isJumping;
    [SyncVar] private bool isThrown;  // ������ ���¸� �����ϴ� ����
    [SyncVar] private bool isCarryingObject = false;

    private PlayerState playerState = PlayerState.Normal;

    private Rigidbody2D rb;
    private BoxCollider2D playerCollider;

    private bool jumpCooldown = false;
    private float jumpCooldownTime = 0.1f;
    private float jumpCooldownTimer = 0f;

    private PlayerController capturingPlayer;
    private bool captureCooldown = false;
    private float captureCooldownTimer = 0f;

    private bool lastIsGrounded;  // ���� isGrounded ���¸� ����
    private bool lastIsJumping;   // ���� isJumping ���¸� ����

    private bool isMotion = false;
    private bool damagedMotion = false;

    [SerializeField]
    private PlayerAnimationController animationController;

    public PlayerState CurrentPlayerState { get { return playerState; } }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<BoxCollider2D>();
    }

    private void Update()
    {
        print(isLocalPlayer);
        if (isServer)
            TraceCapturePlayer();

        if (!isLocalPlayer) return;
        if (playerState == PlayerState.Finish)
            return;

        HandleInput();
        HandleCarriedObject();
        HandleCooldowns();

        HandleStateSync();

        if (!jumpCooldown && isGrounded && !isJumping && playerState != PlayerState.Captured && playerState != PlayerState.Damaged)
        {
            CmdMovePlayer(movement);
        }
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer || playerState == PlayerState.Finish) return;

        if (!jumpCooldown)
        {
            isGrounded = IsGrounded();

            if (isGrounded)
            {
                animationController.StopFallAnimation();
                animationController.GroundState();
                if (isThrown)
                {
                    CmdStateReset();
                }
                else
                {
                    isJumping = false;
                }
            }
            else if (!isJumping)
            {
                animationController.PlayFallAnimation();
            }

            if (isJumping)
            {
                CmdApplyJumpForce();
                CheckAndFlipOnGroundCollision();
            }
        }

        if (!captureCooldown && playerState == PlayerState.Captured && Input.anyKey)
        {
            CmdEscape();
        }
    }
    private void HandleStateSync()
    {
        CheckAndSyncState(ref isGrounded, IsGrounded(), CmdSyncGroundedState);
        CheckAndSyncState(ref isJumping, isJumping, CmdSyncJumpingState);
    }

    [Command]
    private void CmdStateReset()
    {
        ResetState();
    }
    private void ResetState()
    {
        isThrown = false;
        isJumping = false;
        playerState = PlayerState.Normal;
    }

    private void CheckAndSyncState<T>(ref T currentState, T newState, System.Action<T> syncCommand) where T : System.IComparable
    {
        if (!currentState.Equals(newState))
        {
            currentState = newState;
            syncCommand(newState);
        }
    }

    [Command]
    private void CmdSyncGroundedState(bool grounded)
    {
        isGrounded = grounded;  // �������� ����ȭ

        if (isGrounded)
        {
            animationController.GroundState();
            RpcAnimationStateGround();
        }
    }

    [ClientRpc]
    private void RpcAnimationStateGround()
    {
        animationController.GroundState();
    }

    [Command]
    private void CmdSyncJumpingState(bool jumping)
    {
        isJumping = jumping;  // �������� ����ȭ
    }


    private void HandleCooldowns()
    {
        UpdateCooldown(ref jumpCooldown, ref jumpCooldownTimer, jumpCooldownTime, PlayerState.Normal, PlayerState.CarryingObject);
        UpdateCooldown(ref captureCooldown, ref captureCooldownTimer, captureDuration, PlayerState.Captured);
    }

    private void UpdateCooldown(ref bool cooldown, ref float cooldownTimer, float cooldownTime, params PlayerState[] validStates)
    {
        if (cooldown && System.Array.Exists(validStates, state => state == playerState))
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                cooldown = false;
            }
        }
    }
    private void HandleCarriedObject()
    {
        if (isCarryingObject && carriedObject != null)
        {
            carriedObject.transform.position = new Vector3(transform.position.x, transform.position.y + pickupYpos, 0);
        }

        if (capturingPlayer != null && playerState == PlayerState.Captured)
        {
            SyncCapturedPosition();
        }
    }

    private void SyncCapturedPosition()
    {
        transform.localScale = capturingPlayer.transform.localScale;
        transform.position = capturingPlayer.transform.position + new Vector3(0, pickupYpos, 0);
    }

    private void HandleInput()
    {
        if (playerState == PlayerState.Captured || playerState == PlayerState.Damaged) return;

        isGrounded = IsGrounded();
        movement.x = Input.GetAxisRaw("Horizontal");
        if (isGrounded) isJumping = false;

        if (Input.GetButtonDown("Jump")) CmdJump(isGrounded);
        if (Input.GetKeyDown(KeyCode.E)) CmdHandlePickupOrThrow();
        if (Input.GetKeyDown(KeyCode.X) && !isMotion) CmdAttack();
    }

    [Command]
    private void CmdJump(bool isGrounded)
    {
        if (isGrounded && !isThrown)
        {
            StartJump();
            RpcJump();
        }
    }
    private void StartJump()
    {
        isJumping = true;
        jumpCooldown = true;
        jumpCooldownTimer = jumpCooldownTime;
        animationController.PlayJumpAnimation();
        rb.linearVelocity = new Vector2(transform.localScale.x * forwardJumpForce, jumpForce);
    }

    [ClientRpc]
    private void RpcJump()
    {
        StartJump();
    }
    [Command]
    private void CmdHandlePickupOrThrow()
    {
        if (isCarryingObject)
        {
            CmdThrowObject();
        }
        else
        {
            CmdTryPickUpObject();
        }
    }

    [Command]
    private void CmdApplyJumpForce()
    {
        rb.linearVelocity = new Vector2(transform.localScale.x * forwardJumpForce, rb.linearVelocityY);
    }

    [Command]
    private void CmdFlipDirection()
    {
        transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
    }

    // �÷��̾ �Ĵ� ���� ������ ������Ʈ
    private void UpdatePlayerDirection()
    {
        if (movement.x > 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else if (movement.x < 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        nameText.transform.localScale = new Vector3(transform.localScale.x, nameText.transform.localScale.y, nameText.transform.localScale.z);
    }

    [Command]
    private void CmdMovePlayer(Vector2 movement)
    {
        if (playerState != PlayerState.Captured && !isThrown)
        {
            this.movement = movement;
            rb.linearVelocity = new Vector2(movement.x * moveSpeed, rb.linearVelocity.y);

            if (!isJumping)
            {
                if (movement.x == 0)
                {
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    animationController.PlayIdleAnimation();
                }
                else
                {
                    animationController.PlayWalkAnimation();
                }
            }
            UpdatePlayerDirection();
            RpcMovePlayer(movement);
        }
    }

    [ClientRpc]
    private void RpcMovePlayer(Vector2 movement)
    {
        if (movement.x == 0 && !isJumping)
        {
            animationController.PlayIdleAnimation();
        }
        else
        {
            animationController.PlayWalkAnimation();
        }
    }


    // �÷��̾ ���� ����ִ��� �Ǵ��ϴ� �޼���
    private bool IsGrounded()
    {
        if (jumpCooldown) return false;

        Bounds bounds = playerCollider.bounds;
        float rayStartX = bounds.min.x;
        float rayEndX = bounds.max.x;
        float raySpacing = (rayEndX - rayStartX) / 8f;
        float rayLength = 0.1f;
        Vector2 rayOrigin = new Vector2(rayStartX, bounds.min.y);

        for (int i = 0; i < 9; i++)
        {
            Vector2 rayPos = rayOrigin + new Vector2(i * raySpacing, 0);
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayPos, Vector2.down, rayLength, groundLayer);

            Debug.DrawRay(rayPos, Vector2.down * rayLength, Color.red);

            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.collider != playerCollider)
                {
                    return true;
                }
            }
        }
        return false;
    }

    [Command]
    private void CmdTryPickUpObject()
    {
        float playerDirection = Mathf.Sign(transform.localScale.x);
        Vector2 frontPosition = new Vector2(transform.position.x + playerDirection * (playerCollider.size.x / 2), transform.position.y);
        Collider2D[] colliders = Physics2D.OverlapCircleAll(frontPosition, 0.1f);
        foreach (Collider2D collider in colliders)
        {
            if (collider.CompareTag("Pickable") && collider.GetComponent<PickupObj>()?.IsCarried == false)
            {
                SetCarriedEntity(collider.gameObject, true);
                RpcSyncPickUpObject(collider.gameObject);
                collider.GetComponent<PickupObj>()?.SetPickupState(this, true);
                break;
            }
            if (PlayerCanCatchState(collider))
            {
                SetCarriedEntity(collider.gameObject, true);
                RpcSyncPickUpObject(collider.gameObject);
                break;
            }
        }
    }

    [ClientRpc]
    private void RpcSyncPickUpObject(GameObject pickedObject)
    {
        SetCarriedEntity(pickedObject, true);
    }

    private void SetCarriedEntity(GameObject obj, bool isCarried)
    {
        carriedObject = obj;
        isCarryingObject = isCarried;

        Rigidbody2D objRigidbody = obj.GetComponent<Rigidbody2D>();
        BoxCollider2D objCollider = obj.GetComponent<BoxCollider2D>();

        if (isCarried)
        {
            captureCollider.gameObject.SetActive(true);
            captureCollider.size = objCollider.size;
            captureCollider.transform.localScale = obj.transform.localScale;

            objRigidbody.bodyType = RigidbodyType2D.Kinematic;
            Physics2D.IgnoreCollision(captureCollider, objCollider, true);
            Physics2D.IgnoreCollision(playerCollider, objCollider, true);
            PlayerController playerController = obj.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // �÷��̾ ���� �� �߰����� ó��
                playerController.CapturePlayer(this);
            }

            playerState = PlayerState.CarryingObject;
        }
        else
        {
            objRigidbody.bodyType = RigidbodyType2D.Dynamic;
            Physics2D.IgnoreCollision(captureCollider, objCollider, false);
            Physics2D.IgnoreCollision(playerCollider, objCollider, false);

            PickupObj pickupObj = obj.GetComponent<PickupObj>();
            if (pickupObj != null)
            {
                // ��ü ���� �ʱ�ȭ
                pickupObj.StateReset();
            }

            PlayerController playerController = obj.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // �÷��̾ ���� �� �߰����� ó��
                playerController.ReleasePlayer();
            }
            captureCollider.gameObject.SetActive(false);

            playerState = PlayerState.Normal;
        }
    }


    [Command]
    private void CmdEscape()
    {
        if (capturingPlayer != null)
        {
            capturingPlayer.ForceThrowCarriedObject();
        }
    }

    public void ForceThrowCarriedObject()
    {
        if (isCarryingObject && carriedObject != null)
        {
            ThrowObject(carriedObject);
        }
    }

    [Command]
    public void CmdThrowObject()
    {
        if (carriedObject != null)
        {
            ThrowObject(carriedObject);
        }
    }

    public void ThrowObject(GameObject obj)
    {
        SetCarriedEntity(obj, false);

        Rigidbody2D objectRb = obj.GetComponent<Rigidbody2D>();
        objectRb.linearVelocity = Vector3.zero;
        Vector2 throwDirection = new Vector2(transform.localScale.x, 1.5f);
        objectRb.AddForce(throwDirection * throwForce, ForceMode2D.Impulse);

        PlayerController playerController = obj.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.ReleasePlayer();
            playerController.StartThrownState();  // ������ ���·� ��ȯ
            playerController.RpcStartThrownState();  // ������ ���·� ��ȯ
        }
    }

    // ������ ���·� ��ȯ�ϴ� �޼���
    public void StartThrownState()
    {
        isThrown = true;
        jumpCooldown = true;
        jumpCooldownTimer = jumpCooldownTime;  // ������ �� ��� ������ ����
        isJumping = false;  // ������ ���¿����� ���� �Ұ���
    }
    [ClientRpc]
    public void RpcStartThrownState()
    {
        ReleasePlayer();
        StartThrownState();
    }

    public void CapturePlayer(PlayerController playerController)
    {
        captureCooldownTimer = captureDuration;
        playerState = PlayerState.Captured;
        capturingPlayer = playerController;
        captureCooldown = true;
    }

    public void ReleasePlayer()
    {
        playerState = PlayerState.Normal;
        capturingPlayer = null;
    }

    private bool PlayerCanCatchState(Collider2D collider)
    {
        PlayerController playerController = collider.GetComponent<PlayerController>();
        return (collider.CompareTag("Player") && playerController != null && playerController.playerState == PlayerState.Normal && collider.transform != transform);
    }

    private void TraceCapturePlayer()
    {
        if (capturingPlayer != null && playerState == PlayerState.Captured)
        {
            Vector2 targetPosition = capturingPlayer.transform.position + Vector3.up * capturingPlayer.pickupYpos;
            rb.MovePosition(targetPosition);
            transform.localScale = capturingPlayer.transform.localScale;
        }
    }

    [Command]
    private void CmdAttack()
    {
        Attack();
        RpcAttack();
    }

    [ClientRpc]
    private void RpcAttack()
    {
        Attack();
    }

    void Attack()
    {
        isMotion = true;
        animationController.PlayAttackAnimation();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Attack") && !damagedMotion)
        {
            if (isLocalPlayer) // ���� �÷��̾����� Ȯ��
            {
                // ������ ��ġ ������ �Բ� ������ ��û
                CmdTakeDamageRequest(collision.transform.position, 3f);
            }
        }
    }
    private void CheckAndFlipOnGroundCollision()
    {
        if (!isJumping) return;

        // �÷��̾� ���ʿ� ���� ���� ����ĳ��Ʈ�� �߻��Ͽ� Ground ������Ʈ�� �ִ��� Ȯ��
        float direction = transform.localScale.x;
        Bounds bounds = playerCollider.bounds;

        // captureCollider�� Ȱ��ȭ�� ���, �÷��̾�� ĸó ������Ʈ ��θ� ������ Bounds�� Ȯ��
        if (captureCollider != null && captureCollider.gameObject.activeSelf)
        {
            Bounds captureBounds = captureCollider.bounds;
            bounds.Encapsulate(captureBounds); // �� Bounds�� ���ļ� �ϳ��� �������� ����ϴ�.
        }

        float rayStartY = bounds.min.y;
        float rayEndY = bounds.max.y;
        float raySpacing = (rayEndY - rayStartY) / 8f;
        float rayLength = 0.05f; // �÷��̾� ���� ũ�⺸�� �ణ �� ��� ����

        for (int i = 0; i <= 8; i++)
        {
            Vector2 rayOrigin = new Vector2(bounds.center.x + direction * bounds.extents.x, rayStartY + i * raySpacing);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * direction, rayLength, groundLayer);

            Debug.DrawRay(rayOrigin, Vector2.right * direction * rayLength, Color.blue);

            if (hit.collider != null && hit.collider.CompareTag("Ground"))
            {
                // Ground �±׸� ���� ������Ʈ�� �浹�� ��� ������ ����
                CmdFlipDirection();
                break; // �ϳ��� �����Ǹ� ������ �����ϰ� �ݺ��� ����
            }
        }
    }


    // ������ ������ ������ ��û �޼���
    [Command]
    private void CmdTakeDamageRequest(Vector2 attackerPosition, float knockbackForce)
    {
        RpcTakeDamage(attackerPosition, knockbackForce);
    }

    // �������� ó���ϴ� Ŭ���̾�Ʈ RPC
    [ClientRpc]
    private void RpcTakeDamage(Vector2 attackerPosition, float knockbackForce)
    {
        // ������ ó���� Ŭ���̾�Ʈ���� �̷����
        TakeDamage(attackerPosition, knockbackForce);
    }

    // ������ ó�� ����
    private void TakeDamage(Vector2 attackerPosition, float knockbackForce)
    {
        // �ൿ �Ҵ� ���� ����
        playerState = PlayerState.Damaged;
        captureCooldown = true;
        captureCooldownTimer = captureDuration;

        // ������ ��ġ �������� ���� ����
        float knockbackDirection = transform.position.x < attackerPosition.x ? -1f : 1f;
        Vector2 knockback = new Vector2(knockbackDirection * knockbackForce, 3.0f);

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(knockback, ForceMode2D.Impulse);

        // ���� ����
        transform.localScale = new Vector3(-Mathf.Sign(knockbackDirection), transform.localScale.y, transform.localScale.z);

        // �ִϸ��̼� ���
        animationController.PlayDamagedAnimation();
    }

    //Use Animation
    public void EndMotion()
    {
        isMotion = false;
    }

    // �ǰ��� �����ð�
    public void GracePeriod()
    {
        damagedMotion = false;
        playerState = PlayerState.Normal;
    }

    [Command]
    public void CmdFinishState()
    {
        playerCollider.isTrigger = true;
        rb.bodyType = RigidbodyType2D.Kinematic;
        playerState = PlayerState.Finish;
        movement = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
    }
}
