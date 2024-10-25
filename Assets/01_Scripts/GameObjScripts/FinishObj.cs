using UnityEngine;
using Mirror;

public class FinishObj : NetworkBehaviour
{
    // ���߿� Ư�� Ű�� ������ Ŭ���� �ǰ� �ٽ� �ٸ� Ű�� ������ Ŭ���� ��� �Ҽ� �ְ�
    // Ȥ�� �� ��Ȳ
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && isServer)
        {
            Finish(collision.GetComponent<PlayerController>());
        }
    }

    private void Finish(PlayerController player)
    {
        player.CmdFinishState();
    }
}
