using System.Numerics;

namespace Content.Shared.DeadSpace.MangoSpiderBoss.Systems;

public sealed class FabrikChain
{
    public readonly Vector2[] Joints;
    private readonly float[] _lengths;

    public readonly float Total;
    public int MaxIterations = 8;
    public float Tolerance = 0.001f;

    public Vector2 End => Joints[^1];

    public FabrikChain(Vector2 root, Vector2 dir, float[] lengths)
    {
        _lengths = lengths;
        Joints = new Vector2[lengths.Length + 1];
        Joints[0] = root;

        dir = DirOrDefault(dir);
        for (var i = 0; i < lengths.Length; i++)
        {
            Joints[i + 1] = Joints[i] + dir * lengths[i];
            Total += lengths[i];
        }
    }

    public void Solve(Vector2 root, Vector2 target, float bendSign = 0f)
    {
        var n = Joints.Length;

        Joints[0] = root;
        ForwardPass();

        var toTarget = target - root;
        var dist = toTarget.Length();
        if (dist >= Total)
        {
            var dir = dist > 1e-6f ? toTarget / dist : new Vector2(1f, 0f);
            for (var i = 1; i < n; i++)
                Joints[i] = Joints[i - 1] + dir * _lengths[i - 1];
            return;
        }

        if (bendSign != 0f)
            ApplyBend(bendSign);

        for (var iter = 0; iter < MaxIterations; iter++)
        {
            if (Vector2.Distance(End, target) < Tolerance)
                break;

            Joints[n - 1] = target;
            for (var i = n - 2; i >= 0; i--)
                Joints[i] = Joints[i + 1] + DirOrDefault(Joints[i] - Joints[i + 1]) * _lengths[i];

            Joints[0] = root;
            ForwardPass();

            if (bendSign != 0f)
                ApplyBend(bendSign);
        }
    }

    private void ForwardPass()
    {
        for (var i = 1; i < Joints.Length; i++)
            Joints[i] = Joints[i - 1] + DirOrDefault(Joints[i] - Joints[i - 1]) * _lengths[i - 1];
    }

    private void ApplyBend(float bendSign)
    {
        for (var i = 1; i < Joints.Length - 1; i++)
        {
            var a = Joints[i - 1];
            var b = Joints[i + 1];
            var line = b - a;
            if (line.LengthSquared() < 1e-8f)
                continue;

            var v = Joints[i] - a;
            var cross = line.X * v.Y - line.Y * v.X;

            if (MathF.Abs(cross) < 1e-5f)
            {
                var left = Vector2.Normalize(new Vector2(-line.Y, line.X));
                Joints[i] += left * bendSign * 0.05f * _lengths[i - 1];
            }
            else if (cross * bendSign < 0f)
            {
                var d = Vector2.Normalize(line);
                Joints[i] = a + 2f * d * Vector2.Dot(v, d) - v;
            }
        }
    }

    private static Vector2 DirOrDefault(Vector2 v)
    {
        var len = v.Length();
        return len > 1e-6f ? v / len : new Vector2(1f, 0f);
    }
}