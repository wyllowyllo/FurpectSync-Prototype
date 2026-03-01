using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("Spawn Points")]
    [SerializeField] private Transform spawnPointTeamA;
    [SerializeField] private Transform spawnPointTeamB;

    private const int MAX_PLAYERS = 4;
    private const int TEAM_SIZE = 2;

    private bool teamASpawned;
    private bool teamBSpawned;

    void Start()
    {
        PhotonNetwork.ConnectUsingSettings();
        Debug.Log("[NetworkManager] Connecting to Photon...");
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[NetworkManager] Connected to Master. Joining room...");
        var options = new RoomOptions { MaxPlayers = MAX_PLAYERS };
        PhotonNetwork.JoinOrCreateRoom("TestRoom", options, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[NetworkManager] Joined room. ActorNumber={PhotonNetwork.LocalPlayer.ActorNumber}, " +
                  $"PlayerCount={PhotonNetwork.CurrentRoom.PlayerCount}");

        if (PhotonNetwork.IsMasterClient)
            TrySpawnTeams();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[NetworkManager] Player entered: ActorNumber={newPlayer.ActorNumber}");

        if (PhotonNetwork.IsMasterClient)
            TrySpawnTeams();
    }

    private void TrySpawnTeams()
    {
        Player[] players = PhotonNetwork.PlayerList;

        if (!teamASpawned && CountTeamMembers(players, isTeamA: true) >= TEAM_SIZE)
        {
            SpawnConvergenceCharacter(spawnPointTeamA.position, GetTeamActorNumbers(players, isTeamA: true), true);
            teamASpawned = true;
        }

        if (!teamBSpawned && CountTeamMembers(players, isTeamA: false) >= TEAM_SIZE)
        {
            SpawnConvergenceCharacter(spawnPointTeamB.position, GetTeamActorNumbers(players, isTeamA: false), false);
            teamBSpawned = true;
        }
    }

    private int CountTeamMembers(Player[] players, bool isTeamA)
    {
        int count = 0;
        foreach (Player p in players)
        {
            if (IsTeamA(p.ActorNumber) == isTeamA)
                count++;
        }
        return count;
    }

    private int[] GetTeamActorNumbers(Player[] players, bool isTeamA)
    {
        var result = new int[TEAM_SIZE];
        int index = 0;
        foreach (Player p in players)
        {
            if (IsTeamA(p.ActorNumber) == isTeamA && index < TEAM_SIZE)
                result[index++] = p.ActorNumber;
        }
        return result;
    }

    private void SpawnConvergenceCharacter(Vector3 position, int[] teamActorNumbers, bool isTeamA)
    {
        object[] initData = { teamActorNumbers[0], teamActorNumbers[1], isTeamA };

        PhotonNetwork.Instantiate(
            "ConvergenceCharacter",
            position,
            Quaternion.identity,
            0,
            initData
        );

        Debug.Log($"[NetworkManager] Spawned ConvergenceCharacter for actors {teamActorNumbers[0]}, {teamActorNumbers[1]}");
    }

    public static bool IsTeamA(int actorNumber)
    {
        return actorNumber <= TEAM_SIZE;
    }
}
