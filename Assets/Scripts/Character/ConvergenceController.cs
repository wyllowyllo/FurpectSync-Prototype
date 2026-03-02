using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(CharacterController))]
public class ConvergenceController : MonoBehaviourPun, IPunObservable
{
    private CharacterController characterController;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float accelerationTime = 0.15f;
    [SerializeField] private float decelerationTime = 0.1f;

    [Header("Input Buffer")]
    [Tooltip("Buffer time in seconds. Adjust with [ ] keys at runtime.")]
    public float bufferTime = 0.1f;

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

    // Non-master: authority position from master
    private Vector3 authorityPosition;
    private Quaternion authorityRotation;

    [Header("Soft Correction")]
    [SerializeField] private float correctionThreshold = 0.3f;
    [SerializeField] private float correctionSpeed = 5f;
    [SerializeField] private float snapThreshold = 3f;

    // Knockback
    private Vector3 knockbackVelocity;
    private const float KNOCKBACK_DECAY = 5f;

    // Input send throttle (20Hz) — delta compressed
    private float inputSendTimer;
    private const float INPUT_SEND_INTERVAL = 0.05f;
    private Vector2 lastSentInput;
    private Vector2 lastBroadcastDirection;

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
        authorityPosition = transform.position;
        authorityRotation = transform.rotation;

        InitializeBodySlam();
    }

    void Start()
    {
        if (IsLocalPlayerOnTeam())
            AssignCinemachineTarget();
    }

    private void InitializeBodySlam()
    {
        var slam = GetComponent<BodySlamDetector>();
        if (slam == null)
            slam = gameObject.AddComponent<BodySlamDetector>();
        slam.Initialize(isTeamA, PhotonNetwork.IsMasterClient);
    }

    void Update()
    {
        if (IsLocalPlayerOnTeam())
            SendInputToMaster();

        if (PhotonNetwork.IsMasterClient)
        {
            UpdateBuffer();
            ApplyMovement();
            CheckFallAndRespawn();
        }
        else
        {
            ApplyMovement();
            ApplySoftCorrection();
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

        if (normalizedInput == lastSentInput)
            return;

        lastSentInput = normalizedInput;
        photonView.RPC(nameof(ReceiveInput), RpcTarget.MasterClient, normalizedInput.x, normalizedInput.y);
    }

    private Vector2 ReadLocalInput()
    {
        int localActor = PhotonNetwork.LocalPlayer.ActorNumber;
        bool isPlayer1 = localActor == player1ActorNumber;
        return InputReader.ReadMovementInput(isTeamA, isPlayer1);
    }

    [PunRPC]
    private void ReceiveInput(float x, float y, PhotonMessageInfo info)
    {
        if (info.Sender.ActorNumber == player1ActorNumber)
            player1Input = new Vector2(x, y);
        else if (info.Sender.ActorNumber == player2ActorNumber)
            player2Input = new Vector2(x, y);
    }

    [PunRPC]
    private void ReceiveCombinedDirection(float x, float y)
    {
        combinedDirection = new Vector2(x, y);
    }

    private void UpdateBuffer()
    {
        bufferTimer += Time.deltaTime;

        if (bufferTimer >= bufferTime)
        {
            Vector2 combined = player1Input + player2Input;
            bufferTimer = 0f;

            combinedDirection = combined;

            if (combined != lastBroadcastDirection)
            {
                lastBroadcastDirection = combined;
                photonView.RPC(nameof(ReceiveCombinedDirection), RpcTarget.Others, combined.x, combined.y);
            }
        }
    }

    public void ApplyKnockback(Vector3 force)
    {
        knockbackVelocity += force;
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

        // Add knockback
        movement += knockbackVelocity * Time.deltaTime;
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * KNOCKBACK_DECAY);

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

    private void CheckFallAndRespawn()
    {
        if (transform.position.y >= -5f) return;

        // Respawn at a safe position above current XZ
        Vector3 respawn = new Vector3(transform.position.x, 2f, transform.position.z);
        characterController.enabled = false;
        transform.position = respawn;
        characterController.enabled = true;
        currentVelocity = Vector2.zero;
        knockbackVelocity = Vector3.zero;
        Debug.Log("[Convergence] Fall detected — respawned");
    }

    private void ApplySoftCorrection()
    {
        float drift = Vector3.Distance(transform.position, authorityPosition);

        if (drift > snapThreshold)
        {
            characterController.enabled = false;
            transform.position = authorityPosition;
            transform.rotation = authorityRotation;
            characterController.enabled = true;
            currentVelocity = Vector2.zero;
            return;
        }

        bool isStopped = combinedDirection.sqrMagnitude < 0.01f
                         && currentVelocity.sqrMagnitude < 0.1f;

        if (drift > correctionThreshold && isStopped)
        {
            Vector3 correction = (authorityPosition - transform.position) * (Time.deltaTime * correctionSpeed);
            characterController.Move(correction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, authorityRotation,
                Time.deltaTime * correctionSpeed * 0.5f);
        }
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
            authorityPosition = (Vector3)stream.ReceiveNext();
            authorityRotation = (Quaternion)stream.ReceiveNext();
            stream.ReceiveNext(); // velocity x (unused, keep stream order)
            stream.ReceiveNext(); // velocity y (unused, keep stream order)
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
        if (InputReader.IsKeyDown(0xDB, ref prevLeftBracket)) // [
        {
            bufferTime = Mathf.Max(0.01f, bufferTime - 0.01f);
            Debug.Log($"[Buffer] Decreased to {bufferTime * 1000f:F0}ms");
        }

        if (InputReader.IsKeyDown(0xDD, ref prevRightBracket)) // ]
        {
            bufferTime = Mathf.Min(0.5f, bufferTime + 0.01f);
            Debug.Log($"[Buffer] Increased to {bufferTime * 1000f:F0}ms");
        }
    }
}
