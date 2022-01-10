namespace laye;

public readonly struct SourceSpan : IEquatable<SourceSpan>, IComparable<SourceSpan>
{
    public static bool operator ==(SourceSpan left, SourceSpan right) =>  left.Equals(right);
    public static bool operator !=(SourceSpan left, SourceSpan right) => !left.Equals(right);

    public static SourceSpan Combine(SourceSpan start, SourceSpan end) => new(start.StartLocation, end.EndLocation);
    public static SourceSpan Combine(IHasSourceSpan start, IHasSourceSpan end) => new(start.SourceSpan.StartLocation, end.SourceSpan.EndLocation);

    public readonly SourceLocation StartLocation;
    public readonly SourceLocation EndLocation;

    public string SourceName => StartLocation.SourceName;

    public SourceSpan(SourceLocation location)
        : this(location, location)
    {
    }

    public SourceSpan(SourceLocation startLocation, SourceLocation endLocation)
    {
        if (startLocation.SourceName != endLocation.SourceName)
            throw new ArgumentException("Source span may only be constructed from two source locations in the same source file");

        if (startLocation.SourceIndex > endLocation.SourceIndex)
            (startLocation, endLocation) = (endLocation, startLocation);

        StartLocation = startLocation;
        EndLocation = endLocation;
    }

    public bool Equals(SourceSpan other) => other.StartLocation == StartLocation && other.EndLocation == EndLocation;
    public override bool Equals(object? obj) => obj is SourceSpan other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(StartLocation, EndLocation);

    public int CompareTo(SourceSpan other) => StartLocation.CompareTo(other.StartLocation);
}
