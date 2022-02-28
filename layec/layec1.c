// includes
#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// defines
#define LYSTR_STRING(lit, len) ((ly_string){ .data = (uint8_t*)(lit), .length = len })
#define LYSTR_SLICE(lit, len) ((ly_slice_1){ .data = (uint8_t*)(lit), .length = len })

// typedefs
typedef struct ly_string ly_string;
typedef struct ly_slice_1 ly_slice_1; /* laye type: u8[] */
typedef int8_t ly_bool8_t;
typedef int16_t ly_bool16_t;
typedef int32_t ly_bool32_t;
typedef int64_t ly_bool64_t;
typedef intptr_t ly_int_t;
typedef uintptr_t ly_uint_t;
typedef struct { void* data; ly_uint_t count; ly_uint_t capacity; } ly_dynamic_t;
typedef struct ly_diagnostic_bag ly_diagnostic_bag;
typedef struct ly_source_span ly_source_span;
typedef struct ly_source_location ly_source_location;
typedef struct ly_source ly_source;
typedef struct ly_diagnostic ly_diagnostic;
typedef enum ly_diagnostic_kind ly_diagnostic_kind;
typedef struct ly_slice_6 ly_slice_6; /* laye type: string[] */
typedef struct ly_slice_8 ly_slice_8; /* laye type: laye_token[] */
typedef struct ly_laye_token ly_laye_token;
typedef struct ly_slice_9 ly_slice_9; /* laye type: laye_trivia[] */
typedef struct ly_laye_trivia ly_laye_trivia;
typedef enum ly_laye_trivia_kind ly_laye_trivia_kind;
typedef struct ly_laye_token_kind ly_laye_token_kind;
typedef struct ly_string_builder ly_string_builder;
typedef struct ly_lexer_data ly_lexer_data;

// type declarations
struct ly_string {
  size_t length;
  uint8_t* data;
};

struct ly_slice_1 {
  size_t length;
  uint8_t* data;
};

struct ly_diagnostic_bag {
  ly_dynamic_t diagnostics;
};

struct ly_source {
  ly_string name;
  ly_string text;
  ly_bool8_t isValid;
};

struct ly_source_location {
  ly_source source;
  ly_uint_t characterIndex;
  ly_uint_t lineNumber;
  ly_uint_t columnNumber;
};

struct ly_source_span {
  ly_source_location startLocation;
  ly_source_location endLocation;
};

enum ly_diagnostic_kind {
  ly_diagnostic_kind__info = 0,
  ly_diagnostic_kind__warning = 1,
  ly_diagnostic_kind__error = 2,
};

struct ly_diagnostic {
  ly_string message;
  ly_source_span sourceSpan;
  ly_diagnostic_kind kind;
};

struct ly_slice_6 {
  size_t length;
  ly_string* data;
};

enum ly_laye_trivia_kind {
  ly_laye_trivia_kind__invalid = 0,
  ly_laye_trivia_kind__comment_line = 1,
  ly_laye_trivia_kind__comment_block = 2,
  ly_laye_trivia_kind__white_space = 3,
};

struct ly_laye_trivia {
  ly_source_span sourceSpan;
  ly_laye_trivia_kind kind;
};

struct ly_slice_9 {
  size_t length;
  ly_laye_trivia* data;
};

enum ly_laye_token_kind_Kinds {
  ly_laye_token_kind_Kinds__poison_token = 1,
  ly_laye_token_kind_Kinds__eof,
  ly_laye_token_kind_Kinds__ident,
  ly_laye_token_kind_Kinds__literal_integer,
  ly_laye_token_kind_Kinds__literal_float,
  ly_laye_token_kind_Kinds__literal_character,
  ly_laye_token_kind_Kinds__literal_string,
  ly_laye_token_kind_Kinds__open_paren,
  ly_laye_token_kind_Kinds__close_paren,
  ly_laye_token_kind_Kinds__open_bracket,
  ly_laye_token_kind_Kinds__close_bracket,
  ly_laye_token_kind_Kinds__open_brace,
  ly_laye_token_kind_Kinds__close_brace,
  ly_laye_token_kind_Kinds__comma,
  ly_laye_token_kind_Kinds__dot,
  ly_laye_token_kind_Kinds__question,
  ly_laye_token_kind_Kinds__colon,
  ly_laye_token_kind_Kinds__semi_colon,
  ly_laye_token_kind_Kinds__colon_colon,
  ly_laye_token_kind_Kinds__equal_greater,
  ly_laye_token_kind_Kinds__question_dot,
  ly_laye_token_kind_Kinds__equal,
  ly_laye_token_kind_Kinds__plus,
  ly_laye_token_kind_Kinds__minus,
  ly_laye_token_kind_Kinds__star,
  ly_laye_token_kind_Kinds__slash,
  ly_laye_token_kind_Kinds__percent,
  ly_laye_token_kind_Kinds__amp,
  ly_laye_token_kind_Kinds__pipe,
  ly_laye_token_kind_Kinds__tilde,
  ly_laye_token_kind_Kinds__equal_equal,
  ly_laye_token_kind_Kinds__bang_equal,
  ly_laye_token_kind_Kinds__less,
  ly_laye_token_kind_Kinds__greater,
  ly_laye_token_kind_Kinds__less_equal,
  ly_laye_token_kind_Kinds__greater_equal,
  ly_laye_token_kind_Kinds__bang_less,
  ly_laye_token_kind_Kinds__bang_greater,
  ly_laye_token_kind_Kinds__bang_less_equal,
  ly_laye_token_kind_Kinds__bang_greater_equal,
  ly_laye_token_kind_Kinds__less_less,
  ly_laye_token_kind_Kinds__greater_greater,
  ly_laye_token_kind_Kinds__kw_and,
  ly_laye_token_kind_Kinds__kw_or,
  ly_laye_token_kind_Kinds__kw_xor,
  ly_laye_token_kind_Kinds__kw_not,
  ly_laye_token_kind_Kinds__kw_cast,
  ly_laye_token_kind_Kinds__kw_is,
  ly_laye_token_kind_Kinds__kw_sizeof,
  ly_laye_token_kind_Kinds__kw_offsetof,
  ly_laye_token_kind_Kinds__kw_nameof,
  ly_laye_token_kind_Kinds__kw_nameof_variant,
  ly_laye_token_kind_Kinds__kw_dynamic_append,
  ly_laye_token_kind_Kinds__kw_void,
  ly_laye_token_kind_Kinds__kw_int,
  ly_laye_token_kind_Kinds__kw_uint,
  ly_laye_token_kind_Kinds__kw_int_sized,
  ly_laye_token_kind_Kinds__kw_uint_sized,
  ly_laye_token_kind_Kinds__kw_bool,
  ly_laye_token_kind_Kinds__kw_float,
  ly_laye_token_kind_Kinds__kw_bool_sized,
  ly_laye_token_kind_Kinds__kw_float_sized,
  ly_laye_token_kind_Kinds__kw_rune,
  ly_laye_token_kind_Kinds__kw_string,
  ly_laye_token_kind_Kinds__kw_rawptr,
  ly_laye_token_kind_Kinds__kw_dynamic,
  ly_laye_token_kind_Kinds__kw_noreturn,
  ly_laye_token_kind_Kinds__kw_varargs,
  ly_laye_token_kind_Kinds__kw_struct,
  ly_laye_token_kind_Kinds__kw_enum,
  ly_laye_token_kind_Kinds__kw_const,
  ly_laye_token_kind_Kinds__kw_readonly,
  ly_laye_token_kind_Kinds__kw_writeonly,
  ly_laye_token_kind_Kinds__kw_true,
  ly_laye_token_kind_Kinds__kw_false,
  ly_laye_token_kind_Kinds__kw_nil,
  ly_laye_token_kind_Kinds__kw_nullptr,
  ly_laye_token_kind_Kinds__kw_context,
  ly_laye_token_kind_Kinds__kw_noinit,
  ly_laye_token_kind_Kinds__kw_global,
  ly_laye_token_kind_Kinds__kw_extern,
  ly_laye_token_kind_Kinds__kw_public,
  ly_laye_token_kind_Kinds__kw_private,
  ly_laye_token_kind_Kinds__kw_callconv,
  ly_laye_token_kind_Kinds__kw_inline,
  ly_laye_token_kind_Kinds__kw_if,
  ly_laye_token_kind_Kinds__kw_else,
  ly_laye_token_kind_Kinds__kw_while,
  ly_laye_token_kind_Kinds__kw_for,
  ly_laye_token_kind_Kinds__kw_switch,
  ly_laye_token_kind_Kinds__kw_case,
  ly_laye_token_kind_Kinds__kw_return,
  ly_laye_token_kind_Kinds__kw_yield,
  ly_laye_token_kind_Kinds__kw_break,
  ly_laye_token_kind_Kinds__kw_continue,
};

