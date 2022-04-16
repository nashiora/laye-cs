/*
 * The `rationale` example project is where I (Local) will be including well-commented examples
 * of various hard language and library design choices and comparing them with other languages
 * and as many use cases as I can think of.
 * 
 * While Laye is a language I'm developing almost exclusively for myself and my enjoyment of programming,
 * part of that enjoyment is making my stance on many programming issues real and testing their
 * effectiveness in a real programming language.
 * 
 * I'm very interested in lower level development like drivers, operating systems or micro-controllers
 * but do not actively develop any of those. I'm a higher level applications developer almost exclusively
 * but I highly value knowing more about the inner workings of the tools you use and the platforms you are
 * targetting so Laye is a way for me to learn about and apply my theories on lower level programs in a
 * way that I enjoy and am more familiar with.
 */

/* ===== Program Entry and Exit =====
 * 
 * The first versions of Laye do what C does: command line arguments are passed to `main` if requested.
 * This is a feature supported early on primarily for two reasons: Many languages do this already, so it's
 * familiar, and conditional compilation is not supported in the early stages of development.
 * The first is fairly straightforward and is grounds for maintaining the feature in later versions of the language.
 * The second is the real limitting factor, in that we can't provide a single standardized library call to select
 * a platform function to retrieve the command line arguments (like GetCommandLineA on Windows.)
 * 
 * The early versions of Laye do rely on the C standard library, though, so the return type can always be `void`
 * and any nonzero exit code can be provided via the `exit` library function. This functionality will be provided
 * in the Laye core or standard library in the future and will even have the `noreturn` annotation (whatever that
 * turns into) to have full semantic information given to the compiler without special treatment in optimizing stages.
 * 
 * Speaking of the `exit` library function and the `noreturn` annotation, this means that the entry point function
 * may also be required to be marked as `noreturn` since the entry of a program does not have a place to return to
 * and must call the `exit` library function to exit. It may implicitly call `exit(0)` if any path reaches the end of the
 * function, but I would love to avoid requiring that if at all possible as implicit weird special cases are not where
 * I want to take the language. I do realize that while `noreturn _start()` is a great signature as an entry point,
 * it really only matters to lower-level developers who may still not care that the entry point is so rigorously defined.
 * It may very well be that it's implied that the entry point function has special handling of the required `exit` system
 * call and knowledge of the `exit` library function.
 * 
 * Below is an example of how Laye may handle program entry points, where `_start` is provided by the compiler unless
 * otherwise specified by the programmer and provided in source directly.
 * 
 * The example fails semantically, since a `void` function would likely be forbidden to call a `noreturn` function.
 * This is an argument for `main` being `noreturn`, but then `_start` couldn't implicitly call `exit(0)` which means
 * that maybe any function CAN call a `noreturn` function, but if EVERY code path calls a `noreturn` then it would be
 * required to also be marked `noreturn`? I'll need to research other high level languages that use no-return markers like this.
 */
noreturn exit(i32 exitCode) { /* insert exit syscall or platform library call */ } // provided in the core or standard library for the platform
noreturn _start() { main(); exit(0); } // provided implicitly by the compiler or the executable portion of the core or standard library
void main() { } // the user-space entry point

/* ===== String Types and Unicode =====
 * 
 * Laye believes Unicode is incredibly important to modern communication and wants the programmer to be able
 * to manipulate text as easily as is possible to increase the quality of applications that deal with globalized text.
 * 
 * However, Laye doesn't want to force a program to support the entirety of Unicode operations. For example,
 * many programmers may care about getting a unicode category for a character or its printable name, but that would require
 * having access to a large lookup table to access the data. Many other programmers will not want every application
 * to have that data embedded in their application if they aren't going to use it.
 * 
 * Laye needs to have an ability to disable certain portions of its core or standard libraries and the rest of the code base
 * must be stable without them. If the programmer passes a `-nounicode` flag, for example, the unicode support should be removed
 * from the included libraries.
 * 
 * Regardless of the programmer's desire to have the additional Unicode data embedded, the string representation needs to be
 * consistent in Laye programs for semantic reasons. The default string type will likely remain UTF-8 encoded so that we can provide
 * an indexing operation that returns a u8 all the time. This may be a problem on platforms where an 8 bit number is not native, so we could
 * instead define a string as a `uleast7[*]` or similar internally instead so that at the very least ASCII is required, or `uleast8[*]` to
 * continue to require UTF-8 encoding as a default.
 * 
 * Regardless of default encoding or internal representation there should be some way to specify the encoding of a string in source either
 * via a prefix or suffix of some kind. For example, `$utf16"Hello, hunter!"` may be a valid prefix syntax to enforce a different encoding.
 * What then do we type this as? Is it still a `string` to the programmer? I don't think it should be. A string shouldn't be required to use
 * library functionality and decoding to be operated on, and providing dozens of separate `string` types like `string_utf16` would make it
 * incredibly difficult to work with strings as duplicate functions would need to be provided for each possible string type OR an interface
 * system would need to be implemented in Laye and potentially require virtual dispatch.
 * 
 * I believe an answer could be to maintain the `uleast8[*]` or similar internal buffer but add an enum to the string to specify its encoding.
 * Anything interacting with the bytes could still do so, and the functions dealing with encodings can check the enum marker. This is still very
 * complicated in comparison to just supporting UTF-8 as the only string type and requiring the users to use less convenient types like `uleast16[]`
 * to represent other encodings and forfeit string manipulation code working with them by default.
 * 
 * My current belief is that a Laye string is and always will be UTF-8 encoded with a `uleast8[*]` internal buffer.
 */
struct string { uleast8[*] data; uint length; } // an example of how strings are implemented internally. it's incredibly
