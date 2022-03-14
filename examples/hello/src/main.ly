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

namespace laye
{

    rawptr? memory_allocate(uint size)
    {
        return context.allocator(nullptr, size);
    }
    
    /*

    // alias allocator_function = callconv(nocontext) rawptr? (rawptr? pointer, uint size);

    struct __laye_context
    {
        callconv(nocontext) rawptr? (rawptr? pointer, uint size) allocator;

        __context_invoker_info invoker;
    }

    rawptr? memory_allocate(uint size)
    {
        return context.allocator(nullptr, size);
    }

    void memory_free(rawptr? memory)
    {
        if (memory != nullptr) context.allocator(memory, 0);
    }

    rawptr? memory_reallocate(rawptr? memory, uint size)
    {
        return context.allocator(memory, size);
    }

    struct __context_invoker_info
    {
        string fileUri;
        string functionName;
        uint line;
    }

    */
}
