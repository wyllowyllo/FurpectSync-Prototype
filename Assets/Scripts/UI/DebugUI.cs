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

    [SerializeField] private ModeManager modeManager;

    private ConvergenceController trackedConvergence;
    private DivideController trackedDivide;

    void Update()
    {
        FindTrackedController();

        UpdatePingDisplay();
        UpdateBufferDisplay();
        UpdateVectorDisplay();
        UpdateModeDisplay();
        UpdatePlayersDisplay();
    }

    private void FindTrackedController()
    {
        if (!PhotonNetwork.InRoom) return;

        ModeManager.GameMode mode = modeManager != null
            ? modeManager.CurrentMode
            : ModeManager.GameMode.Convergence;

        int localActor = PhotonNetwork.LocalPlayer.ActorNumber;

        if (mode == ModeManager.GameMode.Convergence || mode == ModeManager.GameMode.Transitioning)
        {
            if (trackedConvergence != null) return;
            trackedDivide = null;

            var controllers = FindObjectsByType<ConvergenceController>(FindObjectsSortMode.None);
            foreach (var cc in controllers)
            {
                if (localActor == cc.Player1ActorNumber || localActor == cc.Player2ActorNumber)
                {
                    trackedConvergence = cc;
                    return;
                }
            }
        }
        else if (mode == ModeManager.GameMode.Divide)
        {
            if (trackedDivide != null) return;
            trackedConvergence = null;

            var controllers = FindObjectsByType<DivideController>(FindObjectsSortMode.None);
            foreach (var dc in controllers)
            {
                if (dc.photonView.IsMine)
                {
                    trackedDivide = dc;
                    return;
                }
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

        if (trackedConvergence != null)
        {
            float bufferMs = trackedConvergence.bufferTime * 1000f;
            float timerMs = trackedConvergence.BufferTimer * 1000f;
            bool hasP1 = trackedConvergence.Player1Input.sqrMagnitude > 0.001f;
            bool hasP2 = trackedConvergence.Player2Input.sqrMagnitude > 0.001f;

            string p1 = hasP1 ? "<color=green>OK</color>" : "<color=red>--</color>";
            string p2 = hasP2 ? "<color=green>OK</color>" : "<color=red>--</color>";

            bufferText.text = $"[Buffer] {bufferMs:F0}ms ({timerMs:F0}ms) | P1:{p1} P2:{p2}";
        }
        else if (trackedDivide != null)
        {
            bufferText.text = "[Buffer] N/A (Divide mode)";
        }
        else
        {
            bufferText.text = "[Buffer] --";
        }
    }

    private void UpdateVectorDisplay()
    {
        if (vectorText == null) return;

        if (trackedConvergence != null)
        {
            Vector2 p1 = trackedConvergence.Player1Input;
            Vector2 p2 = trackedConvergence.Player2Input;
            Vector2 combined = p1 + p2;
            vectorText.text = $"[Vector] P1:({p1.x:F1},{p1.y:F1}) + P2:({p2.x:F1},{p2.y:F1}) = ({combined.x:F1},{combined.y:F1})";
        }
        else if (trackedDivide != null)
        {
            Vector2 vel = trackedDivide.CurrentVelocity;
            vectorText.text = $"[Vector] Divide vel:({vel.x:F1},{vel.y:F1})";
        }
        else
        {
            vectorText.text = "[Vector] --";
        }
    }

    private void UpdateModeDisplay()
    {
        if (modeText == null) return;

        if (modeManager != null)
            modeText.text = $"[Mode] {modeManager.CurrentMode.ToString().ToUpper()} (Space to switch)";
        else
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
