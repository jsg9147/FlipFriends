using UnityEngine;
using Mirror;

public class PickupObj : NetworkBehaviour
{
    [SyncVar] private bool isCarried = false;
    public bool IsCarried => isCarried;

    private PlayerController playerController;
    private Rigidbody2D rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public void SetPickupState(PlayerController playerController, bool isCarried)
    {
        this.playerController = playerController;
        this.isCarried = isCarried;

        if (isCarried)
        {
            //rb.bodyType = RigidbodyType2D.Kinematic; // �������� ���� ������ ���� �ʵ��� ����
            rb.linearVelocity = Vector2.zero; // �ӵ� �ʱ�ȭ
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
        }
    }

    public void StateReset()
    {
        this.playerController = null;
        this.isCarried = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
    }

   
}
