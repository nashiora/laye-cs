/*

// X finished
// ! in progress
// T needs testing, but looks to be implemented

[T] Operator expressions
    [T] Infix ops (+, -, *, /, %, <<, >>, &, |, ~, <, >, ==, !=, <=, >=, and, or)
    [T] Prefix ops (&, *, -, not)
[ ] Structure operations
    [ ] Size of structure `sizeof(Type)`
    [ ] Offset of structure element (optional?) `offsetof(Type, field_name)`
[ ] Branch structures
    [X] If/else `if (expr) { } else if (expr) { } else { }
    [X] While `while (expr) { } else { }`
    [ ] C-style For `for (binding or expr; expr; expr) { } else { }`
    [ ] Switch statements `switch (expr) { case Constant: <scoped exprs, no break needed> default: <same> }`
[ ] Tagged unions
    [ ] Enum syntax `enum EnumName { VariantName }` `enum EnumName { VariantName(int VariantField) }`
    [ ] Switch statement support for enums `switch (expr) { case EnumName::VariantName: <scoped exprs, no break needed> case ::VariantName <same> default: <same> }`
    [ ] Union syntax (optional) `enum EnumName { VariantName }` `enum EnumName { VariantName(int VariantField) }`
    [ ] Switch statement support for unions (optional) `switch (expr) { case UnionName::VariantName: <scoped exprs, no break needed> default: <same> }`

*/

struct test
{
    uint value;
}

// TODO(local): compile `main` into `_laye_start`, overwrite the entry point with actual startup logic to turn this into a string slice
void main(i32 argc, u8 readonly[*] readonly[*] argv)
{
    // program startup logic (if we keep the idea of passing the program args to main like C and friends)
    /*
    string[*] storage = memory_allocate((8 + 8) * 2); // sizeof(uint) + sizeof(rawptr) = 8 + 8 = 16 (on my machine)
    string argv0 = argv[0][:strlen(argv[0])];
    storage[0] = argv0;
    storage[1] = argv0;
    string[] args = storage[:2];
    /*/
    string[*] argvStorage = memory_allocate((8 + 8) * argc);
    uint argvCounter = 0;
    while (argvCounter < argc)
    {
        argvStorage[argvCounter] = argv[argvCounter][:strlen(argv[argvCounter])];
        argvCounter = argvCounter + 1;
    }
    string[] args = argvStorage[:argc];
    //*/
    // end program startup logic

    test t;
    t.value = 10;
    printf("t.value = %d%c", t.value, 10);

    uint test_size = sizeof(test);
    uint test_size2 = sizeof(t);

    printf("sizeof(test) == %llu, sizeof(t) == %llu%c", test_size, test_size2, 10);

    if (1 == 1.0) printf("Hello, hunter!%c", 10);
    else printf("Hello, world!%c", 10);

    printf("process invoked with the following arguments:%c", 10);
    // TODO(local): allow indexing a slice
    printf("  %.*s%c", args[0].length, args[0].data, 10);
}
