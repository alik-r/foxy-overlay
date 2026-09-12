using System;


namespace FoxyOverlay.Core.Packs;

/// <summary>Raised when a pack directory is missing, malformed, or incomplete.</summary>
public sealed class PackException : Exception
{
    public PackException(string message) : base(message) { }
    public PackException(string message, Exception inner) : base(message, inner) { }
}
