
struct test_struct
{
    int a;
    readonly int b;
}

struct __laye_context
{
    rawptr nocontext(uint, rawptr) allocatorFunction;
    string invokerFileName;
    uint invokerFileLine;
}

/*
rawptr nocontext default_allocator(uint size, rawptr memory)
{
    if (size > 0)
    {
        if (memory != nullptr)
            return realloc(memory, size);

        return malloc(size);
    }

    free(memory);
    return nullptr;
}
*/

void main()
{
    __laye_context lcontext;

    u8 readonly[*] test = GetCommandLineA();
    u8[*] testcpy = malloc(strlen(test));
    memcpy(testcpy, test, strlen(test));
    testcpy[0] = 65;

    printf("test: %s%c", testcpy, 10);

    string args = system_command_line_get();
    printf("process invoked with the following arguments:%c%.*s%c", 10, args.length, args.data, 10);
}
