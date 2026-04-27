using System.Collections.Generic;
using UnityEngine;

public enum RetroBuildingDoorSide
{
    Exterior = 0,
    Interior = 1
}

public enum RetroBuildingDoorLockMode
{
    None = 0,
    CodeLock = 1,
    KeyResource = 2,
    AuthorizedActors = 3,
    Team = 4
}

[DisallowMultipleComponent]
public sealed class RetroDoorAccess : MonoBehaviour
{
    [SerializeField] private string teamId;
    [SerializeField] private bool canUseLockedNpcDoors;
    [SerializeField] private string[] knownDoorCodes = System.Array.Empty<string>();
    [SerializeField] private string[] additionalTeamIds = System.Array.Empty<string>();

    public string TeamId => teamId;
    public bool CanUseLockedNpcDoors => canUseLockedNpcDoors;

    public bool HasDoorCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string normalized = NormalizeToken(code);
        for (int i = 0; i < knownDoorCodes.Length; i++)
        {
            if (NormalizeToken(knownDoorCodes[i]) == normalized)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasTeam(string candidateTeamId)
    {
        if (string.IsNullOrWhiteSpace(candidateTeamId))
        {
            return false;
        }

        string normalized = NormalizeToken(candidateTeamId);
        if (NormalizeToken(teamId) == normalized)
        {
            return true;
        }

        for (int i = 0; i < additionalTeamIds.Length; i++)
        {
            if (NormalizeToken(additionalTeamIds[i]) == normalized)
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        teamId = teamId != null ? teamId.Trim() : string.Empty;
        TrimArray(knownDoorCodes);
        TrimArray(additionalTeamIds);
    }

    private static void TrimArray(string[] values)
    {
        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = values[i] != null ? values[i].Trim() : string.Empty;
        }
    }

    private static string NormalizeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}

[DisallowMultipleComponent]
public sealed class RetroBuildingDoorInteractable : RetroInteractableBehaviour
{
    [Header("Door Portal")]
    [SerializeField] private RetroHybridBuilding building;
    [SerializeField] private RetroBuildingDoorSide doorSide;
    [SerializeField] private Transform teleportTarget;
    [SerializeField] private Transform npcApproachTarget;
    [SerializeField] private RetroBuildingDoorInteractable pairedDoor;
    [SerializeField] private bool alignActorYaw = true;
    [SerializeField] private bool clearRigidbodyVelocity = true;
    [SerializeField] private bool allowNpcUse = true;
    [SerializeField, Min(0.05f)] private float npcUseCooldown = 0.65f;
    [SerializeField] private string enteredMessage = "Entered.";
    [SerializeField, Min(0.1f)] private float enteredMessageDuration = 1.1f;

    [Header("Lock")]
    [SerializeField] private RetroBuildingDoorLockMode lockMode = RetroBuildingDoorLockMode.None;
    [SerializeField] private bool locked;
    [SerializeField] private string ownerTeamId;
    [SerializeField] private string accessCode = "1234";
    [SerializeField] private RetroResourceDefinition requiredKeyResource;
    [SerializeField, Min(1)] private int requiredKeyAmount = 1;
    [SerializeField] private bool consumeKeyOnUse;
    [SerializeField] private bool rememberCodeUsers = true;
    [SerializeField] private bool allowNpcUseWhenLocked = true;
    [SerializeField] private List<GameObject> authorizedActors = new();
    [SerializeField] private List<string> authorizedTags = new();
    [SerializeField] private string lockedMessage = "Door is locked.";
    [SerializeField] private string unauthorizedMessage = "Access denied.";

    private float nextNpcUseTime;

    protected override string DefaultInteractionVerb => CanActorUse(null, false) ? ResolveVerb() : "Locked";
    protected override string DefaultInteractionName => "building";

    public RetroHybridBuilding Building => ResolveBuilding();
    public RetroBuildingDoorSide DoorSide => doorSide;
    public RetroBuildingDoorInteractable PairedDoor => pairedDoor;
    public Transform TeleportTarget => teleportTarget;
    public Vector3 NpcApproachPosition => npcApproachTarget != null ? npcApproachTarget.position : transform.position;
    public Vector3 DestinationPosition => teleportTarget != null ? teleportTarget.position : transform.position;
    public bool LeadsInside => doorSide == RetroBuildingDoorSide.Exterior;
    public bool IsLocked => lockMode != RetroBuildingDoorLockMode.None && locked;
    public bool AllowsNpcUse => allowNpcUse;

    public void SetTeleportTarget(Transform target)
    {
        teleportTarget = target;
    }

    public void SetNpcApproachTarget(Transform target)
    {
        npcApproachTarget = target;
    }

    public void SetBuilding(RetroHybridBuilding ownerBuilding, RetroBuildingDoorSide side)
    {
        building = ownerBuilding;
        doorSide = side;
    }

