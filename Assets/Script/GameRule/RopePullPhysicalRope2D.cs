using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 複数のRigidbody2D・CapsuleCollider2D・HingeJoint2Dを連結し、
/// 地面や壁へ衝突できる物理ロープを実行時に生成します。
/// PlayerRopePullControllerから生成・更新されます。
/// </summary>
[DisallowMultipleComponent]
public sealed class RopePullPhysicalRope2D : MonoBehaviour
{
    public struct Settings
    {
        public int SegmentLayer;
        public float PreferredSegmentLength;
        public int MinimumSegmentCount;
        public int MaximumSegmentCount;
        public float Thickness;
        public float SegmentMass;
        public float GravityScale;
        public float LinearDamping;
        public float AngularDamping;
        public float BreakForce;
        public PhysicsMaterial2D PhysicsMaterial;
        public Collider2D[] IgnoredColliders;
    }

    private sealed class Segment
    {
        public GameObject GameObject;
        public Transform Transform;
        public Rigidbody2D Rigidbody;
        public CapsuleCollider2D Collider;
        public HingeJoint2D StartJoint;
    }

    public bool IsActive => isActive;

    /// <summary>
    /// Jointが破断・削除されておらず、ロープとして使用できる状態です。
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (!isActive || startBody == null || endBody == null ||
                endJoint == null || !endJoint.enabled ||
                segments.Count <= 0)
            {
                return false;
            }

