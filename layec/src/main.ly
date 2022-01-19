
//extern nocontext ctype(int) printf(ctype(char) *format, varargs);
//extern nocontext cint printf(cchar *format, varargs);
//extern nocontext i32 printf(u8 *format, varargs);
//extern i32 printf(u8 *format, varargs);

i32 putchar(i32 c);
i32 printf(u8 *format, varargs);

void main()
{
    printf("Hello, hunter!");
    putchar(10);
}
