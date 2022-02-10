namespace laye;

internal sealed class DebugPrettyPrinter
{
    private static IEnumerable<IHasSourceSpan> GetChildren(IHasSourceSpan node)
    {
        var properties = node.GetType().GetProperties();
        foreach (var property in properties)
        {
            var propertyType = property.PropertyType;
            if (propertyType.IsAssignableTo(typeof(IHasSourceSpan)))
            {
                var child = (IHasSourceSpan?)property.GetValue(node);
                if (child is not null)
                    yield return child;
            }
            else if (propertyType.IsAssignableTo(typeof(IEnumerable<IHasSourceSpan>)))
            {
                var container = (IEnumerable<IHasSourceSpan>?)property.GetValue(node);
                if (container is not null)
                {
                    foreach (var child in container)
                    {
                        if (child is not null)
                            yield return child;
                    }
                }
            }
        }
    }

    public string SourceText { get; }

    public DebugPrettyPrinter(string sourceText)
    {
        SourceText = sourceText;
    }

    public void PrettyPrint(LayeAstRoot root)
    {
        var children = root.TopLevelNodes.OrderBy(n => n.SourceSpan);
        if (!children.Any())
            return;

        var lastChild = children.Last();
        foreach (var child in children)
            PrettyPrint(child, "", child == lastChild);

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();
    }

    private void PrettyPrint(IHasSourceSpan node, string indent, bool isLast)
    {
        Console.ForegroundColor = ConsoleColor.Gray;

        string marker = isLast ? "└──" : "├──";

        Console.Write(indent);
        Console.Write(marker);

        if (node is LayeToken)
            Console.ForegroundColor = ConsoleColor.Yellow;
        else Console.ForegroundColor = ConsoleColor.Cyan;

        string typeName = node.GetType().Name;
        //if (typeName.StartsWith("LayeAst")) typeName = typeName[7..].AddSpaceToCamelCase();

        Console.Write(typeName);

        Console.ForegroundColor = ConsoleColor.Gray;

        if (node is LayeToken token)
        {
            string tokenString = token.SourceSpan.ToString(SourceText);
            if (!string.IsNullOrWhiteSpace(tokenString))
            {
                Console.Write("  ");
                Console.Write(tokenString);
            }
        }

        Console.WriteLine();

        indent += isLast ? "   " : "│  ";

        var children = GetChildren(node).OrderBy(n => n.SourceSpan);
        if (!children.Any())
            return;

        var lastChild = children.Last();
        foreach (var child in children)
            PrettyPrint(child, indent, child == lastChild);
    }
}