struct ly_laye_token_kind {
  int kind;
  union {
    struct {
      ly_string image;
    } ident;
    struct {
      uint64_t value;
    } literal_integer;
    struct {
      double value;
    } literal_float;
    struct {
      int32_t value;
    } literal_character;
    struct {
      ly_string value;
    } literal_string;
    struct {
      uint16_t size;
    } kw_int_sized;
    struct {
      uint16_t size;
    } kw_uint_sized;
    struct {
      uint16_t size;
    } kw_bool_sized;
    struct {
      uint16_t size;
    } kw_float_sized;
  } variants;
};

struct ly_laye_token {
  ly_source_span sourceSpan;
  ly_slice_9 leadingTrivia;
  ly_slice_9 trailingTrivia;
  ly_laye_token_kind kind;
};

struct ly_slice_8 {
  size_t length;
  ly_laye_token* data;
};

struct ly_string_builder {
  ly_uint_t length;
  ly_uint_t capacity;
  uint8_t* data;
};

struct ly_lexer_data {
  ly_source currentSource;
  ly_uint_t currentIndex;
  ly_uint_t currentColumn;
  ly_uint_t currentLine;
  ly_diagnostic_bag* diagnostics;
};


// function prototypes
ly_string ly_internal_substring(ly_string s_, ly_uint_t o_, ly_uint_t l_);
ly_string ly_internal_slicetostring(ly_slice_1 s_);
void diagnostics_add_info(ly_diagnostic_bag* b, ly_source_span sourceSpan, ly_string message);
void diagnostics_add_warning(ly_diagnostic_bag* b, ly_source_span sourceSpan, ly_string message);
void diagnostics_add_error(ly_diagnostic_bag* b, ly_source_span sourceSpan, ly_string message);
void ly_creservedfn_main(int32_t argc, uint8_t** argv);
ly_slice_1 ly_internal_bufferslice_5(uint8_t* b_, ly_uint_t o_, ly_uint_t l_);
ly_slice_6 ly_internal_bufferslice_7(ly_string* b_, ly_uint_t o_, ly_uint_t l_);
void laye_main(ly_slice_6 args);
void diagnostics_print(ly_diagnostic_bag b);
void panic(ly_string message);
void assert(ly_bool8_t test, ly_string message);
ly_uint_t uint_num_digits(ly_uint_t n);
ly_uint_t int_num_digits(ly_int_t n);
void* memory_allocate(ly_uint_t byteCount);
uint8_t* string_to_cstring(ly_string s);
ly_string string_concat(ly_string a, ly_string b);
ly_string uint_to_string(ly_uint_t v);
ly_bool8_t char_is_digit(uint8_t c);
ly_bool8_t char_is_alpha(uint8_t c);
ly_bool8_t char_is_white_space(uint8_t c);
ly_uint_t string_builder_length_get(ly_string_builder sb);
ly_uint_t string_builder_capacity_get(ly_string_builder sb);
uint8_t* string_builder_data_get(ly_string_builder sb);
void string_builder_free(ly_string_builder* sb);
void string_builder_ensure_capacity(ly_string_builder* sb, ly_uint_t desired_capacity);
ly_string string_builder_to_string(ly_string_builder sb);
ly_slice_1 ly_internal_bufferslice_10(uint8_t* b_, ly_uint_t o_, ly_uint_t l_);
void string_builder_append_string(ly_string_builder* sb, ly_string v);
void string_builder_append_character(ly_string_builder* sb, uint8_t c);
void string_builder_append_rune(ly_string_builder* sb, int32_t c);
void string_builder_append_uint(ly_string_builder* sb, ly_uint_t v);
void string_builder_append_uint_hexn(ly_string_builder* sb, ly_uint_t v, ly_uint_t n);
ly_uint_t unicode_utf8_calc_encoded_byte_count(uint8_t byte1);
ly_uint_t unicode_utf8_calc_rune_byte_count(int32_t r);
int32_t unicode_utf8_string_rune_at_index(ly_string s, ly_uint_t index);
ly_bool8_t unicode_is_digit(int32_t r);
ly_bool8_t unicode_is_letter(int32_t r);
ly_bool8_t unicode_is_white_space(int32_t r);
ly_bool8_t rune_is_laye_digit(int32_t r);
ly_uint_t rune_laye_digit_value(int32_t r);
ly_bool8_t rune_is_laye_identifier_part(int32_t r);
ly_bool8_t lexer_is_eof(ly_lexer_data l);
int32_t lexer_current_rune(ly_lexer_data l);
int32_t lexer_peek_char(ly_lexer_data l);
ly_source_location lexer_current_location(ly_lexer_data l);
void lexer_advance(ly_lexer_data* l);
ly_slice_9 lexer_get_laye_trivia(ly_lexer_data* l, ly_bool8_t untilEndOfLine);
ly_slice_9 ly_internal_dynamicslice_12(ly_dynamic_t d_, ly_uint_t o_, ly_uint_t l_);
ly_slice_8 lexer_read_laye_tokens(ly_source source, ly_diagnostic_bag* diagnostics);
ly_slice_8 ly_internal_dynamicslice_15(ly_dynamic_t d_, ly_uint_t o_, ly_uint_t l_);
ly_laye_token lexer_read_laye_token(ly_lexer_data* l);
void lexer_read_laye_identifier_or_number(ly_lexer_data* l, ly_laye_token* token);
void lexer_read_laye_radix_integer_from_delimiter(ly_lexer_data* l, ly_laye_token* token, ly_source_location startLocation, uint64_t radix);
void lexer_read_laye_string(ly_lexer_data* l, ly_laye_token* token);
ly_source source_create_from_file(ly_string fileName);
ly_string source_location_name_get(ly_source_location sl);
ly_uint_t source_location_line_get(ly_source_location sl);
ly_uint_t source_location_column_get(ly_source_location sl);
ly_string source_location_to_string(ly_source_location sl);
ly_source_span source_span_create(ly_source_location start, ly_source_location end);
ly_string source_span_name_get(ly_source_span ss);
ly_string source_span_to_string(ly_source_span ss);

// functions
ly_string ly_internal_substring(ly_string s_, ly_uint_t o_, ly_uint_t l_)
{
    ly_string result;
    result.data = s_.data + o_;
    result.length = l_;
    return result;
}
ly_string ly_internal_slicetostring(ly_slice_1 s_)
{
    ly_string result;
    result.data = malloc(s_.length);
    memcpy(result.data, s_.data, s_.length);
    result.length = s_.length;
    return result;
}
void ly_internal_dynamic_ensure_capacity(ly_dynamic_t* d_, ly_uint_t reqiredCapacity)
{
	ly_uint_t capacity = d_->capacity;
	if (capacity < reqiredCapacity)
	{
        ly_uint_t desiredCapacity = capacity == 0 ? reqiredCapacity * 2 : capacity * 2;
        d_->capacity = desiredCapacity;
		d_->data = realloc(d_->data, desiredCapacity);
	}
}
static ly_string ly_enum_to_string__ly_diagnostic_kind(int enumValue) {
    switch (enumValue) {
    case ly_diagnostic_kind__info: return LYSTR_STRING("info", 4);
    case ly_diagnostic_kind__warning: return LYSTR_STRING("warning", 7);
    case ly_diagnostic_kind__error: return LYSTR_STRING("error", 5);
    default: return LYSTR_STRING("<invalid>", 9); 
    }
}

