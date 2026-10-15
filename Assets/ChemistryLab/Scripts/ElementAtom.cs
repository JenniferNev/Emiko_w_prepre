using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// One atom of the chemistry lab. Holds the element's bond "sockets" (the
/// directions its covalent bonds point, built from real bond angles) and
/// snaps to a nearby atom when the player lets go of it.
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

    private Vector3[] sockets;          // bond directions, in this atom's local space
    private ElementAtom[] partners;     // the atom bonded on each socket (null = free)
    private int bondCount;

    void Start()
    {
        sockets = ComputeSockets();
        partners = new ElementAtom[sockets.Length];
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

    /// <summary>A bonded atom is carried by its partner, so it stops being a pickup.</summary>
    public void SetGrabbable(bool grabbable)
    {
        VRC_Pickup pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
        if (pickup != null) pickup.pickupable = grabbable;
    }

    // ------------------------------------------------------------------ bonding

    public override void OnDrop()
    {
        TryBondNearby();
    }

    public void TryBondNearby()
    {
        if (GetFreeSocketCount() == 0) return;

        // Largest covalent radius is ~0.26 m (Cs), so this reaches any partner.
        Collider[] hits = Physics.OverlapSphere(transform.position, GetRadius() * snapFactor + 0.5f);

        ElementAtom best = null;
        float bestGap = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            ElementAtom other = hits[i].GetComponent<ElementAtom>();
            if (other == null || other == this) continue;
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
