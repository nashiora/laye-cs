extern "C" i32 printf(u8 readonly [*]format, varargs);

void main()
{
	printf("Hello, hunter!%c", 10);
}
