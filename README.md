# Laye

Laye is a modern general purpose programming language aiming to provide a C-like experience with additional low- and zero-cost language features.

# Laye as a C Compiler

Since Laye is designed to interface easily with C, you can use Laye as a C compiler. The `layec` compiler has two C compilation modes for different purposes. The compiler natively accepts C source files (just how it would .ly Laye source files) and compiles them into the same internal IR that Laye uses. Using this feature means you can compile C code to the same targets that Laye supports by default. For convenience, the `layec cc` command is provided which will invoke other installed compilers to compile C directly as those compilers would. For example, if clang is installed and visible to the `layec` executable then clang will be the compiler used. You can control this behavior and pass additional arguments with various flags.

```
# Use the Laye toolchain to compile a C source file
layec hello.c

# Use the first detected C compiler to compile a C source file
layec cc hello.c
```

(As this feature is built out, more example will be provided to illustrate the various uses.)
