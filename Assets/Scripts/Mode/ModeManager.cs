using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class ModeManager : MonoBehaviourPun
{
    public enum GameMode { Convergence, Divide, Transitioning }

    [Header("Transition")]
    [SerializeField] private float transitionDelay = 0.3f;

    // Per-team mode state
    private readonly Dictionary<bool, GameMode> teamModes = new();
    private bool prevTeamAKey;
    private bool prevTeamBKey;

    // Team info (populated by NetworkManager)
    private readonly List<TeamInfo> teams = new();

    // Spawned character tracking
    private readonly Dictionary<bool, GameObject> convergenceChars = new();

    public GameMode CurrentMode
    {
        get
        {
            if (!PhotonNetwork.InRoom) return GameMode.Convergence;
            int localActor = PhotonNetwork.LocalPlayer.ActorNumber;
            foreach (var team in teams)
            {
                if (localActor == team.Player1ActorNumber || localActor == team.Player2ActorNumber)
                    return teamModes.TryGetValue(team.IsTeamA, out var m) ? m : GameMode.Convergence;
            }
            return GameMode.Convergence;
        }
    }

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
        teamModes[isTeamA] = GameMode.Convergence;
    }

    void Update()
    {
        if (teams.Count == 0) return;
        int localActor = PhotonNetwork.LocalPlayer.ActorNumber;

        foreach (var team in teams)
        {
            if (localActor != team.Player1ActorNumber && localActor != team.Player2ActorNumber)
                continue;

            if (teamModes.TryGetValue(team.IsTeamA, out GameMode mode) && mode == GameMode.Transitioning)
                break;

            bool pressed = team.IsTeamA
                ? InputReader.IsModeSwitchKeyDown(true, ref prevTeamAKey)
                : InputReader.IsModeSwitchKeyDown(false, ref prevTeamBKey);

            if (pressed)
                photonView.RPC(nameof(RequestModeSwitch), RpcTarget.MasterClient, team.IsTeamA);

            break; // 로컬 플레이어는 정확히 한 팀에만 속함
        }
    }

    [PunRPC]
    private void RequestModeSwitch(bool isTeamA, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!teamModes.TryGetValue(isTeamA, out GameMode mode) || mode == GameMode.Transitioning) return;

        Debug.Log($"[ModeManager] Mode switch requested by Actor#{info.Sender.ActorNumber} for Team{(isTeamA ? "A" : "B")}");

        if (mode == GameMode.Convergence)
            StartCoroutine(TransitionToDivide(isTeamA));
        else if (mode == GameMode.Divide)
            StartCoroutine(TransitionToConvergence(isTeamA));
    }

    [PunRPC]
    private void NotifyTransitionStart(bool isTeamA)
    {
        teamModes[isTeamA] = GameMode.Transitioning;
        Debug.Log($"[ModeManager] Transition started for Team{(isTeamA ? "A" : "B")}");
    }

    [PunRPC]
    private void NotifyTransitionComplete(bool isTeamA, int newModeInt)
    {
        teamModes[isTeamA] = (GameMode)newModeInt;
        Debug.Log($"[ModeManager] Transition complete → Team{(isTeamA ? "A" : "B")} {(GameMode)newModeInt}");
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
    private void DestroyOwnedDivideChars(bool isTeamA)
    {
        var controllers = FindObjectsByType<DivideController>(FindObjectsSortMode.None);
        foreach (var dc in controllers)
        {
            if (dc.photonView.IsMine && dc.IsTeamA == isTeamA)
                PhotonNetwork.Destroy(dc.gameObject);
        }
    }

    private IEnumerator TransitionToDivide(bool isTeamA)
    {
        photonView.RPC(nameof(NotifyTransitionStart), RpcTarget.All, isTeamA);
        yield return new WaitForSeconds(transitionDelay);

        // Save position and destroy the team's convergence character
        Vector3 spawnBase = Vector3.zero;
        bool hasPos = false;
        if (convergenceChars.TryGetValue(isTeamA, out GameObject cc) && cc != null)
        {
            spawnBase = cc.transform.position;
            hasPos = true;
            PhotonNetwork.Destroy(cc);
            convergenceChars.Remove(isTeamA);
        }

        yield return null; // Wait one frame for destroy to propagate

        if (!hasPos) yield break;

        // Find the team and spawn divide characters for its members
        foreach (var team in teams)
        {
            if (team.IsTeamA != isTeamA) continue;

            Vector3 offset1 = Vector3.left * 1f;
            Vector3 offset2 = Vector3.right * 1f;

            photonView.RPC(nameof(SpawnDivideCharacter), RpcTarget.All,
                spawnBase + offset1, team.Player1ActorNumber, team.Player2ActorNumber, team.IsTeamA);
            photonView.RPC(nameof(SpawnDivideCharacter), RpcTarget.All,
                spawnBase + offset2, team.Player2ActorNumber, team.Player1ActorNumber, team.IsTeamA);
            break;
        }

        yield return new WaitForSeconds(0.2f); // Allow spawning to complete

        photonView.RPC(nameof(NotifyTransitionComplete), RpcTarget.All, isTeamA, (int)GameMode.Divide);
    }

    private IEnumerator TransitionToConvergence(bool isTeamA)
    {
        photonView.RPC(nameof(NotifyTransitionStart), RpcTarget.All, isTeamA);
        yield return new WaitForSeconds(transitionDelay);

        // Calculate midpoint from this team's divide characters
        Vector3 p1Pos = Vector3.zero;
        Vector3 p2Pos = Vector3.zero;
        bool hasP1 = false, hasP2 = false;

        TeamInfo targetTeam = default;
        foreach (var team in teams)
        {
            if (team.IsTeamA == isTeamA) { targetTeam = team; break; }
        }

        var divControllers = FindObjectsByType<DivideController>(FindObjectsSortMode.None);
        foreach (var dc in divControllers)
        {
            if (dc.IsTeamA != isTeamA) continue;

            if (dc.OwnerActorNumber == targetTeam.Player1ActorNumber)
            { p1Pos = dc.transform.position; hasP1 = true; }
            else if (dc.OwnerActorNumber == targetTeam.Player2ActorNumber)
            { p2Pos = dc.transform.position; hasP2 = true; }
        }

        Vector3 midpoint;
        if (hasP1 && hasP2) midpoint = (p1Pos + p2Pos) * 0.5f;
        else if (hasP1)     midpoint = p1Pos;
        else if (hasP2)     midpoint = p2Pos;
        else                midpoint = Vector3.zero;

        // Tell all players to destroy their own DivideCharacters for this team
        photonView.RPC(nameof(DestroyOwnedDivideChars), RpcTarget.All, isTeamA);

        yield return null; // Wait for destroy

        // Master spawns convergence character at midpoint
        object[] initData = { targetTeam.Player1ActorNumber, targetTeam.Player2ActorNumber, isTeamA };
        var go = PhotonNetwork.Instantiate("ConvergenceCharacter", midpoint, Quaternion.identity, 0, initData);
        convergenceChars[isTeamA] = go;

        yield return new WaitForSeconds(0.1f);

        photonView.RPC(nameof(NotifyTransitionComplete), RpcTarget.All, isTeamA, (int)GameMode.Convergence);
    }
}
