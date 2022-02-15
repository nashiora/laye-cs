
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

void panic(string message)
{
    printf("%.*s%c", message.length, message.data, 10);
    abort();
}

void assert(bool test, string message)
{
    if (not test) panic(string_concat("assertion fail: ", message));
}

uint uint_num_digits(uint n)
{
    if (n < 10) return 1;
    else if (n < 100) return 2;
    else if (n < 1000) return 3;
    else if (n < 10000) return 4;
    else if (n < 100000) return 5;
    else if (n < 1000000) return 6;
    else if (n < 10000000) return 7;
    else if (n < 100000000) return 8;
    else if (n < 1000000000) return 9;
    else if (n < 10000000000) return 10;
    else if (n < 100000000000) return 11;
    else if (n < 1000000000000) return 12;
    else if (n < 10000000000000) return 13;
    else if (n < 100000000000000) return 14;
    else if (n < 1000000000000000) return 15;
    else if (n < 10000000000000000) return 16;
    else if (n < 100000000000000000) return 17;
    else if (n < 1000000000000000000) return 18;
    else return 19;
}

uint int_num_digits(int n)
{
    if (n < 0)
    {
        // n = (n == INT_MIN) ? INT_MAX : -n;
        if (n == -9223372036854775808)
            n = 9223372036854775807;
        else n = -n;
    }

    if (n < 10) return 1;
    else if (n < 100) return 2;
    else if (n < 1000) return 3;
    else if (n < 10000) return 4;
    else if (n < 100000) return 5;
    else if (n < 1000000) return 6;
    else if (n < 10000000) return 7;
    else if (n < 100000000) return 8;
    else if (n < 1000000000) return 9;
    else if (n < 10000000000) return 10;
    else if (n < 100000000000) return 11;
    else if (n < 1000000000000) return 12;
    else if (n < 10000000000000) return 13;
    else if (n < 100000000000000) return 14;
    else if (n < 1000000000000000) return 15;
    else if (n < 10000000000000000) return 16;
    else if (n < 100000000000000000) return 17;
    else if (n < 1000000000000000000) return 18;
    else return 19;
}
