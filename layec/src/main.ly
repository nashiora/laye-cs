
// for now, we assume that bodyless functions are extern
// we also assume all functions are nocontext for now, as I have no plans for v1 to have a context system anymore
// ALSO for now, no plans to support specific C types that have the same semantic since the C support will come later as well

i32 putchar(i32 c);
i32 puts(u8 [*]format);
//i32 printf(u8 [*]format, varargs);

void main()
{
    puts("Hello, hunter!");
    //putchar(10);
}