            foreach (Segment segment in segments)
            {
                if (segment == null || segment.GameObject == null ||
                    segment.Rigidbody == null || segment.Collider == null ||
                    segment.StartJoint == null ||
                    !segment.StartJoint.enabled)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public int VisualPointCount => IsValid ? segments.Count + 2 : 0;
    public float CurrentLength => currentLength;

    private readonly List<Segment> segments = new List<Segment>();
    private readonly List<Vector3> capturedPath = new List<Vector3>();
    private readonly List<Vector3> sampledBoundaries = new List<Vector3>();

    private Settings settings;
    private Rigidbody2D startBody;
    private Rigidbody2D endBody;
    private Vector2 startWorldAnchor;
    private Vector2 endWorldAnchor;
    private HingeJoint2D endJoint;
    private float currentLength;
    private bool isActive;

    public bool Build(
        Rigidbody2D start,
        Vector2 startAnchorWorld,
        Rigidbody2D end,
        Vector2 endAnchorWorld,
        float totalLength,
        Settings buildSettings)
    {
        DestroyRope();

        if (start == null || end == null || totalLength <= 0.01f)
        {
            return false;
        }

        settings = SanitizeSettings(buildSettings);
        startBody = start;
        endBody = end;
        startWorldAnchor = startAnchorWorld;
        endWorldAnchor = endAnchorWorld;
        currentLength = Mathf.Max(0.1f, totalLength);
        isActive = true;

        int count = CalculateSegmentCount(currentLength);
        CreateInitialSagPath(capturedPath);
        RebuildSegments(count, capturedPath);

        if (!IsValid)
        {
            DestroyRope();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Player側と対象物側の接続位置を更新します。
    /// Playerの手元位置が移動・反転しても追従します。
    /// </summary>
    public void UpdateAnchors(
        Vector2 newStartWorldAnchor,
        Vector2 newEndWorldAnchor)
    {
        startWorldAnchor = newStartWorldAnchor;
        endWorldAnchor = newEndWorldAnchor;

        if (!IsValid)
        {
            return;
        }

        Segment first = segments[0];
        first.StartJoint.connectedAnchor =
            startBody.transform.InverseTransformPoint(startWorldAnchor);

        endJoint.anchor =
            endBody.transform.InverseTransformPoint(endWorldAnchor);
    }

    /// <summary>
    /// ロープ全長を変更します。必要な時だけセグメント数を増減し、
    /// それ以外は各セグメントの長さを滑らかに変更します。
    /// </summary>
    public bool SetLength(float totalLength)
    {
        if (!isActive)
        {
            return false;
        }

        float nextLength = Mathf.Max(0.1f, totalLength);
        int desiredCount = CalculateSegmentCount(nextLength);
        bool countChanged = desiredCount != segments.Count;

        currentLength = nextLength;

        if (countChanged)
        {
            CaptureCurrentPath(capturedPath);
            RebuildSegments(desiredCount, capturedPath);
        }
        else
        {
            ApplySegmentGeometry();
        }

        return IsValid;
    }

    public float GetApproximatePathLength()
    {
        if (!IsValid)
        {
            return 0f;
        }

        float length = 0f;
        Vector2 previous = startWorldAnchor;

        foreach (Segment segment in segments)
        {
            Vector2 current = segment.Rigidbody.position;
            length += Vector2.Distance(previous, current);
            previous = current;
        }

        length += Vector2.Distance(previous, endWorldAnchor);
        return length;
    }

    public int CopyVisualPoints(Vector3[] destination)
    {
        int required = VisualPointCount;

        if (required <= 0 || destination == null ||
            destination.Length < required)
        {
            return 0;
        }

        destination[0] = startWorldAnchor;

        for (int i = 0; i < segments.Count; i++)
        {
            destination[i + 1] = segments[i].Transform.position;
        }

        destination[required - 1] = endWorldAnchor;
        return required;
    }

    public void DestroyRope()
    {
        isActive = false;

        if (endJoint != null)
        {
            endJoint.enabled = false;
            Destroy(endJoint);
            endJoint = null;
        }

        foreach (Segment segment in segments)
        {
            DisableAndDestroySegment(segment);
        }

        segments.Clear();
        capturedPath.Clear();
        sampledBoundaries.Clear();

        startBody = null;
        endBody = null;
        currentLength = 0f;
    }

    private void RebuildSegments(
        int segmentCount,
        IReadOnlyList<Vector3> sourcePath)
    {
        SamplePathBoundaries(
            sourcePath,
            segmentCount + 1,
            sampledBoundaries
        );

        if (endJoint != null)
        {
            endJoint.enabled = false;
            Destroy(endJoint);
            endJoint = null;
        }

        foreach (Segment segment in segments)
        {
            DisableAndDestroySegment(segment);
        }

        segments.Clear();

        float segmentLength = currentLength / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 boundaryA = sampledBoundaries[i];
            Vector3 boundaryB = sampledBoundaries[i + 1];
            Vector2 direction = boundaryB - boundaryA;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = endWorldAnchor - startWorldAnchor;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) *
                          Mathf.Rad2Deg;

            GameObject segmentObject = new GameObject(
                $"PhysicalRopeSegment_{i:00}"
            );

            segmentObject.layer = settings.SegmentLayer;
            segmentObject.transform.SetParent(transform, true);
            segmentObject.transform.position =
                Vector3.Lerp(boundaryA, boundaryB, 0.5f);
            segmentObject.transform.rotation =
                Quaternion.Euler(0f, 0f, angle);

            Rigidbody2D body = segmentObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = settings.SegmentMass;
            body.gravityScale = settings.GravityScale;
            body.linearDamping = settings.LinearDamping;
            body.angularDamping = settings.AngularDamping;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D capsule =
                segmentObject.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Horizontal;
            capsule.sharedMaterial = settings.PhysicsMaterial;

            HingeJoint2D startJoint =
                segmentObject.AddComponent<HingeJoint2D>();
            startJoint.autoConfigureConnectedAnchor = false;
            startJoint.enableCollision = false;
            startJoint.breakForce = settings.BreakForce > 0f
                ? settings.BreakForce
                : Mathf.Infinity;
            startJoint.breakTorque = Mathf.Infinity;

            if (i == 0)
            {
                startJoint.connectedBody = startBody;
                startJoint.connectedAnchor =
                    startBody.transform.InverseTransformPoint(
                        startWorldAnchor
                    );
            }
            else
            {
                startJoint.connectedBody =
                    segments[i - 1].Rigidbody;
                startJoint.connectedAnchor =
                    new Vector2(segmentLength * 0.5f, 0f);
            }

            Segment segment = new Segment
            {
                GameObject = segmentObject,
                Transform = segmentObject.transform,
                Rigidbody = body,
                Collider = capsule,
                StartJoint = startJoint
            };

            IgnoreConfiguredCollisions(capsule);

            foreach (Segment previous in segments)
            {
                if (previous?.Collider != null)
                {
                    Physics2D.IgnoreCollision(
                        capsule,
                        previous.Collider,
                        true
                    );
                }
            }

            segments.Add(segment);
        }

        endJoint = endBody.gameObject.AddComponent<HingeJoint2D>();
        endJoint.autoConfigureConnectedAnchor = false;
        endJoint.enableCollision = false;
        endJoint.connectedBody = segments[segments.Count - 1].Rigidbody;
        endJoint.breakForce = settings.BreakForce > 0f
            ? settings.BreakForce
            : Mathf.Infinity;
        endJoint.breakTorque = Mathf.Infinity;

        ApplySegmentGeometry();
    }

    private void ApplySegmentGeometry()
    {
        if (!isActive || segments.Count <= 0 ||
            startBody == null || endBody == null)
        {
            return;
        }

        float segmentLength = currentLength / segments.Count;
        float colliderLength = Mathf.Max(
            settings.Thickness,
            segmentLength + settings.Thickness * 0.35f
        );

        for (int i = 0; i < segments.Count; i++)
        {
            Segment segment = segments[i];

            if (segment?.Collider == null ||
                segment.StartJoint == null)
            {
                continue;
            }

            segment.Collider.size = new Vector2(
                colliderLength,
                settings.Thickness
            );

            segment.StartJoint.anchor =
                new Vector2(-segmentLength * 0.5f, 0f);

            if (i == 0)
            {
                segment.StartJoint.connectedAnchor =
                    startBody.transform.InverseTransformPoint(
                        startWorldAnchor
                    );
            }
            else
            {
                segment.StartJoint.connectedAnchor =
                    new Vector2(segmentLength * 0.5f, 0f);
            }
        }

        if (endJoint != null)
        {
            endJoint.anchor =
                endBody.transform.InverseTransformPoint(endWorldAnchor);
            endJoint.connectedAnchor =
                new Vector2(segmentLength * 0.5f, 0f);
        }
    }

    private int CalculateSegmentCount(float length)
    {
        int desired = Mathf.CeilToInt(
            length / settings.PreferredSegmentLength
        );

        return Mathf.Clamp(
            desired,
            settings.MinimumSegmentCount,
            settings.MaximumSegmentCount
        );
    }

    private void CreateInitialSagPath(List<Vector3> result)
    {
        result.Clear();

        const int pointCount = 17;
        Vector2 start = startWorldAnchor;
        Vector2 end = endWorldAnchor;
        float directDistance = Vector2.Distance(start, end);
        float extraLength = Mathf.Max(0f, currentLength - directDistance);

        float geometricSag = 0.5f * Mathf.Sqrt(
            Mathf.Max(
                0f,
                currentLength * currentLength -
                directDistance * directDistance
            )
        );

        float sag = Mathf.Max(extraLength * 0.65f, geometricSag * 0.7f);
        sag = Mathf.Min(sag, currentLength * 0.45f);

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            point += Vector3.down *
                (Mathf.Sin(Mathf.PI * t) * sag);
            result.Add(point);
        }
    }

