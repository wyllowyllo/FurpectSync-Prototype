using Photon.Pun;
using TMPro;
using UnityEngine;

public class DebugUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI pingText;
    [SerializeField] private TextMeshProUGUI bufferText;
    [SerializeField] private TextMeshProUGUI vectorText;
    [SerializeField] private TextMeshProUGUI modeText;
    [SerializeField] private TextMeshProUGUI playersText;

    private ConvergenceController trackedController;

    void Update()
    {
        if (trackedController == null)
            FindLocalTeamController();

        UpdatePingDisplay();
        UpdateBufferDisplay();
        UpdateVectorDisplay();
        UpdateModeDisplay();
        UpdatePlayersDisplay();
    }

    private void FindLocalTeamController()
    {
        if (!PhotonNetwork.InRoom) return;

        var controllers = FindObjectsByType<ConvergenceController>(FindObjectsSortMode.None);
        int localActor = PhotonNetwork.LocalPlayer.ActorNumber;

        foreach (var cc in controllers)
        {
            if (localActor == cc.Player1ActorNumber || localActor == cc.Player2ActorNumber)
            {
                trackedController = cc;
                return;
            }
        }
    }

    private void UpdatePingDisplay()
    {
        if (pingText == null) return;
        pingText.text = PhotonNetwork.IsConnected
            ? $"[Ping] {PhotonNetwork.GetPing()}ms"
            : "[Ping] --";
    }

    private void UpdateBufferDisplay()
    {
        if (bufferText == null) return;

        if (trackedController == null)
        {
            bufferText.text = "[Buffer] --";
            return;
        }

        float bufferMs = trackedController.bufferTime * 1000f;
        float timerMs = trackedController.BufferTimer * 1000f;
        bool hasP1 = trackedController.Player1Input.sqrMagnitude > 0.001f;
        bool hasP2 = trackedController.Player2Input.sqrMagnitude > 0.001f;

        string p1 = hasP1 ? "<color=green>OK</color>" : "<color=red>--</color>";
        string p2 = hasP2 ? "<color=green>OK</color>" : "<color=red>--</color>";

        bufferText.text = $"[Buffer] {bufferMs:F0}ms ({timerMs:F0}ms) | P1:{p1} P2:{p2}";
    }

    private void UpdateVectorDisplay()
    {
        if (vectorText == null) return;

        if (trackedController == null)
        {
            vectorText.text = "[Vector] --";
            return;
        }

        Vector2 p1 = trackedController.Player1Input;
        Vector2 p2 = trackedController.Player2Input;
        Vector2 combined = p1 + p2;

        vectorText.text = $"[Vector] P1:({p1.x:F1},{p1.y:F1}) + P2:({p2.x:F1},{p2.y:F1}) = ({combined.x:F1},{combined.y:F1})";
    }

    private void UpdateModeDisplay()
    {
        if (modeText == null) return;
        modeText.text = "[Mode] CONVERGENCE";
    }

    private void UpdatePlayersDisplay()
    {
        if (playersText == null) return;

        if (!PhotonNetwork.InRoom)
        {
            playersText.text = "[Players] Not in room";
            return;
        }

        int count = PhotonNetwork.CurrentRoom.PlayerCount;
        int max = PhotonNetwork.CurrentRoom.MaxPlayers;
        playersText.text = $"[Players] Room: {count}/{max} | " +
                           $"Master: Actor#{PhotonNetwork.MasterClient.ActorNumber} | " +
                           $"Me: Actor#{PhotonNetwork.LocalPlayer.ActorNumber}";
    }
}