ly_slice_1 ly_internal_bufferslice_5(uint8_t* b_, ly_uint_t o_, ly_uint_t l_)
{
  ly_slice_1 result;
  result.data = b_ + o_;
  result.length = l_;
  return result;
}
ly_slice_6 ly_internal_bufferslice_7(ly_string* b_, ly_uint_t o_, ly_uint_t l_)
{
  ly_slice_6 result;
  result.data = b_ + o_;
  result.length = l_;
  return result;
}
static ly_string ly_enum_to_string__ly_laye_trivia_kind(int enumValue) {
    switch (enumValue) {
    case ly_laye_trivia_kind__invalid: return LYSTR_STRING("invalid", 7);
    case ly_laye_trivia_kind__comment_line: return LYSTR_STRING("comment_line", 12);
    case ly_laye_trivia_kind__comment_block: return LYSTR_STRING("comment_block", 13);
    case ly_laye_trivia_kind__white_space: return LYSTR_STRING("white_space", 11);
    default: return LYSTR_STRING("<invalid>", 9); 
    }
}

static ly_string ly_union_tag_to_string__ly_laye_token_kind(int enumValue) {
    switch (enumValue) {
    case 0: return LYSTR_STRING("nil", 3);
    case ly_laye_token_kind_Kinds__poison_token: return LYSTR_STRING("poison_token", 12);
    case ly_laye_token_kind_Kinds__eof: return LYSTR_STRING("eof", 3);
    case ly_laye_token_kind_Kinds__ident: return LYSTR_STRING("ident", 5);
    case ly_laye_token_kind_Kinds__literal_integer: return LYSTR_STRING("literal_integer", 15);
    case ly_laye_token_kind_Kinds__literal_float: return LYSTR_STRING("literal_float", 13);
    case ly_laye_token_kind_Kinds__literal_character: return LYSTR_STRING("literal_character", 17);
    case ly_laye_token_kind_Kinds__literal_string: return LYSTR_STRING("literal_string", 14);
    case ly_laye_token_kind_Kinds__open_paren: return LYSTR_STRING("open_paren", 10);
    case ly_laye_token_kind_Kinds__close_paren: return LYSTR_STRING("close_paren", 11);
    case ly_laye_token_kind_Kinds__open_bracket: return LYSTR_STRING("open_bracket", 12);
    case ly_laye_token_kind_Kinds__close_bracket: return LYSTR_STRING("close_bracket", 13);
    case ly_laye_token_kind_Kinds__open_brace: return LYSTR_STRING("open_brace", 10);
    case ly_laye_token_kind_Kinds__close_brace: return LYSTR_STRING("close_brace", 11);
    case ly_laye_token_kind_Kinds__comma: return LYSTR_STRING("comma", 5);
    case ly_laye_token_kind_Kinds__dot: return LYSTR_STRING("dot", 3);
    case ly_laye_token_kind_Kinds__question: return LYSTR_STRING("question", 8);
    case ly_laye_token_kind_Kinds__colon: return LYSTR_STRING("colon", 5);
    case ly_laye_token_kind_Kinds__semi_colon: return LYSTR_STRING("semi_colon", 10);
    case ly_laye_token_kind_Kinds__colon_colon: return LYSTR_STRING("colon_colon", 11);
    case ly_laye_token_kind_Kinds__equal_greater: return LYSTR_STRING("equal_greater", 13);
    case ly_laye_token_kind_Kinds__question_dot: return LYSTR_STRING("question_dot", 12);
    case ly_laye_token_kind_Kinds__equal: return LYSTR_STRING("equal", 5);
    case ly_laye_token_kind_Kinds__plus: return LYSTR_STRING("plus", 4);
    case ly_laye_token_kind_Kinds__minus: return LYSTR_STRING("minus", 5);
    case ly_laye_token_kind_Kinds__star: return LYSTR_STRING("star", 4);
    case ly_laye_token_kind_Kinds__slash: return LYSTR_STRING("slash", 5);
    case ly_laye_token_kind_Kinds__percent: return LYSTR_STRING("percent", 7);
    case ly_laye_token_kind_Kinds__amp: return LYSTR_STRING("amp", 3);
    case ly_laye_token_kind_Kinds__pipe: return LYSTR_STRING("pipe", 4);
    case ly_laye_token_kind_Kinds__tilde: return LYSTR_STRING("tilde", 5);
    case ly_laye_token_kind_Kinds__equal_equal: return LYSTR_STRING("equal_equal", 11);
    case ly_laye_token_kind_Kinds__bang_equal: return LYSTR_STRING("bang_equal", 10);
    case ly_laye_token_kind_Kinds__less: return LYSTR_STRING("less", 4);
    case ly_laye_token_kind_Kinds__greater: return LYSTR_STRING("greater", 7);
    case ly_laye_token_kind_Kinds__less_equal: return LYSTR_STRING("less_equal", 10);
    case ly_laye_token_kind_Kinds__greater_equal: return LYSTR_STRING("greater_equal", 13);
    case ly_laye_token_kind_Kinds__bang_less: return LYSTR_STRING("bang_less", 9);
    case ly_laye_token_kind_Kinds__bang_greater: return LYSTR_STRING("bang_greater", 12);
    case ly_laye_token_kind_Kinds__bang_less_equal: return LYSTR_STRING("bang_less_equal", 15);
    case ly_laye_token_kind_Kinds__bang_greater_equal: return LYSTR_STRING("bang_greater_equal", 18);
    case ly_laye_token_kind_Kinds__less_less: return LYSTR_STRING("less_less", 9);
    case ly_laye_token_kind_Kinds__greater_greater: return LYSTR_STRING("greater_greater", 15);
    case ly_laye_token_kind_Kinds__kw_and: return LYSTR_STRING("kw_and", 6);
    case ly_laye_token_kind_Kinds__kw_or: return LYSTR_STRING("kw_or", 5);
    case ly_laye_token_kind_Kinds__kw_xor: return LYSTR_STRING("kw_xor", 6);
    case ly_laye_token_kind_Kinds__kw_not: return LYSTR_STRING("kw_not", 6);
    case ly_laye_token_kind_Kinds__kw_cast: return LYSTR_STRING("kw_cast", 7);
    case ly_laye_token_kind_Kinds__kw_is: return LYSTR_STRING("kw_is", 5);
    case ly_laye_token_kind_Kinds__kw_sizeof: return LYSTR_STRING("kw_sizeof", 9);
    case ly_laye_token_kind_Kinds__kw_offsetof: return LYSTR_STRING("kw_offsetof", 11);
    case ly_laye_token_kind_Kinds__kw_nameof: return LYSTR_STRING("kw_nameof", 9);
    case ly_laye_token_kind_Kinds__kw_nameof_variant: return LYSTR_STRING("kw_nameof_variant", 17);
    case ly_laye_token_kind_Kinds__kw_dynamic_append: return LYSTR_STRING("kw_dynamic_append", 17);
    case ly_laye_token_kind_Kinds__kw_void: return LYSTR_STRING("kw_void", 7);
    case ly_laye_token_kind_Kinds__kw_int: return LYSTR_STRING("kw_int", 6);
    case ly_laye_token_kind_Kinds__kw_uint: return LYSTR_STRING("kw_uint", 7);
    case ly_laye_token_kind_Kinds__kw_int_sized: return LYSTR_STRING("kw_int_sized", 12);
    case ly_laye_token_kind_Kinds__kw_uint_sized: return LYSTR_STRING("kw_uint_sized", 13);
    case ly_laye_token_kind_Kinds__kw_bool: return LYSTR_STRING("kw_bool", 7);
    case ly_laye_token_kind_Kinds__kw_float: return LYSTR_STRING("kw_float", 8);
    case ly_laye_token_kind_Kinds__kw_bool_sized: return LYSTR_STRING("kw_bool_sized", 13);
    case ly_laye_token_kind_Kinds__kw_float_sized: return LYSTR_STRING("kw_float_sized", 14);
    case ly_laye_token_kind_Kinds__kw_rune: return LYSTR_STRING("kw_rune", 7);
    case ly_laye_token_kind_Kinds__kw_string: return LYSTR_STRING("kw_string", 9);
    case ly_laye_token_kind_Kinds__kw_rawptr: return LYSTR_STRING("kw_rawptr", 9);
    case ly_laye_token_kind_Kinds__kw_dynamic: return LYSTR_STRING("kw_dynamic", 10);
    case ly_laye_token_kind_Kinds__kw_noreturn: return LYSTR_STRING("kw_noreturn", 11);
    case ly_laye_token_kind_Kinds__kw_varargs: return LYSTR_STRING("kw_varargs", 10);
    case ly_laye_token_kind_Kinds__kw_struct: return LYSTR_STRING("kw_struct", 9);
    case ly_laye_token_kind_Kinds__kw_enum: return LYSTR_STRING("kw_enum", 7);
    case ly_laye_token_kind_Kinds__kw_const: return LYSTR_STRING("kw_const", 8);
    case ly_laye_token_kind_Kinds__kw_readonly: return LYSTR_STRING("kw_readonly", 11);
    case ly_laye_token_kind_Kinds__kw_writeonly: return LYSTR_STRING("kw_writeonly", 12);
    case ly_laye_token_kind_Kinds__kw_true: return LYSTR_STRING("kw_true", 7);
    case ly_laye_token_kind_Kinds__kw_false: return LYSTR_STRING("kw_false", 8);
    case ly_laye_token_kind_Kinds__kw_nil: return LYSTR_STRING("kw_nil", 6);
    case ly_laye_token_kind_Kinds__kw_nullptr: return LYSTR_STRING("kw_nullptr", 10);
    case ly_laye_token_kind_Kinds__kw_context: return LYSTR_STRING("kw_context", 10);
    case ly_laye_token_kind_Kinds__kw_noinit: return LYSTR_STRING("kw_noinit", 9);
    case ly_laye_token_kind_Kinds__kw_global: return LYSTR_STRING("kw_global", 9);
    case ly_laye_token_kind_Kinds__kw_extern: return LYSTR_STRING("kw_extern", 9);
    case ly_laye_token_kind_Kinds__kw_public: return LYSTR_STRING("kw_public", 9);
    case ly_laye_token_kind_Kinds__kw_private: return LYSTR_STRING("kw_private", 10);
    case ly_laye_token_kind_Kinds__kw_callconv: return LYSTR_STRING("kw_callconv", 11);
    case ly_laye_token_kind_Kinds__kw_inline: return LYSTR_STRING("kw_inline", 9);
    case ly_laye_token_kind_Kinds__kw_if: return LYSTR_STRING("kw_if", 5);
    case ly_laye_token_kind_Kinds__kw_else: return LYSTR_STRING("kw_else", 7);
    case ly_laye_token_kind_Kinds__kw_while: return LYSTR_STRING("kw_while", 8);
    case ly_laye_token_kind_Kinds__kw_for: return LYSTR_STRING("kw_for", 6);
    case ly_laye_token_kind_Kinds__kw_switch: return LYSTR_STRING("kw_switch", 9);
    case ly_laye_token_kind_Kinds__kw_case: return LYSTR_STRING("kw_case", 7);
    case ly_laye_token_kind_Kinds__kw_return: return LYSTR_STRING("kw_return", 9);
    case ly_laye_token_kind_Kinds__kw_yield: return LYSTR_STRING("kw_yield", 8);
    case ly_laye_token_kind_Kinds__kw_break: return LYSTR_STRING("kw_break", 8);
    case ly_laye_token_kind_Kinds__kw_continue: return LYSTR_STRING("kw_continue", 11);
    default: return LYSTR_STRING("<invalid>", 9); 
    }
}

