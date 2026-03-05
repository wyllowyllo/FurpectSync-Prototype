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
        if (teams.Count == 0) return;
        if (currentMode == GameMode.Transitioning) return;
        if (!IsLocalPlayerInAnyTeam()) return;

        if (InputReader.IsKeyDown(0x20, ref prevSpaceKey))
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
        Debug.Log($"[ModeManager] Transition complete → {(GameMode)newModeInt}");
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

        var spawnData = new List<(TeamInfo team, Vector3 pos)>();
        foreach (var team in teams)
        {
            if (convergenceChars.TryGetValue(team.IsTeamA, out GameObject cc) && cc != null)
            {
                spawnData.Add((team, cc.transform.position));
                PhotonNetwork.Destroy(cc);
                convergenceChars.Remove(team.IsTeamA);
            }
        }

        yield return null; // Wait one frame for destroy to propagate

        foreach (var (team, spawnBase) in spawnData)
        {
            photonView.RPC(nameof(SpawnDivideCharacter), RpcTarget.All,
                spawnBase + Vector3.left, team.Player1ActorNumber, team.Player2ActorNumber, team.IsTeamA);
            photonView.RPC(nameof(SpawnDivideCharacter), RpcTarget.All,
                spawnBase + Vector3.right, team.Player2ActorNumber, team.Player1ActorNumber, team.IsTeamA);
        }

        yield return new WaitForSeconds(0.2f);

        photonView.RPC(nameof(NotifyTransitionComplete), RpcTarget.All, (int)GameMode.Divide);
    }

    private IEnumerator TransitionToConvergence()
    {
        photonView.RPC(nameof(NotifyTransitionStart), RpcTarget.All);
        yield return new WaitForSeconds(transitionDelay);

        var divControllers = FindObjectsByType<DivideController>(FindObjectsSortMode.None);
        var spawnData = new List<(TeamInfo team, Vector3 midpoint)>();

        foreach (var team in teams)
        {
            Vector3 p1Pos = Vector3.zero;
            Vector3 p2Pos = Vector3.zero;
            bool hasP1 = false, hasP2 = false;

            foreach (var dc in divControllers)
            {
                if (dc.IsTeamA != team.IsTeamA) continue;

                if (dc.OwnerActorNumber == team.Player1ActorNumber)
                { p1Pos = dc.transform.position; hasP1 = true; }
                else if (dc.OwnerActorNumber == team.Player2ActorNumber)
                { p2Pos = dc.transform.position; hasP2 = true; }
            }

            Vector3 midpoint;
            if (hasP1 && hasP2) midpoint = (p1Pos + p2Pos) * 0.5f;
            else if (hasP1)     midpoint = p1Pos;
            else if (hasP2)     midpoint = p2Pos;
            else                midpoint = Vector3.zero;

            spawnData.Add((team, midpoint));
        }

        photonView.RPC(nameof(DestroyOwnedDivideChars), RpcTarget.All);
        yield return null; // Wait for destroy

        foreach (var (team, midpoint) in spawnData)
        {
            object[] initData = { team.Player1ActorNumber, team.Player2ActorNumber, team.IsTeamA };
            var go = PhotonNetwork.Instantiate("ConvergenceCharacter", midpoint, Quaternion.identity, 0, initData);
            convergenceChars[team.IsTeamA] = go;
        }

        yield return new WaitForSeconds(0.1f);

        photonView.RPC(nameof(NotifyTransitionComplete), RpcTarget.All, (int)GameMode.Convergence);
    }
}
