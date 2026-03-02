using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(CharacterController))]
public class DivideController : MonoBehaviourPun, IPunObservable
{
    private CharacterController characterController;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.25f; // convergence(5) × 0.65
    [SerializeField] private float accelerationTime = 0.15f;
    [SerializeField] private float decelerationTime = 0.1f;

    // Team assignment (set via instantiation data)
    private int ownerActorNumber;
    private int partnerActorNumber;
    private bool isTeamA;

    private Vector2 currentVelocity;

    // Non-owner: network sync
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float interpolationSpeed = 12f;

    // Knockback (applied by BodySlamDetector)
    private Vector3 knockbackVelocity;
    private const float KNOCKBACK_DECAY = 5f;

    // Rubber band pull (applied by RubberBand)
    private Vector3 rubberBandForce;

    // Debug: exposed for DebugUI / RubberBand
    public int OwnerActorNumber => ownerActorNumber;
    public int PartnerActorNumber => partnerActorNumber;
    public bool IsTeamA => isTeamA;
    public Vector2 CurrentVelocity => currentVelocity;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();

        object[] data = photonView.InstantiationData;
        if (data != null && data.Length >= 3)
        {
            ownerActorNumber = (int)data[0];
            partnerActorNumber = (int)data[1];
            isTeamA = (bool)data[2];
        }

        ApplyTeamColor();
        networkPosition = transform.position;
        networkRotation = transform.rotation;

        InitializeBodySlam();
    }

    void Start()
    {
        if (photonView.IsMine)
            AssignCinemachineTarget();

        if (GetComponent<RubberBand>() == null)
            gameObject.AddComponent<RubberBand>();
    }

    private void InitializeBodySlam()
    {
        var slam = GetComponent<BodySlamDetector>();
        if (slam == null)
            slam = gameObject.AddComponent<BodySlamDetector>();
        slam.Initialize(isTeamA, photonView.IsMine);
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            ProcessLocalInput();
            ApplyMovement();
            CheckFallAndRespawn();
        }
        else
        {
            InterpolatePosition();
        }
    }

    private void ProcessLocalInput()
    {
        bool isPlayer1 = ownerActorNumber == GetPlayer1ActorNumber();
        Vector2 rawInput = InputReader.ReadMovementInput(isTeamA, isPlayer1);
        Vector2 normalizedInput = rawInput.sqrMagnitude > 1f ? rawInput.normalized : rawInput;

        Vector2 targetVelocity = normalizedInput * moveSpeed;

        float smoothTime = targetVelocity.sqrMagnitude > currentVelocity.sqrMagnitude
            ? accelerationTime
            : decelerationTime;

        if (smoothTime > 0f)
            currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, Time.deltaTime / smoothTime);
        else
            currentVelocity = targetVelocity;
    }

    private void ApplyMovement()
    {
        var movement = new Vector3(currentVelocity.x, 0f, currentVelocity.y) * Time.deltaTime;

        // Add knockback
        movement += knockbackVelocity * Time.deltaTime;
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * KNOCKBACK_DECAY);

        // Add rubber band pull
        movement += rubberBandForce * Time.deltaTime;
        rubberBandForce = Vector3.zero;

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

        Vector3 respawn = new Vector3(transform.position.x, 2f, transform.position.z);
        characterController.enabled = false;
        transform.position = respawn;
        characterController.enabled = true;
        currentVelocity = Vector2.zero;
        knockbackVelocity = Vector3.zero;
        Debug.Log("[Divide] Fall detected — respawned");
    }

    private void InterpolatePosition()
    {
        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * interpolationSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * interpolationSpeed);
    }

    public void ApplyKnockback(Vector3 force)
    {
        knockbackVelocity += force;
    }

    public void ApplyRubberBandForce(Vector3 force)
    {
        rubberBandForce = force;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }

    private int GetPlayer1ActorNumber()
    {
        // In a team, the lower actor number is player1
        return Mathf.Min(ownerActorNumber, partnerActorNumber);
    }

    private void ApplyTeamColor()
    {
        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        // Determine if this owner is player1 (lower actor number) for brightness variation
        bool isPlayer1 = ownerActorNumber == GetPlayer1ActorNumber();
        float brightnessMod = isPlayer1 ? 1f : 0.65f;

        Color baseColor = isTeamA
            ? new Color(1f, 0.3f, 0.2f)
            : new Color(0.2f, 0.4f, 1f);

        Color finalColor = baseColor * brightnessMod;
        finalColor.a = 1f;

        rend.material.SetColor("_BaseColor", finalColor);
    }

    private void AssignCinemachineTarget()
    {
        var vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam == null) return;

        vcam.Follow = transform;
        vcam.LookAt = transform;
    }
}