ly_slice_1 ly_internal_bufferslice_10(uint8_t* b_, ly_uint_t o_, ly_uint_t l_)
{
  ly_slice_1 result;
  result.data = b_ + o_;
  result.length = l_;
  return result;
}
ly_slice_9 ly_internal_dynamicslice_12(ly_dynamic_t d_, ly_uint_t o_, ly_uint_t l_)
{
  ly_slice_9 result;
  ly_laye_trivia* data = (ly_laye_trivia*)d_.data;
  result.data = malloc(l_ * sizeof(ly_laye_trivia));
  memcpy(result.data, data + o_, l_);
  result.length = l_;
  return result;
}
ly_slice_8 ly_internal_dynamicslice_15(ly_dynamic_t d_, ly_uint_t o_, ly_uint_t l_)
{
  ly_slice_8 result;
  ly_laye_token* data = (ly_laye_token*)d_.data;
  result.data = malloc(l_ * sizeof(ly_laye_token));
  memcpy(result.data, data + o_, l_);
  result.length = l_;
  return result;
}

void diagnostics_add_info(ly_diagnostic_bag* b, ly_source_span sourceSpan, ly_string message) {
  ly_diagnostic d = {0};
  (d).message = message;
  (d).sourceSpan = sourceSpan;
  (d).kind = ly_diagnostic_kind__info;
  ly_dynamic_t* ly_dyn_2 = &((*(b)).diagnostics);
  ly_internal_dynamic_ensure_capacity(ly_dyn_2, (ly_dyn_2->count + 1) * sizeof(ly_diagnostic));
  (/*cast*/(ly_diagnostic*) (ly_dyn_2->data))[ly_dyn_2->count] = (d);
  ly_dyn_2->count++;
}

void diagnostics_add_warning(ly_diagnostic_bag* b, ly_source_span sourceSpan, ly_string message) {
  ly_diagnostic d = {0};
  (d).message = message;
  (d).sourceSpan = sourceSpan;
  (d).kind = ly_diagnostic_kind__warning;
  ly_dynamic_t* ly_dyn_3 = &((*(b)).diagnostics);
  ly_internal_dynamic_ensure_capacity(ly_dyn_3, (ly_dyn_3->count + 1) * sizeof(ly_diagnostic));
  (/*cast*/(ly_diagnostic*) (ly_dyn_3->data))[ly_dyn_3->count] = (d);
  ly_dyn_3->count++;
}

void diagnostics_add_error(ly_diagnostic_bag* b, ly_source_span sourceSpan, ly_string message) {
  ly_diagnostic d = {0};
  (d).message = message;
  (d).sourceSpan = sourceSpan;
  (d).kind = ly_diagnostic_kind__error;
  ly_dynamic_t* ly_dyn_4 = &((*(b)).diagnostics);
  ly_internal_dynamic_ensure_capacity(ly_dyn_4, (ly_dyn_4->count + 1) * sizeof(ly_diagnostic));
  (/*cast*/(ly_diagnostic*) (ly_dyn_4->data))[ly_dyn_4->count] = (d);
  ly_dyn_4->count++;
}

void ly_creservedfn_main(int32_t argc, uint8_t** argv) {
  ly_string* argvStorage = (ly_string*)memory_allocate((sizeof(ly_string)) * ((ly_uint_t)argc));
  ly_uint_t argvCounter = 0;
  while ((argvCounter) < ((ly_uint_t)argc))
  {
    (argvStorage)[argvCounter] = ly_internal_slicetostring(ly_internal_bufferslice_5((argv)[argvCounter], 0, strlen((argv)[argvCounter])));
    argvCounter = (argvCounter) + (1);
  }
  ly_slice_6 args = ly_internal_bufferslice_7(argvStorage, 0, argc);
  laye_main(args);
}

void laye_main(ly_slice_6 args) {
  printf("Laye stand-alone compiler%cVersion 0.1.0%c%c", 10, 10, 10);
  if (((args).length) < (2))
  {
    printf("error: no file given%c", 10);
    return;
  }
  ly_diagnostic_bag diagnostics = {0};
  ly_string sourceFilePath = (args).data[1];
  ly_source sourceFile = source_create_from_file(sourceFilePath);
  if (!((sourceFile).isValid))
  {
    printf("failed to open file%c", 10);
    return;
  }
  ly_slice_8 sourceTokens = lexer_read_laye_tokens(sourceFile, &(diagnostics));
  printf("read %llu tokens from file successfully%c", (sourceTokens).length, 10);
  diagnostics_print(diagnostics);
  if (false)
  {
    ly_uint_t i = 0;
    while ((i) < ((sourceTokens).length))
    {
      ly_laye_token token = (sourceTokens).data[i];
      ly_string tokenLocationString = source_location_to_string(((token).sourceSpan).startLocation);
      ly_string tokenString = source_span_to_string((token).sourceSpan);
      ly_string tokenKindString = ly_union_tag_to_string__ly_laye_token_kind(((token).kind).kind);
      printf("> token %.*s (%.*s) %.*s%c", (tokenKindString).length, (tokenKindString).data, (tokenLocationString).length, (tokenLocationString).data, (tokenString).length, (tokenString).data, 10);
      i = (i) + (1);
    }
  }
}