    public void SetPairedDoor(RetroBuildingDoorInteractable otherDoor)
    {
        pairedDoor = otherDoor;
    }

    public void SetLocked(bool isLocked)
    {
        locked = lockMode != RetroBuildingDoorLockMode.None && isLocked;
        if (pairedDoor != null && pairedDoor.locked != locked && pairedDoor.lockMode == lockMode)
        {
            pairedDoor.locked = locked;
        }
    }

    public void ConfigureCodeLock(string code, bool startLocked = true)
    {
        lockMode = RetroBuildingDoorLockMode.CodeLock;
        accessCode = string.IsNullOrWhiteSpace(code) ? accessCode : code.Trim();
        locked = startLocked;
    }

    public void AuthorizeActor(GameObject actor)
    {
        if (actor != null && !authorizedActors.Contains(actor))
        {
            authorizedActors.Add(actor);
        }
    }

    public bool CanNpcUse(GameObject actor)
    {
        return allowNpcUse
            && Time.time >= nextNpcUseTime
            && teleportTarget != null
            && (!IsLocked || allowNpcUseWhenLocked || IsActorAuthorized(actor, true));
    }

    public bool TryUseByNpc(GameObject actor)
    {
        if (!CanNpcUse(actor))
        {
            return false;
        }

        bool used = TryUse(actor, actor != null ? actor.transform : null, true, null, out _);
        if (used)
        {
            nextNpcUseTime = Time.time + npcUseCooldown;
            if (pairedDoor != null)
            {
                pairedDoor.nextNpcUseTime = nextNpcUseTime;
            }
        }

        return used;
    }

    public bool TryUse(GameObject actor, Transform actorTransform, bool isNpc, RetroInteractor interactor, out string failureMessage)
    {
        failureMessage = null;
        actorTransform = ResolveActorTransform(actor, actorTransform, interactor);
        if (actorTransform == null || teleportTarget == null)
        {
            failureMessage = "Door has no destination.";
            return false;
        }

        if (!CanActorUse(actor, isNpc))
        {
            failureMessage = IsLocked ? lockedMessage : unauthorizedMessage;
            return false;
        }

        if (IsLocked && !TryConsumeAccessCost(actor))
        {
            failureMessage = unauthorizedMessage;
            return false;
        }

        TeleportActor(actor, actorTransform);
        return true;
    }

    public RetroHybridBuilding ResolveDestinationBuilding()
    {
        if (teleportTarget == null)
        {
            return null;
        }

        RetroHybridBuilding destinationBuilding = RetroHybridBuilding.FindInteriorContaining(teleportTarget.position, 0.08f);
        if (destinationBuilding != null)
        {
            return destinationBuilding;
        }

        return LeadsInside ? ResolveBuilding() : null;
    }

    public override bool CanInteract(in RetroInteractionContext context)
    {
        return base.CanInteract(context) && teleportTarget != null;
    }

    public override string GetInteractionPrompt(in RetroInteractionContext context)
    {
        if (!CanActorUse(context.Actor, false))
        {
            string name = ResolveInteractionName();
            return string.IsNullOrWhiteSpace(name) ? "Locked" : $"Locked {name}";
        }

        return base.GetInteractionPrompt(context);
    }

    protected override void InteractInternal(in RetroInteractionContext context)
    {
        if (!TryUse(context.Actor, context.ActorTransform, false, context.Interactor, out string failureMessage))
        {
            if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                context.Interactor?.ShowStatusMessage(failureMessage, enteredMessageDuration);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(enteredMessage))
        {
            context.Interactor?.ShowStatusMessage(enteredMessage, enteredMessageDuration);
        }
    }

    private bool CanActorUse(GameObject actor, bool isNpc)
    {
        if (teleportTarget == null)
        {
            return false;
        }

        if (!IsLocked)
        {
            return true;
        }

        if (isNpc && allowNpcUseWhenLocked)
        {
            return true;
        }

        return IsActorAuthorized(actor, isNpc);
    }

    private bool IsActorAuthorized(GameObject actor, bool isNpc)
    {
        if (actor == null)
        {
            return false;
        }

        if (authorizedActors.Contains(actor))
        {
            return true;
        }

        for (int i = 0; i < authorizedActors.Count; i++)
        {
            GameObject authorized = authorizedActors[i];
            if (authorized != null
                && (actor.transform.IsChildOf(authorized.transform) || authorized.transform.IsChildOf(actor.transform)))
            {
                return true;
            }
        }

        if (HasAuthorizedTag(actor))
        {
            return true;
        }

        RetroDoorAccess access = actor.GetComponent<RetroDoorAccess>();
        if (access == null)
        {
            access = actor.GetComponentInParent<RetroDoorAccess>();
        }

        if (access != null && access.CanUseLockedNpcDoors && isNpc)
        {
            return true;
        }

        return lockMode switch
        {
            RetroBuildingDoorLockMode.CodeLock => HasKnownCode(actor, access),
            RetroBuildingDoorLockMode.KeyResource => HasRequiredKey(actor),
            RetroBuildingDoorLockMode.AuthorizedActors => false,
            RetroBuildingDoorLockMode.Team => HasTeamAccess(access),
            _ => true
        };
    }

