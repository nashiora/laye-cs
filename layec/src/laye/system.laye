/*
__header
{
    __impl "win32/system_win32.ly" // if __SYS == "windows"
//    __impl "win32/system_linux.ly" if __SYS == "linux"
}

string system_command_line_get();
//*/

/* // ===== WIN32 implementation =====

string system_command_line_get()
{
    // get the command line arguments from Windows for now
    u8 readonly[*] cmdarg = GetCommandLineA();
    return cmdarg[:strlen(cmdarg)];
}

//*/ // ===== END WIN32 IMPLEMENTATION =====
