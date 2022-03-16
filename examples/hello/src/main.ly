using laye;

namespace hello;

#[program_entry]
noreturn main()
{
    console::write_string("Hello, hunter!\n");
}

#[if target.os == ::windows]
namespace win32
{
    const rawptr INVALID_HANDLE_VALUE = cast(rawptr) -1;

    const i32 STD_OUTPUT_HANDLE = -11;

    foreign "WriteConsoleA" callconv(stdcall)
    i32 write_console( rawptr hConsoleOutput
                     , u8 readonly[*] lpBuffer
                     , i32 nNumberOfCharsToWrite
                     , i32 writeonly* lpNumberOfCharsWritten
                     , rawptr lpReserved);
    
    foreign "GetStdHandle" callconv(stdcall)
    rawptr get_std_handle(i32 nStdHandle);
}

#[if target.os == ::windows]
namespace laye::console
{
    void write_string(string message)
    {
        rawptr stdoutHandle = global::win32::get_std_handle(global::win32::STD_OUTPUT_HANDLE);
        if (stdoutHandle == nullptr or stdoutHandle == global::win32::INVALID_HANDLE_VALUE)
            return;

        i32 nCharsWritten;
        i32 writeResult = global::win32::write_console(stdoutHandle, message.data, cast(i32) message.length, &nCharsWritten, nullptr);
    }
}

namespace laye::memory
{
    rawptr? allocate(uint size) => context.allocator(context.allocatorUserdata, nullptr, size);
    rawptr? reallocate(rawptr? memory, uint size) => context.allocator(context.allocatorUserdata, memory, size);
    void deallocate(rawptr? memory) => context.allocator(context.allocatorUserdata, memory, 0);

    callconv(nocontext) rawptr? default_allocator(rawptr? userdata, rawptr? memory, uint size)
    {
        if (size == 0)
        {
            if (memory == nullptr)
                return malloc(size);

            free(memory);
            return nullptr;
        }

        return realloc(memory, size);
    }

    struct __context_invoker_info
    {
        string fileUri;
        string functionName;
        uint line;
    }

    struct __laye_context
    {
        callconv(nocontext) rawptr? (rawptr?, rawptr?, uint) allocator;
        rawptr? allocatorUserdata;
        __context_invoker_info invoker;
    }
}