void diagnostics_print(ly_diagnostic_bag b) {
  ly_uint_t count = ((b).diagnostics).count;
  {
    ly_uint_t i = 0;
    while ((i) < (count))
    {
      ly_diagnostic d = (/*cast*/(ly_diagnostic*) ((b).diagnostics).data)[i];
      ly_string locationString = source_location_to_string(((d).sourceSpan).startLocation);
      ly_string kindString = ly_enum_to_string__ly_diagnostic_kind((d).kind);
      printf("%.*s: %.*s: %.*s%c", (locationString).length, (locationString).data, (kindString).length, (kindString).data, ((d).message).length, ((d).message).data, 10);
      i = (i) + (1);
    }
  }
}

void panic(ly_string message) {
  printf("%.*s%c", (message).length, (message).data, 10);
  abort();
}

void assert(ly_bool8_t test, ly_string message) {
  if (!(test))
    panic(string_concat(LYSTR_STRING("assertion fail: ", 16), message));
}

ly_uint_t uint_num_digits(ly_uint_t n) {
  if ((n) < (10))
    return(1);
  else   if ((n) < (100))
    return(2);
  else   if ((n) < (1000))
    return(3);
  else   if ((n) < (10000))
    return(4);
  else   if ((n) < (100000))
    return(5);
  else   if ((n) < (1000000))
    return(6);
  else   if ((n) < (10000000))
    return(7);
  else   if ((n) < (100000000))
    return(8);
  else   if ((n) < (1000000000))
    return(9);
  else   if ((n) < (10000000000))
    return(10);
  else   if ((n) < (100000000000))
    return(11);
  else   if ((n) < (1000000000000))
    return(12);
  else   if ((n) < (10000000000000))
    return(13);
  else   if ((n) < (100000000000000))
    return(14);
  else   if ((n) < (1000000000000000))
    return(15);
  else   if ((n) < (10000000000000000))
    return(16);
  else   if ((n) < (100000000000000000))
    return(17);
  else   if ((n) < (1000000000000000000))
    return(18);
  else
    return(19);
}

ly_uint_t int_num_digits(ly_int_t n) {
  if ((n) < (0))
  {
    if ((n) == (9223372036854775808))
      n = 9223372036854775807;
    else
      n = -(n);
  }
  if ((n) < (10))
    return(1);
  else   if ((n) < (100))
    return(2);
  else   if ((n) < (1000))
    return(3);
  else   if ((n) < (10000))
    return(4);
  else   if ((n) < (100000))
    return(5);
  else   if ((n) < (1000000))
    return(6);
  else   if ((n) < (10000000))
    return(7);
  else   if ((n) < (100000000))
    return(8);
  else   if ((n) < (1000000000))
    return(9);
  else   if ((n) < (10000000000))
    return(10);
  else   if ((n) < (100000000000))
    return(11);
  else   if ((n) < (1000000000000))
    return(12);
  else   if ((n) < (10000000000000))
    return(13);
  else   if ((n) < (100000000000000))
    return(14);
  else   if ((n) < (1000000000000000))
    return(15);
  else   if ((n) < (10000000000000000))
    return(16);
  else   if ((n) < (100000000000000000))
    return(17);
  else   if ((n) < (1000000000000000000))
    return(18);
  else
    return(19);
}

void* memory_allocate(ly_uint_t byteCount) {
  return(malloc(byteCount));
}

uint8_t* string_to_cstring(ly_string s) {
  uint8_t* data = (s).data;
  uint8_t* cstring = (uint8_t*)malloc(((s).length) + (1));
  memcpy((void*)cstring, (void*)data, (s).length);
  (cstring)[(s).length] = 0;
  return(cstring);
}

ly_string string_concat(ly_string a, ly_string b) {
  ly_string_builder builder = {0};
  string_builder_ensure_capacity(&(builder), ((a).length) + ((b).length));
  string_builder_append_string(&(builder), a);
  string_builder_append_string(&(builder), b);
  ly_string result = string_builder_to_string(builder);
  string_builder_free(&(builder));
  return(result);
}

ly_string uint_to_string(ly_uint_t v) {
  ly_string_builder builder = {0};
  string_builder_append_uint(&(builder), v);
  ly_string result = string_builder_to_string(builder);
  string_builder_free(&(builder));
  return(result);
}

ly_bool8_t char_is_digit(uint8_t c) {
  return(((c) >= (48)) && ((c) < (58)));
}

ly_bool8_t char_is_alpha(uint8_t c) {
  return((((c) >= (65)) && ((c) <= (90))) || (((c) >= (97)) && ((c) <= (122))));
}

ly_bool8_t char_is_white_space(uint8_t c) {
  return((((((c) == (9)) || ((c) == (10))) || ((c) == (11))) || ((c) == (13))) || ((c) == (32)));
}

ly_uint_t string_builder_length_get(ly_string_builder sb) {
  return((sb).length);
}

ly_uint_t string_builder_capacity_get(ly_string_builder sb) {
  return((sb).capacity);
}

uint8_t* string_builder_data_get(ly_string_builder sb) {
  return((uint8_t*)(sb).data);
}

void string_builder_free(ly_string_builder* sb) {
  free((void*)(*(sb)).data);
  (*(sb)).length = 0;
  (*(sb)).capacity = 0;
  (*(sb)).data = NULL;
}

void string_builder_ensure_capacity(ly_string_builder* sb, ly_uint_t desired_capacity) {
  ly_uint_t capacity = (*(sb)).capacity;
  if ((capacity) < (desired_capacity))
  {
    (*(sb)).capacity = desired_capacity;
    (*(sb)).data = (uint8_t*)realloc((void*)(*(sb)).data, desired_capacity);
  }
}

ly_string string_builder_to_string(ly_string_builder sb) {
  uint8_t* data = string_builder_data_get(sb);
  ly_uint_t length = string_builder_length_get(sb);
  uint8_t* result_data = (uint8_t*)malloc((sizeof(uint8_t)) * (length));
  memcpy((void*)result_data, (void*)data, length);
  return(ly_internal_slicetostring(ly_internal_bufferslice_10(result_data, 0, length)));
}

void string_builder_append_string(ly_string_builder* sb, ly_string v) {
  ly_uint_t sbLength = string_builder_length_get(*(sb));
  ly_uint_t requiredCapacity = (sbLength) + ((v).length);
  string_builder_ensure_capacity(sb, requiredCapacity);
  ly_uint_t i = 0;
  while ((i) < ((v).length))
  {
    ((*(sb)).data)[(sbLength) + (i)] = ((v).data)[i];
    i = (i) + (1);
  }
  (*(sb)).length = requiredCapacity;
}

void string_builder_append_character(ly_string_builder* sb, uint8_t c) {
  ly_uint_t sbLength = string_builder_length_get(*(sb));
  ly_uint_t requiredCapacity = (sbLength) + (1);
  string_builder_ensure_capacity(sb, requiredCapacity);
  ((*(sb)).data)[sbLength] = c;
  (*(sb)).length = requiredCapacity;
}