    private bool HasAuthorizedTag(GameObject actor)
    {
        if (authorizedTags == null || authorizedTags.Count == 0 || actor == null)
        {
            return false;
        }

        for (int i = 0; i < authorizedTags.Count; i++)
        {
            string tag = authorizedTags[i];
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            try
            {
                if (actor.CompareTag(tag))
                {
                    return true;
                }
            }
            catch (UnityException)
            {
            }
        }

        return false;
    }

    private bool HasKnownCode(GameObject actor, RetroDoorAccess access)
    {
        bool knowsCode = access != null && access.HasDoorCode(accessCode);
        if (knowsCode && rememberCodeUsers)
        {
            AuthorizeActor(actor);
        }

        return knowsCode;
    }

    private bool HasTeamAccess(RetroDoorAccess access)
    {
        return access != null
            && !string.IsNullOrWhiteSpace(ownerTeamId)
            && access.HasTeam(ownerTeamId);
    }

    private bool HasRequiredKey(GameObject actor)
    {
        if (requiredKeyResource == null)
        {
            return false;
        }

        RetroInventory inventory = actor.GetComponent<RetroInventory>();
        if (inventory == null)
        {
            inventory = actor.GetComponentInParent<RetroInventory>();
        }

        return inventory != null && inventory.Has(requiredKeyResource, requiredKeyAmount);
    }

    private bool TryConsumeAccessCost(GameObject actor)
    {
        if (lockMode != RetroBuildingDoorLockMode.KeyResource || !consumeKeyOnUse || requiredKeyResource == null)
        {
            return true;
        }

        RetroInventory inventory = actor.GetComponent<RetroInventory>();
        if (inventory == null)
        {
            inventory = actor.GetComponentInParent<RetroInventory>();
        }

        return inventory != null && inventory.Remove(requiredKeyResource, requiredKeyAmount);
    }

    private void TeleportActor(GameObject actor, Transform actorTransform)
    {
        Rigidbody actorBody = actorTransform.GetComponent<Rigidbody>();
        if (actorBody == null && actor != null)
        {
            actorBody = actor.GetComponentInParent<Rigidbody>();
        }

        Vector3 targetPosition = teleportTarget.position;
        Quaternion targetRotation = alignActorYaw
            ? Quaternion.Euler(0f, teleportTarget.eulerAngles.y, 0f)
            : actorTransform.rotation;

        if (actorBody != null)
        {
            actorBody.position = targetPosition;
            actorBody.rotation = targetRotation;
            if (clearRigidbodyVelocity)
            {
#if UNITY_6000_0_OR_NEWER
                actorBody.linearVelocity = Vector3.zero;
#else
                actorBody.velocity = Vector3.zero;
#endif
                actorBody.angularVelocity = Vector3.zero;
            }
        }
        else
        {
            actorTransform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        Physics.SyncTransforms();

        RetroNpcAgent npc = actorTransform.GetComponent<RetroNpcAgent>();
        if (npc == null && actor != null)
        {
            npc = actor.GetComponentInParent<RetroNpcAgent>();
        }

        if (npc != null)
        {
            npc.NotifyTeleportedByDoor(this);
        }
    }

    private RetroHybridBuilding ResolveBuilding()
    {
        if (building != null)
        {
            return building;
        }

        building = GetComponentInParent<RetroHybridBuilding>();
        return building;
    }

    private string ResolveVerb()
    {
        return doorSide == RetroBuildingDoorSide.Interior ? "Exit" : "Enter";
    }

    private string ResolveInteractionName()
    {
        string prompt = base.GetInteractionPrompt(default);
        string verb = ResolveVerb();
        if (prompt.StartsWith(verb + " ", System.StringComparison.OrdinalIgnoreCase))
        {
            return prompt.Substring(verb.Length + 1);
        }

        return "building";
    }

    private void OnValidate()
    {
        requiredKeyAmount = Mathf.Max(1, requiredKeyAmount);
        npcUseCooldown = Mathf.Max(0.05f, npcUseCooldown);
        enteredMessageDuration = Mathf.Max(0.1f, enteredMessageDuration);
        if (lockMode == RetroBuildingDoorLockMode.None)
        {
            locked = false;
        }
    }

    private static Transform ResolveActorTransform(GameObject actor, Transform actorTransform, RetroInteractor interactor)
    {
        if (actorTransform != null)
        {
            return actorTransform;
        }

        if (actor != null)
        {
            return actor.transform;
        }

        return interactor != null ? interactor.transform : null;
    }
}
