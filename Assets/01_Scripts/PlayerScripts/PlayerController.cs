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
    [SyncVar] private bool isThrown;  // 던져진 상태를 관리하는 변수
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

    private bool lastIsGrounded;  // 이전 isGrounded 상태를 추적
    private bool lastIsJumping;   // 이전 isJumping 상태를 추적

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
        isGrounded = grounded;  // 서버에서 동기화

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
        isJumping = jumping;  // 서버에서 동기화
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

    // 플레이어가 쳐다 보는 방향을 업데이트
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


    // 플레이어가 땅에 닿아있는지 판단하는 메서드
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
                // 플레이어를 잡을 때 추가적인 처리
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
                // 물체 상태 초기화
                pickupObj.StateReset();
            }

            PlayerController playerController = obj.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // 플레이어를 놓을 때 추가적인 처리
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
            playerController.StartThrownState();  // 던져진 상태로 전환
            playerController.RpcStartThrownState();  // 던져진 상태로 전환
        }
    }

    // 던져진 상태로 전환하는 메서드
    public void StartThrownState()
    {
        isThrown = true;
        jumpCooldown = true;
        jumpCooldownTimer = jumpCooldownTime;  // 던져진 후 잠시 판정을 멈춤
        isJumping = false;  // 던져진 상태에서는 점프 불가능
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
            if (isLocalPlayer) // 로컬 플레이어인지 확인
            {
                // 서버에 위치 정보와 함께 데미지 요청
                CmdTakeDamageRequest(collision.transform.position, 3f);
            }
        }
    }
    private void CheckAndFlipOnGroundCollision()
    {
        if (!isJumping) return;

        // 플레이어 앞쪽에 여러 개의 레이캐스트를 발사하여 Ground 오브젝트가 있는지 확인
        float direction = transform.localScale.x;
        Bounds bounds = playerCollider.bounds;

        // captureCollider가 활성화된 경우, 플레이어와 캡처 오브젝트 모두를 포함한 Bounds로 확장
        if (captureCollider != null && captureCollider.gameObject.activeSelf)
        {
            Bounds captureBounds = captureCollider.bounds;
            bounds.Encapsulate(captureBounds); // 두 Bounds를 합쳐서 하나의 영역으로 만듭니다.
        }

        float rayStartY = bounds.min.y;
        float rayEndY = bounds.max.y;
        float raySpacing = (rayEndY - rayStartY) / 8f;
        float rayLength = 0.05f; // 플레이어 가로 크기보다 약간 더 길게 설정

        for (int i = 0; i <= 8; i++)
        {
            Vector2 rayOrigin = new Vector2(bounds.center.x + direction * bounds.extents.x, rayStartY + i * raySpacing);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * direction, rayLength, groundLayer);

            Debug.DrawRay(rayOrigin, Vector2.right * direction * rayLength, Color.blue);

            if (hit.collider != null && hit.collider.CompareTag("Ground"))
            {
                // Ground 태그를 가진 오브젝트와 충돌한 경우 방향을 반전
                CmdFlipDirection();
                break; // 하나라도 감지되면 방향을 반전하고 반복문 종료
            }
        }
    }


    // 서버로 전달할 데미지 요청 메서드
    [Command]
    private void CmdTakeDamageRequest(Vector2 attackerPosition, float knockbackForce)
    {
        RpcTakeDamage(attackerPosition, knockbackForce);
    }

    // 데미지를 처리하는 클라이언트 RPC
    [ClientRpc]
    private void RpcTakeDamage(Vector2 attackerPosition, float knockbackForce)
    {
        // 데미지 처리는 클라이언트에서 이루어짐
        TakeDamage(attackerPosition, knockbackForce);
    }

    // 데미지 처리 로직
    private void TakeDamage(Vector2 attackerPosition, float knockbackForce)
    {
        // 행동 불능 상태 설정
        playerState = PlayerState.Damaged;
        captureCooldown = true;
        captureCooldownTimer = captureDuration;

        // 공격자 위치 기준으로 방향 설정
        float knockbackDirection = transform.position.x < attackerPosition.x ? -1f : 1f;
        Vector2 knockback = new Vector2(knockbackDirection * knockbackForce, 3.0f);

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(knockback, ForceMode2D.Impulse);

        // 방향 설정
        transform.localScale = new Vector3(-Mathf.Sign(knockbackDirection), transform.localScale.y, transform.localScale.z);

        // 애니메이션 재생
        animationController.PlayDamagedAnimation();
    }

    //Use Animation
    public void EndMotion()
    {
        isMotion = false;
    }

    // 피격후 무적시간
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
