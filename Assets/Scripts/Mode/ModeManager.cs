using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class ModeManager : MonoBehaviourPun
{
    public enum GameMode { Convergence, Divide, Transitioning }

    [Header("Transition")]
    [SerializeField] private float transitionDelay = 0.3f;

    private GameMode currentMode = GameMode.Convergence;
    private bool prevSpaceKey;

    // Team info (populated by NetworkManager)
    private readonly List<TeamInfo> teams = new();

    // Spawned character tracking
    private readonly Dictionary<bool, GameObject> convergenceChars = new();
    private readonly Dictionary<int, GameObject> divideChars = new();

    public GameMode CurrentMode => currentMode;

    public struct TeamInfo
    {
        public int Player1ActorNumber;
        public int Player2ActorNumber;
        public bool IsTeamA;
    }

    public void RegisterTeam(int player1, int player2, bool isTeamA, GameObject convergenceChar)
    {
        teams.Add(new TeamInfo
        {
            Player1ActorNumber = player1,
            Player2ActorNumber = player2,
            IsTeamA = isTeamA
        });
        convergenceChars[isTeamA] = convergenceChar;
    }

    void Update()
    {
        if (currentMode == GameMode.Transitioning) return;
        if (teams.Count == 0) return;

        if (!IsLocalPlayerInAnyTeam()) return;

        if (InputReader.IsKeyDown(0x20, ref prevSpaceKey)) // Space
            photonView.RPC(nameof(RequestModeSwitch), RpcTarget.MasterClient);
    }

    private bool IsLocalPlayerInAnyTeam()
    {
        int localActor = PhotonNetwork.LocalPlayer.ActorNumber;
        foreach (var team in teams)
        {
            if (localActor == team.Player1ActorNumber || localActor == team.Player2ActorNumber)
                return true;
        }
        return false;
    }

    [PunRPC]
    private void RequestModeSwitch(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (currentMode == GameMode.Transitioning) return;

        Debug.Log($"[ModeManager] Mode switch requested by Actor#{info.Sender.ActorNumber}");

        if (currentMode == GameMode.Convergence)
            StartCoroutine(TransitionToDivide());
        else if (currentMode == GameMode.Divide)
            StartCoroutine(TransitionToConvergence());
    }

    [PunRPC]
    private void NotifyTransitionStart()
    {
        currentMode = GameMode.Transitioning;
        Debug.Log("[ModeManager] Transition started");
    }

    [PunRPC]
    private void NotifyTransitionComplete(int newModeInt)
    {
        currentMode = (GameMode)newModeInt;
        Debug.Log($"[ModeManager] Transition complete → {currentMode}");
    }

    [PunRPC]
    private void SpawnDivideCharacter(Vector3 position, int ownerActor, int partnerActor, bool isTeamA)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber != ownerActor) return;

        object[] initData = { ownerActor, partnerActor, isTeamA };
        var go = PhotonNetwork.Instantiate(
            "DivideCharacter",
            position,
            Quaternion.identity,
            0,
            initData
        );

        Debug.Log($"[ModeManager] Spawned DivideCharacter for Actor#{ownerActor}");
    }

    [PunRPC]
    private void DestroyOwnedDivideChars()
    {
        var controllers = FindObjectsByType<DivideController>(FindObjectsSortMode.None);
        foreach (var dc in controllers)
        {
            if (dc.photonView.IsMine)
                PhotonNetwork.Destroy(dc.gameObject);
        }
    }

    private IEnumerator TransitionToDivide()
    {
        photonView.RPC(nameof(NotifyTransitionStart), RpcTarget.All);
        yield return new WaitForSeconds(transitionDelay);

        // Save positions and destroy convergence characters
        var spawnPositions = new Dictionary<bool, Vector3>();
        foreach (var kvp in convergenceChars)
        {
            if (kvp.Value != null)
            {
                spawnPositions[kvp.Key] = kvp.Value.transform.position;
                PhotonNetwork.Destroy(kvp.Value);
            }
        }
        convergenceChars.Clear();

        yield return null; // Wait one frame for destroy to propagate

        // Tell each player to self-instantiate their DivideCharacter
        foreach (var team in teams)
        {
            if (!spawnPositions.TryGetValue(team.IsTeamA, out Vector3 basePos))
                continue;

            // Offset P1 and P2 slightly apart
            Vector3 offset1 = Vector3.left * 1f;
            Vector3 offset2 = Vector3.right * 1f;

            photonView.RPC(nameof(SpawnDivideCharacter), RpcTarget.All,
                basePos + offset1, team.Player1ActorNumber, team.Player2ActorNumber, team.IsTeamA);
            photonView.RPC(nameof(SpawnDivideCharacter), RpcTarget.All,
                basePos + offset2, team.Player2ActorNumber, team.Player1ActorNumber, team.IsTeamA);
        }

        yield return new WaitForSeconds(0.2f); // Allow spawning to complete

        photonView.RPC(nameof(NotifyTransitionComplete), RpcTarget.All, (int)GameMode.Divide);
    }

    private IEnumerator TransitionToConvergence()
    {
        photonView.RPC(nameof(NotifyTransitionStart), RpcTarget.All);
        yield return new WaitForSeconds(transitionDelay);

        // Calculate midpoints and destroy divide characters
        var midpoints = new Dictionary<bool, Vector3>();
        foreach (var team in teams)
        {
            Vector3 p1Pos = Vector3.zero;
            Vector3 p2Pos = Vector3.zero;
            bool hasP1 = false, hasP2 = false;

            // Find divide characters for this team
            var divControllers = FindObjectsByType<DivideController>(FindObjectsSortMode.None);
            foreach (var dc in divControllers)
            {
                if (dc.IsTeamA != team.IsTeamA) continue;

                if (dc.OwnerActorNumber == team.Player1ActorNumber)
                { p1Pos = dc.transform.position; hasP1 = true; }
                else if (dc.OwnerActorNumber == team.Player2ActorNumber)
                { p2Pos = dc.transform.position; hasP2 = true; }
            }

            if (hasP1 && hasP2)
                midpoints[team.IsTeamA] = (p1Pos + p2Pos) * 0.5f;
            else if (hasP1)
                midpoints[team.IsTeamA] = p1Pos;
            else if (hasP2)
                midpoints[team.IsTeamA] = p2Pos;
        }

        // Tell all players to destroy their own DivideCharacters
        photonView.RPC(nameof(DestroyOwnedDivideChars), RpcTarget.All);
        divideChars.Clear();

        yield return null; // Wait for destroy

        // Master spawns convergence characters at midpoints
        foreach (var team in teams)
        {
            if (!midpoints.TryGetValue(team.IsTeamA, out Vector3 pos))
                continue;

            object[] initData = { team.Player1ActorNumber, team.Player2ActorNumber, team.IsTeamA };
            var go = PhotonNetwork.Instantiate("ConvergenceCharacter", pos, Quaternion.identity, 0, initData);
            convergenceChars[team.IsTeamA] = go;
        }

        yield return new WaitForSeconds(0.1f);

        photonView.RPC(nameof(NotifyTransitionComplete), RpcTarget.All, (int)GameMode.Convergence);
    }
}
