/*

[ ] Branch structures
    [ ] If/else
    [ ] While
    [ ] For? some other simple looping mechanism? even necessary?
[ ] Operator expressions
    [ ] Infix ops
    [ ] Prefix ops
[ ] Tagged unions
    [ ] Enum/union syntax
    [ ] Switch statements

*/

// TODO(local): compile `main` into `_laye_start`, overwrite the entry point with actual startup logic to turn this into a string slice
void main(i32 argc, u8 readonly[*][*] argv)
{
    printf("process invoked with the following arguments:%c", 10);
    printf("  %p%c", argv, 10);
}
