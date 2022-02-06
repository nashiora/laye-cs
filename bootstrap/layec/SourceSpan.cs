namespace laye;

public readonly struct SourceSpan : IEquatable<SourceSpan>, IComparable<SourceSpan>
{
    public static bool operator ==(SourceSpan left, SourceSpan right) =>  left.Equals(right);
    public static bool operator !=(SourceSpan left, SourceSpan right) => !left.Equals(right);

    public static SourceSpan Combine(SourceSpan start, SourceSpan end)
    {
        if (start == Invalid)
            return end;
        else if (end == Invalid)
            return start;

        return new(start.StartLocation, end.EndLocation);
    }

    //public static SourceSpan Combine(IHasSourceSpan start, IHasSourceSpan end) => Combine(start.SourceSpan, end.SourceSpan);
    public static SourceSpan Combine(params IHasSourceSpan?[] values)
    {
        var spans = values.Where(s => s is not null && s.SourceSpan != Invalid).Cast<IHasSourceSpan>();
        if (!spans.Any()) throw new ArgumentException("no non-null source spans", nameof(values));
        return new SourceSpan(spans.Select(s => s.SourceSpan.StartLocation).Min()!, spans.Select(s => s.SourceSpan.EndLocation).Max()!);
    }

    public static readonly SourceSpan Invalid = new(SourceLocation.Invalid);

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

    public override string ToString() => $"{SourceName}:{StartLocation.Line}:{StartLocation.Column}";
    public string ToString(string sourceText) => sourceText[(int)StartLocation.SourceIndex..(int)EndLocation.SourceIndex];

    public bool Equals(SourceSpan other) => other.StartLocation == StartLocation && other.EndLocation == EndLocation;
    public override bool Equals(object? obj) => obj is SourceSpan other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(StartLocation, EndLocation);

    public int CompareTo(SourceSpan other) => StartLocation.CompareTo(other.StartLocation);
}
