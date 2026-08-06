
namespace Goo;

/// <summary>Compile-time constraint for blob struct types. Never use as storage, return, or parameter type (boxes the struct, destroys per-Rebuild allocation profile); only valid in where T : struct, IBlob.</summary>
public interface IBlob
{
    static abstract BlobKind Kind { get; }
    string? Key { get; }
    internal void WriteTo(ref Frame frame);
}

/// <summary>Returns the single root Blob for a GooView build. A named delegate rather than
/// Func&lt;IBlob&gt; because IBlob has a static-abstract member (Kind) and C# bars such interfaces
/// as generic type arguments (CS8920). Consequence for Razor markup: a bare method group cannot
/// bind (its natural type is the illegal Func&lt;IBlob&gt;), so write
/// <c>Build=@(new BlobBuilder(MyBuild))</c>. See docs/site/docs/gotchas.md.</summary>
public delegate IBlob BlobBuilder();
