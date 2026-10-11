using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;
using Content.Shared.DeadSpace.MangoSpiderBoss.Components;
using Robust.Shared.Player;
using System.Numerics;

namespace Content.Shared.DeadSpace.MangoSpiderBoss.Systems;

public sealed class SpiderMarkerFollowSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SpiderSetTargetEvent>(OnSetTarget);
        SubscribeNetworkEvent<SpiderStartRotationEvent>(OnStartRotation);
        SubscribeNetworkEvent<SpiderStopRotationEvent>(OnStopRotation);

        SubscribeLocalEvent<SpiderMarkerFollowComponent, PlayerDetachedEvent>(OnDetached);
    }

    private void OnSetTarget(SpiderSetTargetEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } spider ||
            !TryComp<SpiderMarkerFollowComponent>(spider, out var follow))
            return;

        var coords = GetCoordinates(msg.Target);
        if (!coords.IsValid(EntityManager))
            return;

        var map = _xform.ToMapCoordinates(coords);
        if (map.MapId != Transform(spider).MapID)
            return;

        follow.Target = map;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SpiderMarkerFollowComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var follow, out var body, out var xform))
        {
            var current = _xform.GetWorldRotation(xform);

            var hasTarget = false;
            var moveDir = Vector2.Zero;
            var delta = 0f;
            var worldPos = _xform.GetWorldPosition(xform);

            if (follow.Target is { } target)
            {
                if (target.MapId != xform.MapID)
                {
                    follow.Target = null;
                }
                else
                {
                    var toTarget = target.Position - worldPos;

                    if (toTarget.Length() < follow.StopDistance)
                    {
                        follow.Target = null;
                    }
                    else
                    {
                        hasTarget = true;
                        moveDir = Vector2.Normalize(toTarget);
                        delta = (float)Angle.ShortestDistance(current, toTarget.ToWorldAngle()).Theta;
                    }
                }
            }

            var manual = follow.RotateManually;
            var autoTurn = !manual && follow.AutoTurnToTarget && hasTarget;
            float desired;

            if (manual)
            {
                desired = (follow.RotateRight ? -1f : 1f) * follow.TurnSpeed;
            }
            else if (autoTurn)
            {
                desired = Math.Clamp(delta * follow.TurnGain, -follow.TurnSpeed, follow.TurnSpeed);
            }
            else
            {
                desired = 0f;
            }

            var step = desired * frameTime;

            if (autoTurn && MathF.Abs(step) > MathF.Abs(delta))
                step = delta;

            if (MathF.Abs(step) > 1e-5f)
                _xform.SetWorldRotation(uid, current + new Angle(step));

            var velocity = hasTarget ? moveDir * follow.Speed : Vector2.Zero;
            _physics.SetLinearVelocity(uid, velocity, body: body);
        }
    }

    private void OnStartRotation(SpiderStartRotationEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } spider ||
            !TryComp<SpiderMarkerFollowComponent>(spider, out var follow))
            return;

        follow.RotateManually = true;
        follow.RotateRight = msg.Right;
    }

    private void OnStopRotation(SpiderStopRotationEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } spider ||
            !TryComp<SpiderMarkerFollowComponent>(spider, out var follow))
            return;

        follow.RotateManually = false;
    }

    private void OnDetached(Entity<SpiderMarkerFollowComponent> ent, ref PlayerDetachedEvent args)
    {
        ent.Comp.RotateManually = false;
    }
}