void string_builder_append_rune(ly_string_builder* sb, int32_t c) {
  ly_uint_t runeByteCount = unicode_utf8_calc_rune_byte_count(c);
  ly_uint_t sbLength = string_builder_length_get(*(sb));
  ly_uint_t requiredCapacity = (sbLength) + (runeByteCount);
  string_builder_ensure_capacity(sb, requiredCapacity);
  if ((runeByteCount) == (1))
  {
    ((*(sb)).data)[(sbLength) + (0)] = (uint8_t)(c) & (127);
  }
  else   if ((runeByteCount) == (2))
  {
    ((*(sb)).data)[(sbLength) + (0)] = (uint8_t)(192) | (((c) >> (6)) & (31));
    ((*(sb)).data)[(sbLength) + (1)] = (uint8_t)(128) | (((c) >> (0)) & (63));
  }
  else   if ((runeByteCount) == (3))
  {
    ((*(sb)).data)[(sbLength) + (0)] = (uint8_t)(224) | (((c) >> (12)) & (15));
    ((*(sb)).data)[(sbLength) + (1)] = (uint8_t)(128) | (((c) >> (6)) & (63));
    ((*(sb)).data)[(sbLength) + (2)] = (uint8_t)(128) | (((c) >> (0)) & (63));
  }
  else   if ((runeByteCount) == (4))
  {
    ((*(sb)).data)[(sbLength) + (0)] = (uint8_t)(240) | (((c) >> (18)) & (7));
    ((*(sb)).data)[(sbLength) + (1)] = (uint8_t)(128) | (((c) >> (12)) & (63));
    ((*(sb)).data)[(sbLength) + (2)] = (uint8_t)(128) | (((c) >> (6)) & (63));
    ((*(sb)).data)[(sbLength) + (3)] = (uint8_t)(128) | (((c) >> (0)) & (63));
  }
  (*(sb)).length = requiredCapacity;
}

void string_builder_append_uint(ly_string_builder* sb, ly_uint_t v) {
  ly_uint_t sbLength = string_builder_length_get(*(sb));
  ly_uint_t digitCount = uint_num_digits(v);
  ly_uint_t requiredCapacity = (sbLength) + (digitCount);
  string_builder_ensure_capacity(sb, requiredCapacity);
  ly_uint_t i = 0;
  while ((v) > (0))
  {
    ly_uint_t d = (v) % (10);
    ly_uint_t index = (((sbLength) + (digitCount)) - (i)) - (1);
    uint8_t c = (uint8_t)(48) + (d);
    ((*(sb)).data)[index] = c;
    v = (v) / (10);
    i = (i) + (1);
  }
  (*(sb)).length = requiredCapacity;
}

void string_builder_append_uint_hexn(ly_string_builder* sb, ly_uint_t v, ly_uint_t n) {
  ly_uint_t sbLength = string_builder_length_get(*(sb));
  ly_uint_t requiredCapacity = (sbLength) + (n);
  string_builder_ensure_capacity(sb, requiredCapacity);
  ly_uint_t i = 0;
  while ((i) < (n))
  {
    ly_uint_t d = (v) % (16);
    ly_uint_t index = (((sbLength) + (n)) - (i)) - (1);
    if ((d) < (10))
    {
      uint8_t c = (uint8_t)(48) + (d);
      ((*(sb)).data)[index] = c;
    }
    else {
  uint8_t c = (uint8_t)(65) + (d);
  ((*(sb)).data)[index] = c;
}
    v = (v) / (16);
    i = (i) + (1);
  }
  (*(sb)).length = requiredCapacity;
}

ly_uint_t unicode_utf8_calc_encoded_byte_count(uint8_t byte1) {
  if (((byte1) & (128)) == (0))
    return(1);
  else   if (((byte1) & (224)) == (192))
    return(2);
  else   if (((byte1) & (240)) == (224))
    return(3);
  else   if (((byte1) & (248)) == (240))
    return(4);
  else
    return(0);
}

ly_uint_t unicode_utf8_calc_rune_byte_count(int32_t r) {
  if ((r) <= (127))
    return(1);
  else   if ((r) <= (2047))
    return(2);
  else   if ((r) <= (65535))
    return(3);
  else
    return(4);
}

int32_t unicode_utf8_string_rune_at_index(ly_string s, ly_uint_t index) {
  if (((s).length) <= (index))
    return(0);
  ly_uint_t expectedByteCount = unicode_utf8_calc_encoded_byte_count((s).data[index]);
  if ((expectedByteCount) == (0))
    return(0);
  if (((index) + (expectedByteCount)) >= ((s).length))
    return(0);
  if ((expectedByteCount) == (1))
    return((int32_t)(s).data[index]);
  else   if ((expectedByteCount) == (2))
  {
    int32_t byte1 = (int32_t)(s).data[index];
    int32_t byte2 = (int32_t)(s).data[(index) + (1)];
    return((((byte1) & (31)) << (6)) | ((byte2) & (63)));
  }
  else   if ((expectedByteCount) == (3))
  {
    int32_t byte1 = (int32_t)(s).data[index];
    int32_t byte2 = (int32_t)(s).data[(index) + (1)];
    int32_t byte3 = (int32_t)(s).data[(index) + (2)];
    return(((((byte1) & (15)) << (12)) | (((byte2) & (63)) << (6))) | ((byte3) & (63)));
  }
  else {
  int32_t byte1 = (int32_t)(s).data[index];
  int32_t byte2 = (int32_t)(s).data[(index) + (1)];
  int32_t byte3 = (int32_t)(s).data[(index) + (2)];
  int32_t byte4 = (int32_t)(s).data[(index) + (3)];
  return((((((byte1) & (7)) << (18)) | (((byte2) & (63)) << (12))) | (((byte3) & (63)) << (6))) | ((byte4) & (63)));
}
}

ly_bool8_t unicode_is_digit(int32_t r) {
  return(((r) >= (48)) && ((r) < (58)));
}

ly_bool8_t unicode_is_letter(int32_t r) {
  return((((r) >= (65)) && ((r) <= (90))) || (((r) >= (97)) && ((r) <= (122))));
}

ly_bool8_t unicode_is_white_space(int32_t r) {
  return((((((r) == (9)) || ((r) == (10))) || ((r) == (11))) || ((r) == (13))) || ((r) == (32)));
}

ly_bool8_t rune_is_laye_digit(int32_t r) {
  return(((r) < (127)) && (char_is_digit((uint8_t)r)));
}

ly_uint_t rune_laye_digit_value(int32_t r) {
  return((ly_uint_t)(r) - (48));
}

ly_bool8_t rune_is_laye_identifier_part(int32_t r) {
  return(((unicode_is_digit(r)) || (unicode_is_letter(r))) || ((r) == (95)));
}

ly_bool8_t lexer_is_eof(ly_lexer_data l) {
  return((((l).currentIndex) >= ((((l).currentSource).text).length)) || (((((l).currentSource).text).data[(l).currentIndex]) == (0)));
}

int32_t lexer_current_rune(ly_lexer_data l) {
  if (lexer_is_eof(l))
    return(0);
  return(unicode_utf8_string_rune_at_index(((l).currentSource).text, (l).currentIndex));
}

int32_t lexer_peek_char(ly_lexer_data l) {
  if (lexer_is_eof(l))
    return(0);
  ly_uint_t currentByteCount = unicode_utf8_calc_encoded_byte_count((((l).currentSource).text).data[(l).currentIndex]);
  return(unicode_utf8_string_rune_at_index(((l).currentSource).text, ((l).currentIndex) + (currentByteCount)));
}

ly_source_location lexer_current_location(ly_lexer_data l) {
  ly_source_location location = {0};
  (location).source = (l).currentSource;
  (location).characterIndex = (l).currentIndex;
  (location).lineNumber = (l).currentLine;
  (location).columnNumber = (l).currentColumn;
  return(location);
}

void lexer_advance(ly_lexer_data* l) {
  if (lexer_is_eof(*(l)))
    return;
  int32_t c = lexer_current_rune(*(l));
  (*(l)).currentIndex = ((*(l)).currentIndex) + (1);
  if ((c) == (10))
  {
    (*(l)).currentLine = ((*(l)).currentLine) + (1);
    (*(l)).currentColumn = 1;
  }
  else
    (*(l)).currentColumn = ((*(l)).currentColumn) + (1);
}

