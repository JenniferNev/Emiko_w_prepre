using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// One atom of the chemistry lab. Holds the element's bond "sockets" (the
/// directions its covalent bonds point, built from real bond angles) and
/// snaps to a nearby atom when the player lets go of it.
///
/// Atoms marked isDispenser are the originals on the table: grabbing one
/// leaves a copy in its place. While holding: click Use to drop and bond,
/// hold Use to delete, VRChat's drop (right click / grip) to replace an end
/// atom of a molecule.
///
/// Size convention (see Data/elements.json): the sphere's diameter is twice
/// the covalent radius at 1 pm = 1 mm, so two bonded atoms touch exactly at
/// the real bond length r1 + r2. The radius is read from the object's scale.
///
/// Local only for now (learning plan Phase 4): bonds are not synced, so
/// other players don't see them yet. Networking comes in Phase 5.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ElementAtom : UdonSharpBehaviour
{
    [Header("Element (values from Data/elements.json)")]
    public string symbol = "H";

    [Tooltip("How many covalent bonds this atom makes (H 1, O 2, N 3, C 4).")]
    public int maxBonds = 1;

    [Tooltip("terminal, linear, bent, trigonal-planar, trigonal-pyramidal, tetrahedral or octahedral")]
    public string geometry = "terminal";

    [Tooltip("Angle between two bonds in degrees (CH4 109.471, NH3 106.67, H2O 104.48).")]
    public float bondAngle = 109.471f;

    [Header("Snapping")]
    [Tooltip("Snap when the centers are closer than bond length × this factor.")]
    public float snapFactor = 1.6f;

    [Header("Table original")]
    [Tooltip("An original on the table: grabbing it leaves a fresh copy in its place. Originals never bond.")]
    public bool isDispenser = false;

    [Header("Controls")]
    [Tooltip("Holding Use (left click / trigger) this long deletes the held atom. A shorter click drops it.")]
    public float deleteHoldSeconds = 0.6f;

    private Vector3[] sockets;          // bond directions, in this atom's local space
    private ElementAtom[] partners;     // the atom bonded on each socket (null = free)
    private int bondCount;
    private bool deleting;              // set while being deleted, so the drop doesn't bond
    private bool useHeld;               // Use is pressed right now
    private float useDownTime;          // when Use was pressed
    private bool clickDrop;             // this drop came from our own click, not VRChat's drop button
    private float pickupTime;           // when it was grabbed

    // Keeping the button down after a grab sends UseDown once the grab is
    // 0.5 s old (ClientSim's INITIAL_PICKUP_DURATION). A UseDown sooner than
    // this after the grab is that same press, not a new click.
    private const float GrabPressSeconds = 0.6f;

    // Where a dispenser sits, so its replacement appears in the same spot.
    private Transform homeParent;
    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;
    private Vector3 homeLocalScale;

    void Start()
    {
        sockets = ComputeSockets();
        partners = new ElementAtom[sockets.Length];
        bondCount = 0;                  // a copy made by a dispenser starts unbonded

        homeParent = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
        homeLocalScale = transform.localScale;
    }

    // ------------------------------------------------------------------ queries

    /// <summary>Sphere radius in world units (the mesh is a radius-0.5 sphere).</summary>
    public float GetRadius()
    {
        return transform.lossyScale.x * 0.5f;
    }

    public int GetBondCount()
    {
        return bondCount;
    }

    public int GetFreeSocketCount()
    {
        return sockets == null ? 0 : sockets.Length - bondCount;
    }

    public bool IsBondedTo(ElementAtom other)
    {
        for (int i = 0; i < partners.Length; i++)
        {
            if (partners[i] == other) return true;
        }
        return false;
    }

    /// <summary>The free socket that points most toward a world-space direction, or -1.</summary>
    public int GetBestFreeSocket(Vector3 worldDirection)
    {
        Vector3 wanted = worldDirection.normalized;
        int best = -1;
        float bestDot = -2f;
        for (int i = 0; i < sockets.Length; i++)
        {
            if (partners[i] != null) continue;
            float d = Vector3.Dot(transform.TransformDirection(sockets[i]), wanted);
            if (d > bestDot)
            {
                bestDot = d;
                best = i;
            }
        }
        return best;
    }

    public Vector3 GetSocketWorldDirection(int socket)
    {
        return transform.TransformDirection(sockets[socket]);
    }

    public void SetPartner(int socket, ElementAtom other)
    {
        partners[socket] = other;
        bondCount++;
    }

    /// <summary>Swaps the atom on a socket for another, keeping the bond count.</summary>
    public void ReplacePartner(int socket, ElementAtom other)
    {
        partners[socket] = other;
    }

    /// <summary>The socket bonded to `other`, or -1.</summary>
    public int GetSocketOf(ElementAtom other)
    {
        for (int i = 0; i < partners.Length; i++)
        {
            if (partners[i] == other) return i;
        }
        return -1;
    }

    /// <summary>The first bonded atom, or null (enough for end atoms, which have one).</summary>
    public ElementAtom GetFirstPartner()
    {
        for (int i = 0; i < partners.Length; i++)
        {
            if (partners[i] != null) return partners[i];
        }
        return null;
    }

    /// <summary>A bonded atom is carried by its partner, so it stops being a pickup.</summary>
    public void SetGrabbable(bool grabbable)
    {
        VRC_Pickup pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
        if (pickup != null) pickup.pickupable = grabbable;
    }

    // ------------------------------------------------------------ pickup events

    /// <summary>
    /// Grabbing a table original: the atom in your hand becomes a normal atom,
    /// and a copy is left on the table to be the new original.
    /// </summary>
    public override void OnPickup()
    {
        pickupTime = Time.time;
        useHeld = false;
        if (!isDispenser) return;
        isDispenser = false;
        SpawnReplacement();
    }

    // Controls while holding an atom (desktop / VR):
    //   click Use (left click / trigger tap)   -> drop and bond
    //   hold Use for deleteHoldSeconds         -> delete the atom and its molecule
    //   VRChat's drop (right click / grip)     -> replace an end atom, else bond
    // VRChat owns the buttons; we only react to the events they send.

    public override void OnPickupUseDown()
    {
        if (Time.time - pickupTime < GrabPressSeconds) return;   // still the press that grabbed it
        useHeld = true;
        useDownTime = Time.time;
        // Check back later: if Use is still down by then, it was a hold.
        SendCustomEventDelayedSeconds(nameof(CheckDeleteHold), deleteHoldSeconds);
    }

    public override void OnPickupUseUp()
    {
        if (!useHeld) return;               // the click that grabbed it, or already deleted
        useHeld = false;
        clickDrop = true;                   // released early: a click, so drop
        DropPickup();
    }

    /// <summary>Runs deleteHoldSeconds after Use went down (must be public for SendCustomEvent).</summary>
    public void CheckDeleteHold()
    {
        // Still down, and this is the same press (not an older, shorter one).
        if (!useHeld || Time.time - useDownTime < deleteHoldSeconds - 0.05f) return;
        useHeld = false;
        deleting = true;
        DropPickup();                       // let go first, so the hand isn't left holding nothing
        Destroy(gameObject);                // children (bonded atoms) go with it
    }

    public override void OnDrop()
    {
        useHeld = false;
        if (deleting) return;

        bool byClick = clickDrop;
        clickDrop = false;

        // VRChat's own drop (right click / grip): replace an end atom if one is close.
        if (!byClick && TryReplaceNearby()) return;
        TryBondNearby();
    }

    private void DropPickup()
    {
        VRC_Pickup pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
        if (pickup != null) pickup.Drop();
    }

    /// <summary>
    /// Copies this atom back into its table spot. Instantiate is local only:
    /// other players don't see the copy (Phase 5 replaces this with a synced
    /// VRC Object Pool).
    /// </summary>
    private void SpawnReplacement()
    {
        GameObject copy = Instantiate(gameObject, homeParent);
        copy.name = gameObject.name;         // not "Atom_H(Clone)"

        Transform t = copy.transform;
        t.localPosition = homeLocalPosition;
        t.localRotation = homeLocalRotation;
        t.localScale = homeLocalScale;

        ElementAtom atom = copy.GetComponent<ElementAtom>();
        atom.isDispenser = true;
    }

    // ------------------------------------------------------------------ bonding

    public void TryBondNearby()
    {
        if (isDispenser || GetFreeSocketCount() == 0) return;

        // Largest covalent radius is ~0.26 m (Cs), so this reaches any partner.
        Collider[] hits = Physics.OverlapSphere(transform.position, GetRadius() * snapFactor + 0.5f);

        ElementAtom best = null;
        float bestGap = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            ElementAtom other = hits[i].GetComponent<ElementAtom>();
            if (other == null || other == this || other.isDispenser) continue;
            if (other.GetFreeSocketCount() == 0 || IsBondedTo(other)) continue;

            float bondLength = GetRadius() + other.GetRadius();
            float distance = Vector3.Distance(transform.position, other.transform.position);
            if (distance > bondLength * snapFactor) continue;

            float gap = distance - bondLength;
            if (gap < bestGap)
            {
                bestGap = gap;
                best = other;
            }
        }

        if (best != null) BondWith(best);
    }

    /// <summary>
    /// Bonds this atom and another. The "mover" is the atom that is not yet
    /// part of a molecule (if both are free, the one with fewer bonds, e.g. H
    /// moves onto C). It is rotated so its socket points back at the anchor,
    /// placed at the real bond length, and parented to the anchor.
    /// </summary>
    public void BondWith(ElementAtom other)
    {
        ElementAtom mover;
        ElementAtom anchor;
        if (bondCount == 0 && other.GetBondCount() > 0)
        {
            mover = this;
            anchor = other;
        }
        else if (other.GetBondCount() == 0 && bondCount > 0)
        {
            mover = other;
            anchor = this;
        }
        else if (bondCount == 0 && other.GetBondCount() == 0)
        {
            mover = maxBonds <= other.maxBonds ? this : other;
            anchor = mover == this ? other : this;
        }
        else
        {
            return; // joining two molecules comes later
        }

        Transform moverT = mover.transform;
        Transform anchorT = anchor.transform;

        int anchorSocket = anchor.GetBestFreeSocket(moverT.position - anchorT.position);
        int moverSocket = mover.GetBestFreeSocket(anchorT.position - moverT.position);
        if (anchorSocket < 0 || moverSocket < 0) return;

        Vector3 anchorDir = anchor.GetSocketWorldDirection(anchorSocket);
        Vector3 moverDir = mover.GetSocketWorldDirection(moverSocket);

        // Turn the mover so its socket faces back along the anchor's socket...
        moverT.rotation = Quaternion.FromToRotation(moverDir, -anchorDir) * moverT.rotation;
        // ...and put it at the real bond length: r(anchor) + r(mover).
        moverT.position = anchorT.position + anchorDir * (anchor.GetRadius() + mover.GetRadius());
        moverT.SetParent(anchorT, true);

        anchor.SetPartner(anchorSocket, mover);
        mover.SetPartner(moverSocket, anchor);
        mover.SetGrabbable(false);
    }

    // ------------------------------------------------------------------ replacing

    /// <summary>
    /// Swaps this (free, single) atom in for the closest end atom of a molecule,
    /// i.e. one with exactly one bond. Example: C onto one H of CH4 gives H3C–C,
    /// and the new C has three free sockets. Returns false if nothing was replaced.
    /// </summary>
    public bool TryReplaceNearby()
    {
        if (bondCount != 0 || sockets.Length == 0) return false;

        Collider[] hits = Physics.OverlapSphere(transform.position, GetRadius() * snapFactor + 0.5f);

        ElementAtom target = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            ElementAtom other = hits[i].GetComponent<ElementAtom>();
            if (other == null || other == this || other.isDispenser) continue;
            if (other.GetBondCount() != 1) continue;   // only end atoms, for now

            float distance = Vector3.Distance(transform.position, other.transform.position);
            if (distance > (GetRadius() + other.GetRadius()) * snapFactor) continue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                target = other;
            }
        }

        if (target == null) return false;
        ReplaceAtom(target);
        return true;
    }

    private void ReplaceAtom(ElementAtom old)
    {
        ElementAtom partner = old.GetFirstPartner();
        if (partner == null) return;
        int partnerSocket = partner.GetSocketOf(old);
        int mySocket = GetBestFreeSocket(partner.transform.position - transform.position);
        if (partnerSocket < 0 || mySocket < 0) return;

        Transform me = transform;
        Transform partnerT = partner.transform;
        Transform oldT = old.transform;

        // Same placement as bonding: face the partner's socket, sit at r(partner) + r(me).
        Vector3 bondDir = partner.GetSocketWorldDirection(partnerSocket);
        me.rotation = Quaternion.FromToRotation(GetSocketWorldDirection(mySocket), -bondDir) * me.rotation;
        me.position = partnerT.position + bondDir * (partner.GetRadius() + GetRadius());

        if (oldT.parent == partnerT)
        {
            // The old atom hung off its partner: take its place as a carried atom.
            me.SetParent(partnerT, true);
            SetGrabbable(false);
        }
        else
        {
            // The old atom carried its partner (it was the molecule's root): become the root.
            me.SetParent(oldT.parent, true);
            partnerT.SetParent(me, true);   // before Destroy, or the partner would go with it
        }

        partner.ReplacePartner(partnerSocket, this);
        SetPartner(mySocket, partner);
        Destroy(old.gameObject);
    }

    // ------------------------------------------------------------------ geometry

    /// <summary>Bond directions (unit vectors, local space) for the geometry.</summary>
    public Vector3[] ComputeSockets()
    {
        Vector3[] s;
        if (maxBonds <= 0 || geometry == "none")
        {
            s = new Vector3[0];
        }
        else if (geometry == "linear")
        {
            s = new Vector3[] { Vector3.up, Vector3.down };
        }
        else if (geometry == "bent")
        {
            float half = bondAngle * 0.5f * Mathf.Deg2Rad;
            s = new Vector3[]
            {
                new Vector3(Mathf.Sin(half), -Mathf.Cos(half), 0f),
                new Vector3(-Mathf.Sin(half), -Mathf.Cos(half), 0f)
            };
        }
        else if (geometry == "trigonal-planar")
        {
            s = Ring(3, 90f);
        }
        else if (geometry == "trigonal-pyramidal")
        {
            s = Ring(3, PyramidAngle(bondAngle));
        }
        else if (geometry == "tetrahedral")
        {
            // One bond straight up, three below it, all 109.47° apart.
            Vector3[] ring = Ring(3, PyramidAngle(109.4712f));
            s = new Vector3[] { Vector3.up, ring[0], ring[1], ring[2] };
        }
        else if (geometry == "octahedral")
        {
            s = new Vector3[]
            {
                Vector3.up, Vector3.down, Vector3.left,
                Vector3.right, Vector3.forward, Vector3.back
            };
        }
        else
        {
            s = new Vector3[] { Vector3.up }; // "terminal": one bond
        }

        // Never more sockets than bonds (e.g. a 2-bond atom on an octahedral layout).
        if (s.Length > maxBonds && maxBonds > 0)
        {
            Vector3[] trimmed = new Vector3[maxBonds];
            for (int i = 0; i < maxBonds; i++) trimmed[i] = s[i];
            s = trimmed;
        }
        return s;
    }

    /// <summary>
    /// Angle from the downward axis for three bonds spread 120° apart around
    /// it, such that any two make `angle` degrees: cos²α = (2·cosθ + 1) / 3.
    /// (θ = 109.47° gives α = 70.53°, the tetrahedral layout.)
    /// </summary>
    private float PyramidAngle(float angle)
    {
        float cosTheta = Mathf.Cos(angle * Mathf.Deg2Rad);
        return Mathf.Acos(Mathf.Sqrt((2f * cosTheta + 1f) / 3f)) * Mathf.Rad2Deg;
    }

    /// <summary>`count` unit vectors spread evenly around the downward axis.</summary>
    private Vector3[] Ring(int count, float fromDownDeg)
    {
        Vector3[] r = new Vector3[count];
        float a = fromDownDeg * Mathf.Deg2Rad;
        for (int i = 0; i < count; i++)
        {
            float phi = i * 2f * Mathf.PI / count;
            r[i] = new Vector3(Mathf.Sin(a) * Mathf.Cos(phi), -Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(phi));
        }
        return r;
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    // Editor only: draws the bond sockets as yellow lines when the atom is selected.
    void OnDrawGizmosSelected()
    {
        Vector3[] s = ComputeSockets();
        Gizmos.color = Color.yellow;
        for (int i = 0; i < s.Length; i++)
        {
            Vector3 dir = transform.TransformDirection(s[i]);
            Gizmos.DrawLine(transform.position, transform.position + dir * GetRadius() * 2f);
        }
    }
#endif
}
