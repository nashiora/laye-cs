/*

// X finished
// ! in progress

[ ] Operator expressions
    [!] Infix ops (+, -, *, /, %, <<, >>, &, |, ~, <, >, ==, !=, <=, >=, and, or)
    [!] Prefix ops (&, *, -, not)
[ ] Structure operations
    [ ] Size of structure `sizeof(Type)`
    [ ] Offset of structure element (optional?) `offsetof(Type, field_name)`
[ ] Branch structures
    [ ] If/else `if (expr) { } else if (expr) { } else { }
    [ ] While `while (expr) { } else { }`
    [ ] C-style For `for (binding or expr; expr; expr) { } else { }`
    [ ] Switch statements `switch (expr) { case Constant: <scoped exprs, no break needed> default: <same> }`
[ ] Tagged unions
    [ ] Enum syntax `enum EnumName { VariantName }` `enum EnumName { VariantName(int VariantField) }`
    [ ] Switch statement support for enums `switch (expr) { case EnumName::VariantName: <scoped exprs, no break needed> case ::VariantName <same> default: <same> }`
    [ ] Union syntax (optional) `enum EnumName { VariantName }` `enum EnumName { VariantName(int VariantField) }`
    [ ] Switch statement support for unions (optional) `switch (expr) { case UnionName::VariantName: <scoped exprs, no break needed> default: <same> }`

*/

// TODO(local): compile `main` into `_laye_start`, overwrite the entry point with actual startup logic to turn this into a string slice
void main(i32 argc, u8 readonly[*] readonly[*] argv)
{
    // program startup logic (if we keep the idea of passing the program args to main like C and friends)
    string[*] storage = memory_allocate((8 + 8) * 2); // sizeof(uint) + sizeof(rawptr) = 8 + 8 = 16 (on my machine)
    string argv0 = argv[0][:strlen(argv[0])];
    storage[0] = argv0;
    storage[1] = argv0;
    string[] args = storage[:2];
    // end program startup logic

    bool x = 1 == 1 and 2 == 3;

    printf("process invoked with the following arguments:%c", 10);
    // TODO(local): allow indexing a slice
    printf("  %.*s%c", args[1].length, args[1].data, 10);
}