ly_slice_9 lexer_get_laye_trivia(ly_lexer_data* l, ly_bool8_t untilEndOfLine) {
  ly_dynamic_t triviaList = {0};
  while (!(lexer_is_eof(*(l))))
  {
    ly_laye_trivia trivia = {0};
    ly_source_location startLocation = lexer_current_location(*(l));
    int32_t c = lexer_current_rune(*(l));
    if (unicode_is_white_space(c))
    {
      while ((!(lexer_is_eof(*(l)))) && (unicode_is_white_space(lexer_current_rune(*(l)))))
      {
        c = lexer_current_rune(*(l));
        lexer_advance(l);
        if (((c) == (10)) && (untilEndOfLine))
          break;
      }
      ly_source_location endLocation = lexer_current_location(*(l));
      (trivia).kind = ly_laye_trivia_kind__white_space;
      (trivia).sourceSpan = source_span_create(startLocation, endLocation);
    }
    else     if (((c) == (47)) && ((lexer_peek_char(*(l))) == (47)))
    {
      while (!(lexer_is_eof(*(l))))
      {
        c = lexer_current_rune(*(l));
        lexer_advance(l);
        if ((c) == (10))
          break;
      }
      ly_source_location endLocation = lexer_current_location(*(l));
      (trivia).kind = ly_laye_trivia_kind__comment_line;
      (trivia).sourceSpan = source_span_create(startLocation, endLocation);
    }
    else     if (((c) == (47)) && ((lexer_peek_char(*(l))) == (42)))
    {
      lexer_advance(l);
      lexer_advance(l);
      ly_uint_t nesting = 1;
      while (!(lexer_is_eof(*(l))))
      {
        c = lexer_current_rune(*(l));
        lexer_advance(l);
        if (((c) == (47)) && ((lexer_current_rune(*(l))) == (42)))
        {
          lexer_advance(l);
          nesting = (nesting) + (1);
        }
        else         if (((c) == (42)) && ((lexer_current_rune(*(l))) == (47)))
        {
          lexer_advance(l);
          nesting = (nesting) - (1);
          if ((nesting) == (0))
            break;
        }
      }
      ly_source_location endLocation = lexer_current_location(*(l));
      (trivia).sourceSpan = source_span_create(startLocation, endLocation);
      if ((nesting) > (0))
        diagnostics_add_error((*(l)).diagnostics, source_span_create(startLocation, endLocation), LYSTR_STRING("unfinished block comment", 24));
      else
        (trivia).kind = ly_laye_trivia_kind__comment_block;
    }
    else
      break;
    ly_dynamic_t* ly_dyn_11 = &(triviaList);
    ly_internal_dynamic_ensure_capacity(ly_dyn_11, (ly_dyn_11->count + 1) * sizeof(ly_laye_trivia));
    (/*cast*/(ly_laye_trivia*) (ly_dyn_11->data))[ly_dyn_11->count] = (trivia);
    ly_dyn_11->count++;
  }
  ly_slice_9 trivia = ly_internal_dynamicslice_12(triviaList, 0, (triviaList).count);
  ly_dynamic_t* ly_dyn_13 = &(triviaList);
free((ly_dyn_13)->data);
(ly_dyn_13)->data = NULL;
(ly_dyn_13)->count = 0;
(ly_dyn_13)->capacity = 0;
  return(trivia);
}

ly_slice_8 lexer_read_laye_tokens(ly_source source, ly_diagnostic_bag* diagnostics) {
  if (!((source).isValid))
  {
    ly_slice_8 dummyResult = {0};
    return(dummyResult);
  }
  ly_dynamic_t resultTokens = {0};
  ly_lexer_data l = {0};
  (l).diagnostics = diagnostics;
  (l).currentSource = source;
  (l).currentLine = 1;
  (l).currentColumn = 1;
  while (!(lexer_is_eof(l)))
  {
    ly_uint_t currentIndex = (l).currentIndex;
    ly_laye_token token = lexer_read_laye_token(&(l));
    assert((currentIndex) != ((l).currentIndex), LYSTR_STRING("internal Laye lexer error: call to `lexer_read_laye_token` did not consume any characters", 89));
    assert(!(((token).kind).kind == 0), LYSTR_STRING("internal Laye lexer error: call to `lexer_read_laye_token` returned a nil-kinded token", 86));
    ly_dynamic_t* ly_dyn_14 = &(resultTokens);
    ly_internal_dynamic_ensure_capacity(ly_dyn_14, (ly_dyn_14->count + 1) * sizeof(ly_laye_token));
    (/*cast*/(ly_laye_token*) (ly_dyn_14->data))[ly_dyn_14->count] = (token);
    ly_dyn_14->count++;
  }
  ly_slice_8 tokens = ly_internal_dynamicslice_15(resultTokens, 0, (resultTokens).count);
  ly_dynamic_t* ly_dyn_16 = &(resultTokens);
free((ly_dyn_16)->data);
(ly_dyn_16)->data = NULL;
(ly_dyn_16)->count = 0;
(ly_dyn_16)->capacity = 0;
  return(tokens);
}

ly_laye_token lexer_read_laye_token(ly_lexer_data* l) {
  assert(!(lexer_is_eof(*(l))), LYSTR_STRING("lexer_read_laye_token called when at EoF", 40));
  ly_laye_token token = {0};
  ly_slice_9 leadingTrivia = lexer_get_laye_trivia(l, false);
  (token).leadingTrivia = leadingTrivia;
  int32_t c = lexer_current_rune(*(l));
  ly_source_location startLocation = lexer_current_location(*(l));
  if ((c) == (0))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__eof};
    (token).sourceSpan = source_span_create(startLocation, lexer_current_location(*(l)));
    return(token);
  }
  if (rune_is_laye_identifier_part(c))
    lexer_read_laye_identifier_or_number(l, &(token));
  else   if ((c) == (33))
  {
    lexer_advance(l);
    if ((lexer_current_rune(*(l))) == (60))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__bang_less};
    }
    else     if ((lexer_current_rune(*(l))) == (61))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__bang_equal};
    }
    else     if ((lexer_current_rune(*(l))) == (62))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__bang_greater};
    }
    else {
  (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__poison_token};
  diagnostics_add_error((*(l)).diagnostics, source_span_create(startLocation, lexer_current_location(*(l))), LYSTR_STRING("invalid token `!` (did you mean `not`?)", 39));
}
  }
  else   if ((c) == (34))
    lexer_read_laye_string(l, &(token));
  else   if ((c) == (37))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__percent};
  }
  else   if ((c) == (38))
  {
    lexer_advance(l);
    if ((lexer_current_rune(*(l))) == (38))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__poison_token};
      ly_source_span ss = source_span_create(startLocation, lexer_current_location(*(l)));
      diagnostics_add_warning((*(l)).diagnostics, ss, LYSTR_STRING("invalid token `&&` (did you mean `and`?)", 40));
      diagnostics_add_info((*(l)).diagnostics, ss, LYSTR_STRING("`&` can be either the bitwise and infix operator or the address-of prefix operator; `&&` could only legally appear as taking an address of an address inline, which is incredibly unlikely and must instead be written as `&(&variable)` if needed to avoid the much more common mistake of using `&&` as the logical and infix operator", 328));
    }
    else
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__amp};
  }
  else   if ((c) == (40))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__open_paren};
  }
  else   if ((c) == (41))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__close_paren};
  }
  else   if ((c) == (42))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__star};
  }
  else   if ((c) == (43))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__plus};
  }
  else   if ((c) == (44))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__comma};
  }
  else   if ((c) == (45))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__minus};
  }
  else   if ((c) == (46))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__dot};
  }
  else   if ((c) == (47))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__slash};
  }
  else   if ((c) == (58))
  {
    lexer_advance(l);
    if ((lexer_current_rune(*(l))) == (58))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__colon_colon};
    }
    else
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__colon};
  }
  else   if ((c) == (59))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__semi_colon};
  }
  else   if ((c) == (60))
  {
    lexer_advance(l);
    if ((lexer_current_rune(*(l))) == (60))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__less_less};
    }
    else     if ((lexer_current_rune(*(l))) == (61))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__less_equal};
    }
    else
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__less};
  }
  else   if ((c) == (61))
  {
    lexer_advance(l);
    if ((lexer_current_rune(*(l))) == (61))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__equal_equal};
    }
    else
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__equal};
  }
  else   if ((c) == (62))
  {
    lexer_advance(l);
    if ((lexer_current_rune(*(l))) == (62))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__greater_greater};
    }
    else     if ((lexer_current_rune(*(l))) == (61))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__greater_equal};
    }
    else
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__greater};
  }
  else   if ((c) == (91))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__open_bracket};
  }
  else   if ((c) == (93))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__close_bracket};
  }
  else   if ((c) == (123))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__open_brace};
  }
  else   if ((c) == (124))
  {
    lexer_advance(l);
    if ((lexer_current_rune(*(l))) == (124))
    {
      lexer_advance(l);
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__poison_token};
      ly_source_span ss = source_span_create(startLocation, lexer_current_location(*(l)));
      diagnostics_add_warning((*(l)).diagnostics, ss, LYSTR_STRING("invalid token `||` (did you mean `or`?)", 39));
      diagnostics_add_info((*(l)).diagnostics, ss, LYSTR_STRING("`|` is the bitwise or infix operator; `||` could never legally appear in an expression", 86));
    }
    else
      (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__pipe};
  }
  else   if ((c) == (125))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__close_brace};
  }
  else   if ((c) == (126))
  {
    lexer_advance(l);
    (token).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__tilde};
  }
  if (((token).kind).kind == 0)
  {
    lexer_advance(l);
    ly_string_builder errorBuilder = {0};
    string_builder_append_string(&(errorBuilder), LYSTR_STRING("unrecognized character `", 24));
    string_builder_append_rune(&(errorBuilder), c);
    string_builder_append_string(&(errorBuilder), LYSTR_STRING("` (U+", 5));
    string_builder_append_uint_hexn(&(errorBuilder), (ly_uint_t)c, 4);
    string_builder_append_string(&(errorBuilder), LYSTR_STRING(") in lexer", 10));
    diagnostics_add_error((*(l)).diagnostics, source_span_create(startLocation, lexer_current_location(*(l))), string_builder_to_string(errorBuilder));
    string_builder_free(&(errorBuilder));
  }
  (token).sourceSpan = source_span_create(startLocation, lexer_current_location(*(l)));
  ly_slice_9 trailingTrivia = lexer_get_laye_trivia(l, true);
  (token).trailingTrivia = trailingTrivia;
  return(token);
}

