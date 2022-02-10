
/*
struct __laye_context
{
    rawptr nocontext(uint, rawptr) allocatorFunction;
    string invokerFileName;
    uint invokerFileLine;
}

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