    private void CaptureCurrentPath(List<Vector3> result)
    {
        result.Clear();
        result.Add(startWorldAnchor);

        foreach (Segment segment in segments)
        {
            if (segment?.Transform != null)
            {
                result.Add(segment.Transform.position);
            }
        }

        result.Add(endWorldAnchor);

        if (result.Count < 2)
        {
            CreateInitialSagPath(result);
        }
    }

    private static void SamplePathBoundaries(
        IReadOnlyList<Vector3> source,
        int outputCount,
        List<Vector3> output)
    {
        output.Clear();

        if (source == null || source.Count < 2)
        {
            return;
        }

        float totalLength = 0f;
        float[] cumulative = new float[source.Count];

        for (int i = 1; i < source.Count; i++)
        {
            totalLength += Vector3.Distance(source[i - 1], source[i]);
            cumulative[i] = totalLength;
        }

        if (totalLength <= 0.0001f)
        {
            for (int i = 0; i < outputCount; i++)
            {
                output.Add(source[0]);
            }

            return;
        }

        for (int outputIndex = 0; outputIndex < outputCount; outputIndex++)
        {
            float normalized = outputCount <= 1
                ? 0f
                : outputIndex / (float)(outputCount - 1);

            float targetDistance = totalLength * normalized;
            int segmentIndex = 1;

            while (segmentIndex < cumulative.Length - 1 &&
                   cumulative[segmentIndex] < targetDistance)
            {
                segmentIndex++;
            }

            float previousDistance = cumulative[segmentIndex - 1];
            float nextDistance = cumulative[segmentIndex];
            float localT = Mathf.InverseLerp(
                previousDistance,
                nextDistance,
                targetDistance
            );

            output.Add(Vector3.Lerp(
                source[segmentIndex - 1],
                source[segmentIndex],
                localT
            ));
        }
    }

    private void IgnoreConfiguredCollisions(Collider2D segmentCollider)
    {
        if (segmentCollider == null || settings.IgnoredColliders == null)
        {
            return;
        }

        foreach (Collider2D ignored in settings.IgnoredColliders)
        {
            if (ignored != null && ignored != segmentCollider)
            {
                Physics2D.IgnoreCollision(
                    segmentCollider,
                    ignored,
                    true
                );
            }
        }
    }

    private static Settings SanitizeSettings(Settings value)
    {
        value.SegmentLayer = Mathf.Clamp(value.SegmentLayer, 0, 31);
        value.PreferredSegmentLength = Mathf.Max(
            0.08f,
            value.PreferredSegmentLength
        );
        value.MinimumSegmentCount = Mathf.Clamp(
            value.MinimumSegmentCount,
            2,
            64
        );
        value.MaximumSegmentCount = Mathf.Clamp(
            value.MaximumSegmentCount,
            value.MinimumSegmentCount,
            64
        );
        value.Thickness = Mathf.Max(0.01f, value.Thickness);
        value.SegmentMass = Mathf.Max(0.001f, value.SegmentMass);
        value.GravityScale = Mathf.Max(0f, value.GravityScale);
        value.LinearDamping = Mathf.Max(0f, value.LinearDamping);
        value.AngularDamping = Mathf.Max(0f, value.AngularDamping);
        value.BreakForce = Mathf.Max(0f, value.BreakForce);
        return value;
    }

    private static void DisableAndDestroySegment(Segment segment)
    {
        if (segment == null)
        {
            return;
        }

        if (segment.StartJoint != null)
        {
            segment.StartJoint.enabled = false;
        }

        if (segment.Collider != null)
        {
            segment.Collider.enabled = false;
        }

        if (segment.Rigidbody != null)
        {
            segment.Rigidbody.simulated = false;
        }

        if (segment.GameObject != null)
        {
            Destroy(segment.GameObject);
        }
    }

    private void OnDestroy()
    {
        DestroyRope();
    }
}
