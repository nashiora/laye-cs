namespace laye;

public readonly struct SourceLocation : IEquatable<SourceLocation>, IComparable<SourceLocation>
{
    public static bool operator ==(SourceLocation left, SourceLocation right) =>  left.Equals(right);
    public static bool operator !=(SourceLocation left, SourceLocation right) => !left.Equals(right);

    public readonly string SourceName;
    public readonly uint SourceIndex;

    public readonly uint Line;
    public readonly uint Column;

    public SourceLocation(string sourceName, uint sourceIndex, uint line, uint column)
    {
        SourceName = sourceName;
        SourceIndex = sourceIndex;

        Line = line;
        Column = column;
    }

    public override string ToString() => $"{SourceName}:{Line}:{Column}";

    public bool Equals(SourceLocation other) => other.SourceName == SourceName && SourceIndex == other.SourceIndex && other.Line == Line && other.Column == Column;
    public override bool Equals(object? obj) => obj is SourceLocation other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(SourceName, SourceIndex);

    public int CompareTo(SourceLocation other)
    {
        if (other.SourceName != SourceName)
            return SourceName.CompareTo(other.SourceName);
        else return SourceIndex.CompareTo(other.SourceIndex);
    }
}
