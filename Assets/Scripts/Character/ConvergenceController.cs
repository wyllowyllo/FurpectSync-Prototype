using System.Runtime.InteropServices;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(CharacterController))]
public class ConvergenceController : MonoBehaviourPun, IPunObservable
{
    private CharacterController characterController;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static bool IsKeyPressed(int vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;
#endif

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float accelerationTime = 0.15f;
    [SerializeField] private float decelerationTime = 0.1f;

    [Header("Input Buffer")]
    [Tooltip("Buffer time in seconds. Adjust with [ ] keys at runtime.")]
    public float bufferTime = 0.1f;

    [Header("Network Interpolation")]
    [SerializeField] private float lerpSpeed = 10f;

    // Team assignment (set via instantiation data)
    private int player1ActorNumber;
    private int player2ActorNumber;
    private bool isTeamA;

    // Master client: input buffer
    private Vector2 player1Input;
    private Vector2 player2Input;
    private float bufferTimer;
    private Vector2 combinedDirection;
    private Vector2 currentVelocity;

    // Non-master: interpolation targets
    private Vector3 networkPosition;
    private Quaternion networkRotation;

    // Input send throttle (20Hz)
    private float inputSendTimer;
    private const float INPUT_SEND_INTERVAL = 0.05f;

    // Debug: exposed for DebugUI
    public Vector2 Player1Input => player1Input;
    public Vector2 Player2Input => player2Input;
    public Vector2 CurrentVelocity => currentVelocity;
    public int Player1ActorNumber => player1ActorNumber;
    public int Player2ActorNumber => player2ActorNumber;
    public bool IsTeamA => isTeamA;
    public float BufferTimer => bufferTimer;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();

        object[] data = photonView.InstantiationData;
        if (data != null && data.Length >= 3)
        {
            player1ActorNumber = (int)data[0];
            player2ActorNumber = (int)data[1];
            isTeamA = (bool)data[2];
        }

        ApplyTeamColor();
        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    void Start()
    {
        if (IsLocalPlayerOnTeam())
            AssignCinemachineTarget();
    }

    void Update()
    {
        if (IsLocalPlayerOnTeam())
            SendInputToMaster();

        if (PhotonNetwork.IsMasterClient)
        {
            UpdateBuffer();
            ApplyMovement();
        }
        else
        {
            InterpolatePosition();
        }

        AdjustBufferSize();
    }

    private void SendInputToMaster()
    {
        inputSendTimer += Time.deltaTime;
        if (inputSendTimer < INPUT_SEND_INTERVAL)
            return;
        inputSendTimer = 0f;

        Vector2 rawInput = ReadLocalInput();
        Vector2 normalizedInput = rawInput.sqrMagnitude > 1f ? rawInput.normalized : rawInput;

        photonView.RPC(nameof(ReceiveInput), RpcTarget.MasterClient, normalizedInput.x, normalizedInput.y);
    }

    private Vector2 ReadLocalInput()
    {
        int localActor = PhotonNetwork.LocalPlayer.ActorNumber;
        bool isPlayer1 = localActor == player1ActorNumber;

        float h = 0f;
        float v = 0f;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (isPlayer1)
        {
            if (IsKeyPressed(0x57)) v += 1f; // W
            if (IsKeyPressed(0x53)) v -= 1f; // S
            if (IsKeyPressed(0x44)) h += 1f; // D
            if (IsKeyPressed(0x41)) h -= 1f; // A
        }
        else
        {
            if (IsKeyPressed(0x26)) v += 1f; // Up
            if (IsKeyPressed(0x28)) v -= 1f; // Down
            if (IsKeyPressed(0x27)) h += 1f; // Right
            if (IsKeyPressed(0x25)) h -= 1f; // Left
        }
#endif

        return new Vector2(h, v);
    }

    [PunRPC]
    private void ReceiveInput(float x, float y, PhotonMessageInfo info)
    {
        if (info.Sender.ActorNumber == player1ActorNumber)
            player1Input = new Vector2(x, y);
        else if (info.Sender.ActorNumber == player2ActorNumber)
            player2Input = new Vector2(x, y);
    }

    private void UpdateBuffer()
    {
        bufferTimer += Time.deltaTime;

        if (bufferTimer >= bufferTime)
        {
            combinedDirection = player1Input + player2Input;
            bufferTimer = 0f;
        }
    }

    private void ApplyMovement()
    {
        Vector2 targetVelocity = combinedDirection * moveSpeed;

        float smoothTime = targetVelocity.sqrMagnitude > currentVelocity.sqrMagnitude
            ? accelerationTime
            : decelerationTime;

        if (smoothTime > 0f)
            currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, Time.deltaTime / smoothTime);
        else
            currentVelocity = targetVelocity;

        var movement = new Vector3(currentVelocity.x, 0f, currentVelocity.y) * Time.deltaTime;
        characterController.Move(movement);

        if (currentVelocity.sqrMagnitude > 0.01f)
        {
            var lookDirection = new Vector3(currentVelocity.x, 0f, currentVelocity.y);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lookDirection),
                Time.deltaTime * 10f
            );
        }
    }

    private void InterpolatePosition()
    {
        float t = Time.deltaTime * lerpSpeed;
        transform.position = Vector3.Lerp(transform.position, networkPosition, t);
        transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, t);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(currentVelocity.x);
            stream.SendNext(currentVelocity.y);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            float vx = (float)stream.ReceiveNext();
            float vy = (float)stream.ReceiveNext();
            currentVelocity = new Vector2(vx, vy);
        }
    }

    private bool IsLocalPlayerOnTeam()
    {
        int localActor = PhotonNetwork.LocalPlayer.ActorNumber;
        return localActor == player1ActorNumber || localActor == player2ActorNumber;
    }

    private void ApplyTeamColor()
    {
        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        Color teamColor = !isTeamA
            ? new Color(0.2f, 0.4f, 1f)
            : new Color(1f, 0.3f, 0.2f);

        rend.material.SetColor("_BaseColor", teamColor);
    }

    private void AssignCinemachineTarget()
    {
        var vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam == null) return;

        vcam.Follow = transform;
        vcam.LookAt = transform;
    }

    private bool prevLeftBracket;
    private bool prevRightBracket;

    private void AdjustBufferSize()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        bool leftBracket = IsKeyPressed(0xDB);  // [
        bool rightBracket = IsKeyPressed(0xDD); // ]

        if (leftBracket && !prevLeftBracket)
        {
            bufferTime = Mathf.Max(0.01f, bufferTime - 0.01f);
            Debug.Log($"[Buffer] Decreased to {bufferTime * 1000f:F0}ms");
        }

        if (rightBracket && !prevRightBracket)
        {
            bufferTime = Mathf.Min(0.5f, bufferTime + 0.01f);
            Debug.Log($"[Buffer] Increased to {bufferTime * 1000f:F0}ms");
        }

        prevLeftBracket = leftBracket;
        prevRightBracket = rightBracket;
#endif
    }
}
