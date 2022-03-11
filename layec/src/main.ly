/*

// X finished
// ! in progress
// T needs testing, but looks to be implemented

[ ] Full type implementations
    [ ] Rune type (and basic string UTF8 reading associated with it)
[ ] Operator expressions
    [T] Infix ops (+, -, *, /, %, <<, >>, &, |, ~, <, >, ==, !=, <=, >=, and, or)
    [T] Prefix ops (&, *, -, not)
    [X] Explicit type casts
[ ] Structure operations
    [X] Size of structure `sizeof(Type)`
    [ ] Offset of structure element (optional?) `offsetof(Type, field_name)`
    [ ] Structural initializers
    [X] Zero initializers
[ ] Branch structures
    [X] If/else `if (expr) { } else if (expr) { } else { }
    [X] While `while (expr) { } else { }`
    [ ] C-style For `for (binding or expr; expr; expr) { } else { }`
    [ ] Switch statements `switch (expr) { case Constant: <scoped exprs, no break needed> default: <same> }`
[ ] Tagged unions
    [X] Enum syntax `enum EnumName { VariantName }` `enum EnumName { VariantName(int VariantField) }`
    [ ] Switch statement support for enums `switch (expr) { case EnumName::VariantName: <scoped exprs, no break needed> case ::VariantName <same> default: <same> }`
    [X] Union syntax (optional) `enum EnumName { VariantName }` `enum EnumName { VariantName(int VariantField) }`
    [ ] Switch statement support for unions (optional) `switch (expr) { case UnionName::VariantName: <scoped exprs, no break needed> default: <same> }`

*/

// TODO(local): compile `main` into `_laye_start`, overwrite the entry point with actual startup logic to turn this into a string slice
void main(i32 argc, u8 readonly[*] readonly[*] argv)
{
    // program startup logic (if we keep the idea of passing the program args to main like C and friends)
    string[*] argvStorage = memory_allocate(sizeof(string) * argc);
    uint argvCounter = 0;
    while (argvCounter < argc)
    {
        argvStorage[argvCounter] = argv[argvCounter][:strlen(argv[argvCounter])];
        argvCounter = argvCounter + 1;
    }
    string[] args = argvStorage[:argc];
    // end program startup logic

    laye_main(args);
}

void laye_main(string[] args)
{
    printf("Laye stand-alone compiler%cVersion 0.1.0%c%c", 10, 10, 10);

    /*
    {
        printf("invoked with the following arguments:%c", 10);

        uint i = 0;
        while (i < args.length)
        {
            printf("  %.*s%c", args[i].length, args[i].data, 10);
            i = i + 1;
        }
    }
    */

    if (args.length < 2)
    {
        printf("error: no file given%c", 10);
        return;
    }

    diagnostic_bag diagnostics;

    string sourceFilePath = args[1];
    source sourceFile = source_create_from_file(sourceFilePath);
    if (not sourceFile.isValid)
    {
        printf("failed to open file%c", 10);
        return;
    }

    parser_data layeParser;
    parser_init(&layeParser, sourceFile, &diagnostics);

    syntax_node *[dynamic] nodes;
    bool success = parser_read_all_laye_nodes(&layeParser, &nodes);

    if (success)
        printf("read %llu top level nodes from file successfully%c", nodes.length, 10);
    //else printf("failed to read top level nodes from file%c", 10);

    diagnostics_print(diagnostics);
    
    /*
    syntax_token[] sourceTokens = lexer_read_laye_tokens(sourceFile, &diagnostics);

    printf("read %llu tokens from file successfully%c", sourceTokens.length, 10);
    diagnostics_print(diagnostics);

    if (false)
    {
        uint i = 0;
        while (i < sourceTokens.length)
        {
            syntax_token token = sourceTokens[i];

            string tokenLocationString = source_location_to_string(token.sourceSpan.startLocation);
            string tokenString = source_span_to_string(token.sourceSpan);
            string tokenKindString = nameof_variant(token.kind);

            printf("> token %.*s (%.*s) %.*s%c", tokenKindString.length, tokenKindString.data,
                tokenLocationString.length, tokenLocationString.data,
                tokenString.length, tokenString.data,
                10);

            i = i + 1;
        }
    }
    // */
}

void diagnostics_print(diagnostic_bag b)
{
    uint count = b.diagnostics.length;

    {
        uint i = 0;
        while (i < count)
        {
            diagnostic d = b.diagnostics[i];

            string locationString = source_location_to_string(diagnostic_source_span_get(d).startLocation);
            string kindString = nameof_variant(d.kind);

            printf("%.*s: %.*s: %.*s%c",
                locationString.length, locationString.data,
                kindString.length, kindString.data,
                d.message.length, d.message.data,
                10);

            i = i + 1;
        }
    }
}
