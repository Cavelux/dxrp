using System;
using Sandbox;

namespace Goo;

// The single active drag for one interaction space. Pure: no Panel dependency, fully unit-tested.
public sealed class DragContext<T>
{
    public bool IsDragging { get; private set; }
    public bool HasPayload { get; private set; }
    public T Payload { get; private set; } = default!;
    public Vector2 Pos { get; private set; }
    public Func<Container>? Ghost { get; private set; }

    public void Begin(T payload, Func<Container>? ghost, Vector2 pos)
    {
        Payload = payload;
        Ghost = ghost;
        Pos = pos;
        IsDragging = true;
        HasPayload = true;
    }

    public void Move(Vector2 pos) => Pos = pos;

    public void Release() => IsDragging = false;

    public void Complete()
    {
        IsDragging = false;
        HasPayload = false;
        Payload = default!;
        Ghost = null;
    }
}