void lexer_read_laye_identifier_or_number(ly_lexer_data* l, ly_laye_token* token) {
  assert(rune_is_laye_identifier_part(lexer_current_rune(*(l))), LYSTR_STRING("lexer_read_laye_token called without identifier char", 52));
  int32_t lastRune = lexer_current_rune(*(l));
  ly_bool8_t isStillNumber = ((lastRune) == (95)) || (rune_is_laye_digit(lastRune));
  ly_bool8_t doesNumberHaveInvalidUnderscorePlacement = (lastRune) == (95);
  uint64_t currentIntegerValue = 0;
  ly_source_location startLocation = lexer_current_location(*(l));
  while ((!(lexer_is_eof(*(l)))) && (rune_is_laye_identifier_part(lexer_current_rune(*(l)))))
  {
    lastRune = lexer_current_rune(*(l));
    lexer_advance(l);
    if (isStillNumber)
    {
      isStillNumber = ((lastRune) == (95)) || (rune_is_laye_digit(lastRune));
      if ((isStillNumber) && ((lastRune) != (95)))
      {
        currentIntegerValue = (currentIntegerValue) * (10);
        currentIntegerValue = (currentIntegerValue) + ((uint64_t)rune_laye_digit_value(lastRune));
      }
    }
  }
  if (isStillNumber)
  {
    doesNumberHaveInvalidUnderscorePlacement = (lastRune) == (95);
    if ((lexer_current_rune(*(l))) == (35))
      lexer_read_laye_radix_integer_from_delimiter(l, token, startLocation, currentIntegerValue);
    else {
  (*(token)).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__literal_integer, .variants.literal_integer.value = currentIntegerValue};
}
  }
  else {
  ly_source_location endLocation = lexer_current_location(*(l));
  (*(token)).sourceSpan = source_span_create(startLocation, endLocation);
  ly_string ident = source_span_to_string((*(token)).sourceSpan);
  (*(token)).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__ident, .variants.ident.image = ident};
}
}

void lexer_read_laye_radix_integer_from_delimiter(ly_lexer_data* l, ly_laye_token* token, ly_source_location startLocation, uint64_t radix) {
}

void lexer_read_laye_string(ly_lexer_data* l, ly_laye_token* token) {
  assert((lexer_current_rune(*(l))) == (34), LYSTR_STRING("lexer_read_laye_string called without quote char", 48));
  lexer_advance(l);
  ly_source_location startLocation = lexer_current_location(*(l));
  ly_string_builder sb = {0};
  while ((!(lexer_is_eof(*(l)))) && ((lexer_current_rune(*(l))) != (34)))
  {
    int32_t c = lexer_current_rune(*(l));
    lexer_advance(l);
    string_builder_append_rune(&(sb), c);
  }
  if ((lexer_current_rune(*(l))) == (34))
  {
    lexer_advance(l);
    (*(token)).kind = (ly_laye_token_kind){.kind = ly_laye_token_kind_Kinds__literal_string, .variants.literal_string.value = string_builder_to_string(sb)};
    string_builder_free(&(sb));
  }
  else {
  ly_string locationString = source_location_to_string(startLocation);
  printf("%.*s: error: unfinished string literal%c", (locationString).length, (locationString).data, 10);
}
}

ly_source source_create_from_file(ly_string fileName) {
  int32_t ly_SEEK_SET_270 = 0;
  int32_t ly_SEEK_END_271 = 2;
  ly_source resultSource = {0};
  (resultSource).name = fileName;
  void* fileHandle = fopen((uint8_t*)string_to_cstring(fileName), "rb");
  if ((fileHandle) == (NULL))
    return(resultSource);
  fseek(fileHandle, 0, ly_SEEK_END_271);
  int64_t fileSize = ftell(fileHandle);
  fseek(fileHandle, 0, ly_SEEK_SET_270);
  uint8_t* fileBuffer = (uint8_t*)malloc((ly_uint_t)fileSize);
  memset((void*)fileBuffer, 0, (ly_uint_t)fileSize);
  ly_uint_t totalReadCount = fread((void*)fileBuffer, (ly_uint_t)fileSize, 1, fileHandle);
  if ((totalReadCount) != ((ly_uint_t)fileSize))
  {
    if ((ferror(fileHandle)) != (0))
    {
      perror("the following error occurred while reading source file:");
      fclose(fileHandle);
      return(resultSource);
    }
  }
  fclose(fileHandle);
  ly_string sourceText = ly_internal_slicetostring(ly_internal_bufferslice_10(fileBuffer, 0, (ly_uint_t)fileSize));
  (resultSource).text = sourceText;
  (resultSource).isValid = true;
  return(resultSource);
}

ly_string source_location_name_get(ly_source_location sl) {
  return(((sl).source).name);
}

ly_uint_t source_location_line_get(ly_source_location sl) {
  return((sl).lineNumber);
}

ly_uint_t source_location_column_get(ly_source_location sl) {
  return((sl).columnNumber);
}

ly_string source_location_to_string(ly_source_location sl) {
  ly_string slName = source_location_name_get(sl);
  ly_uint_t lineDigitCount = uint_num_digits(source_location_line_get(sl));
  ly_uint_t colDigitCount = uint_num_digits(source_location_column_get(sl));
  ly_string_builder sb = {0};
  string_builder_ensure_capacity(&(sb), ((((slName).length) + (lineDigitCount)) + (colDigitCount)) + (2));
  string_builder_append_string(&(sb), slName);
  string_builder_append_string(&(sb), LYSTR_STRING(":", 1));
  string_builder_append_uint(&(sb), source_location_line_get(sl));
  string_builder_append_string(&(sb), LYSTR_STRING(":", 1));
  string_builder_append_uint(&(sb), source_location_column_get(sl));
  ly_string result = string_builder_to_string(sb);
  string_builder_free(&(sb));
  return(result);
}

ly_source_span source_span_create(ly_source_location start, ly_source_location end) {
  ly_source_span result = {0};
  (result).startLocation = start;
  (result).endLocation = end;
  return(result);
}

ly_string source_span_name_get(ly_source_span ss) {
  return(source_location_name_get((ss).startLocation));
}

ly_string source_span_to_string(ly_source_span ss) {
  ly_string sourceText = (((ss).startLocation).source).text;
  return(ly_internal_substring(sourceText, ((ss).startLocation).characterIndex, (((ss).endLocation).characterIndex) - (((ss).startLocation).characterIndex)));
}


// entry point

int main(int argc, char** argv) {
  ly_creservedfn_main((int32_t)argc, (uint8_t**)argv);
  return 0;
}
