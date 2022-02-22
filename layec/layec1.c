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
typedef struct ly_slice_3 ly_slice_3; /* laye type: string[] */
typedef struct ly_source ly_source;
typedef struct ly_laye_token_list ly_laye_token_list;
typedef struct ly_laye_token ly_laye_token;
typedef struct ly_source_span ly_source_span;
typedef struct ly_source_location ly_source_location;
typedef struct ly_slice_5 ly_slice_5; /* laye type: laye_trivia[] */
typedef struct ly_laye_trivia ly_laye_trivia;
typedef struct ly_slice_6 ly_slice_6; /* laye type: laye_token[] */
typedef struct ly_string_builder ly_string_builder;
typedef struct ly_lexer_data ly_lexer_data;
typedef struct ly_laye_trivia_list ly_laye_trivia_list;

// type declarations
struct ly_string {
  size_t length;
  uint8_t* data;
};

struct ly_slice_1 {
  size_t length;
  uint8_t* data;
};

struct ly_slice_3 {
  size_t length;
  ly_string* data;
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

struct ly_laye_trivia {
  ly_source_span sourceSpan;
  ly_bool8_t isValid;
};

struct ly_slice_5 {
  size_t length;
  ly_laye_trivia* data;
};

struct ly_laye_token {
  ly_source_span sourceSpan;
  ly_slice_5 leadingTrivia;
  ly_slice_5 trailingTrivia;
  ly_bool8_t isValid;
};

struct ly_laye_token_list {
  ly_laye_token* buffer;
  ly_uint_t capacity;
  ly_uint_t length;
  ly_bool8_t isValid;
};

struct ly_slice_6 {
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
};

struct ly_laye_trivia_list {
  ly_laye_trivia* buffer;
  ly_uint_t capacity;
  ly_uint_t length;
  ly_bool8_t isValid;
};


// function prototypes
ly_string ly_internal_substring(ly_string s_, ly_uint_t o_, ly_uint_t l_);
ly_string ly_internal_slicetostring(ly_slice_1 s_);
void ly_creservedfn_main(int32_t argc, uint8_t** argv);
ly_slice_1 ly_internal_bufferslice_2(uint8_t* b_, ly_uint_t o_, ly_uint_t l_);
ly_slice_3 ly_internal_bufferslice_4(ly_string* b_, ly_uint_t o_, ly_uint_t l_);
void laye_main(ly_slice_3 args);
void panic(ly_string message);
void assert(ly_bool8_t test, ly_string message);
ly_uint_t uint_num_digits(ly_uint_t n);
ly_uint_t int_num_digits(ly_int_t n);
void* memory_allocate(ly_uint_t byteCount);
uint8_t* string_to_cstring(ly_string s);
ly_string string_concat(ly_string a, ly_string b);
ly_string uint_to_string(ly_uint_t v);
ly_bool8_t char_is_digit(int32_t c);
ly_bool8_t char_is_alpha(int32_t c);
ly_bool8_t char_is_white_space(int32_t c);
ly_uint_t string_builder_length_get(ly_string_builder sb);
ly_uint_t string_builder_capacity_get(ly_string_builder sb);
uint8_t* string_builder_data_get(ly_string_builder sb);
void string_builder_ensure_capacity(ly_string_builder* sb, ly_uint_t desired_capacity);
ly_string string_builder_to_string(ly_string_builder sb);
ly_slice_1 ly_internal_bufferslice_7(uint8_t* b_, ly_uint_t o_, ly_uint_t l_);
void string_builder_append_string(ly_string_builder* sb, ly_string v);
void string_builder_append_character(ly_string_builder* sb, uint8_t c);
void string_builder_append_uint(ly_string_builder* sb, ly_uint_t v);
ly_bool8_t char_is_laye_identifier_part(int32_t c);
ly_bool8_t lexer_is_eof(ly_lexer_data l);
int32_t lexer_current_char(ly_lexer_data l);
int32_t lexer_peek_char(ly_lexer_data l);
ly_source_location lexer_current_location(ly_lexer_data l);
void lexer_advance(ly_lexer_data* l);
ly_slice_5 lexer_get_laye_trivia(ly_lexer_data* l, ly_bool8_t untilEndOfLine);
ly_laye_token_list lexer_read_laye_tokens(ly_source source);
ly_laye_token lexer_read_laye_token(ly_lexer_data* l);
ly_laye_token lexer_read_laye_identifier(ly_lexer_data* l);
ly_slice_5 laye_trivia_list_get_trivia(ly_laye_trivia_list trivia);
ly_slice_5 ly_internal_bufferslice_8(ly_laye_trivia* b_, ly_uint_t o_, ly_uint_t l_);
void laye_trivia_list_append_trivia(ly_laye_trivia_list* trivia, ly_laye_trivia trivium);
ly_slice_6 laye_token_list_get_tokens(ly_laye_token_list tokens);
ly_slice_6 ly_internal_bufferslice_9(ly_laye_token* b_, ly_uint_t o_, ly_uint_t l_);
void laye_token_list_append_token(ly_laye_token_list* tokens, ly_laye_token token);
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
ly_slice_1 ly_internal_bufferslice_2(uint8_t* b_, ly_uint_t o_, ly_uint_t l_)
{
  ly_slice_1 result;
  result.data = b_ + o_;
  result.length = l_;
  return result;
}
ly_slice_3 ly_internal_bufferslice_4(ly_string* b_, ly_uint_t o_, ly_uint_t l_)
{
  ly_slice_3 result;
  result.data = b_ + o_;
  result.length = l_;
  return result;
}
ly_slice_1 ly_internal_bufferslice_7(uint8_t* b_, ly_uint_t o_, ly_uint_t l_)
{
  ly_slice_1 result;
  result.data = b_ + o_;
  result.length = l_;
  return result;
}
ly_slice_5 ly_internal_bufferslice_8(ly_laye_trivia* b_, ly_uint_t o_, ly_uint_t l_)
{
  ly_slice_5 result;
  result.data = b_ + o_;
  result.length = l_;
  return result;
}
ly_slice_6 ly_internal_bufferslice_9(ly_laye_token* b_, ly_uint_t o_, ly_uint_t l_)
{
  ly_slice_6 result;
  result.data = b_ + o_;
  result.length = l_;
  return result;
}

void ly_creservedfn_main(int32_t argc, uint8_t** argv) {
  ly_string* argvStorage = (ly_string*)memory_allocate((sizeof(ly_string)) * ((ly_uint_t)argc));
  ly_uint_t argvCounter = 0;
  while ((argvCounter) < ((ly_uint_t)argc))
  {
    (argvStorage)[argvCounter] = ly_internal_slicetostring(ly_internal_bufferslice_2((argv)[argvCounter], 0, strlen((argv)[argvCounter])));
    argvCounter = (argvCounter) + (1);
  }
  ly_slice_3 args = ly_internal_bufferslice_4(argvStorage, 0, argc);
  laye_main(args);
}

void laye_main(ly_slice_3 args) {
  printf("Laye stand-alone compiler%cVersion 0.1.0%c", 10, 10);
  ly_source sourceFile = source_create_from_file(LYSTR_STRING("./layec/src/main.ly", 19));
  if (!((sourceFile).isValid))
  {
    printf("failed to open file%c", 10);
    return;
  }
  ly_laye_token_list sourceTokenList = lexer_read_laye_tokens(sourceFile);
  if (!((sourceTokenList).isValid))
  {
    printf("failed to read tokens from file%c", 10);
  }
  ly_slice_6 sourceTokens = laye_token_list_get_tokens(sourceTokenList);
  printf("read %llu tokens from file successfully%c", (sourceTokens).length, 10);
  {
    ly_uint_t i = 0;
    while ((i) < ((sourceTokens).length))
    {
      ly_laye_token token = (sourceTokens).data[i];
      ly_string tokenLocationString = source_location_to_string(((token).sourceSpan).startLocation);
      ly_string tokenString = source_span_to_string((token).sourceSpan);
      printf("> token (");
      printf("%.*s", (tokenLocationString).length, (tokenLocationString).data);
      printf(")%c", 10);
      printf("  `%.*s`%c", (tokenString).length, (tokenString).data, 10);
      {
        printf("    leading trivia%c", 10);
        ly_uint_t j = 0;
        while ((j) < (((token).leadingTrivia).length))
        {
          ly_laye_trivia trivia = ((token).leadingTrivia).data[j];
          ly_string triviaString = source_span_to_string((trivia).sourceSpan);
          printf("    `%.*s`%c", (triviaString).length, (triviaString).data, 10);
          j = (j) + (1);
        }
      }
      {
        printf("    trailing trivia%c", 10);
        ly_uint_t j = 0;
        while ((j) < (((token).trailingTrivia).length))
        {
          ly_laye_trivia trivia = ((token).trailingTrivia).data[j];
          ly_string triviaString = source_span_to_string((trivia).sourceSpan);
          printf("    `%.*s`%c", (triviaString).length, (triviaString).data, 10);
          j = (j) + (1);
        }
      }
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
  return(string_builder_to_string(builder));
}

ly_string uint_to_string(ly_uint_t v) {
  ly_string_builder builder = {0};
  string_builder_append_uint(&(builder), v);
  return(string_builder_to_string(builder));
}

ly_bool8_t char_is_digit(int32_t c) {
  return(((c) >= (48)) && ((c) < (58)));
}

ly_bool8_t char_is_alpha(int32_t c) {
  return((((c) >= (65)) && ((c) <= (90))) || (((c) >= (97)) && ((c) <= (122))));
}

ly_bool8_t char_is_white_space(int32_t c) {
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
  return(ly_internal_slicetostring(ly_internal_bufferslice_7(result_data, 0, length)));
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

ly_bool8_t char_is_laye_identifier_part(int32_t c) {
  return(((char_is_digit(c)) || (char_is_alpha(c))) || ((c) == (95)));
}

ly_bool8_t lexer_is_eof(ly_lexer_data l) {
  return(((l).currentIndex) >= ((((l).currentSource).text).length));
}

int32_t lexer_current_char(ly_lexer_data l) {
  if (lexer_is_eof(l))
    return(0);
  return((int32_t)(((l).currentSource).text).data[(l).currentIndex]);
}

int32_t lexer_peek_char(ly_lexer_data l) {
  if ((((l).currentIndex) + (1)) >= ((((l).currentSource).text).length))
    return(0);
  return((int32_t)(((l).currentSource).text).data[((l).currentIndex) + (1)]);
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
  int32_t c = lexer_current_char(*(l));
  (*(l)).currentIndex = ((*(l)).currentIndex) + (1);
  if ((c) == (10))
  {
    (*(l)).currentLine = ((*(l)).currentLine) + (1);
    (*(l)).currentColumn = 1;
  }
  else
    (*(l)).currentColumn = ((*(l)).currentColumn) + (1);
}

ly_slice_5 lexer_get_laye_trivia(ly_lexer_data* l, ly_bool8_t untilEndOfLine) {
  ly_laye_trivia_list triviaList = {0};
  while (!(lexer_is_eof(*(l))))
  {
    ly_laye_trivia trivia = {0};
    ly_source_location startLocation = lexer_current_location(*(l));
    int32_t c = lexer_current_char(*(l));
    if (char_is_white_space(c))
    {
      while ((!(lexer_is_eof(*(l)))) && (char_is_white_space(lexer_current_char(*(l)))))
      {
        c = lexer_current_char(*(l));
        lexer_advance(l);
        if (((c) == (10)) && (untilEndOfLine))
          break;
      }
      ly_source_location endLocation = lexer_current_location(*(l));
      (trivia).isValid = true;
      (trivia).sourceSpan = source_span_create(startLocation, endLocation);
    }
    else     if (((c) == (47)) && ((lexer_peek_char(*(l))) == (47)))
    {
      while (!(lexer_is_eof(*(l))))
      {
        c = lexer_current_char(*(l));
        lexer_advance(l);
        if ((c) == (10))
          break;
      }
      ly_source_location endLocation = lexer_current_location(*(l));
      (trivia).isValid = true;
      (trivia).sourceSpan = source_span_create(startLocation, endLocation);
    }
    else     if (((c) == (47)) && ((lexer_peek_char(*(l))) == (42)))
    {
      lexer_advance(l);
      lexer_advance(l);
      ly_uint_t nesting = 1;
      while (!(lexer_is_eof(*(l))))
      {
        c = lexer_current_char(*(l));
        lexer_advance(l);
        if (((c) == (47)) && ((lexer_current_char(*(l))) == (42)))
        {
          lexer_advance(l);
          nesting = (nesting) + (1);
        }
        else         if (((c) == (42)) && ((lexer_current_char(*(l))) == (47)))
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
      {
        ly_string locationString = source_location_to_string(endLocation);
        printf("%.*s: error: unfinished block comment%c", (locationString).length, (locationString).data, 10);
        (trivia).isValid = false;
        break;
      }
      else
        (trivia).isValid = true;
    }
    else
      break;
    if ((trivia).isValid)
      laye_trivia_list_append_trivia(&(triviaList), trivia);
    else
      break;
  }
  return(laye_trivia_list_get_trivia(triviaList));
}

ly_laye_token_list lexer_read_laye_tokens(ly_source source) {
  ly_laye_token_list resultTokens = {0};
  if (!((source).isValid))
    return(resultTokens);
  ly_lexer_data l = {0};
  (l).currentSource = source;
  (l).currentLine = 1;
  (l).currentColumn = 1;
  while (!(lexer_is_eof(l)))
  {
    printf("  reading token...%c", 10);
    ly_uint_t currentIndex = (l).currentIndex;
    ly_laye_token token = lexer_read_laye_token(&(l));
    if ((currentIndex) == ((l).currentIndex))
    {
      printf("  internal Laye lexer error: call to `lexer_read_laye_token` did not consume any characters%c", 10);
      return(resultTokens);
    }
    if (!((token).isValid))
      return(resultTokens);
    laye_token_list_append_token(&(resultTokens), token);
  }
  (resultTokens).isValid = true;
  return(resultTokens);
}

ly_laye_token lexer_read_laye_token(ly_lexer_data* l) {
  assert(!(lexer_is_eof(*(l))), LYSTR_STRING("lexer_read_laye_token called when at EoF", 40));
  ly_laye_token token = {0};
  ly_slice_5 leadingTrivia = lexer_get_laye_trivia(l, false);
  (token).leadingTrivia = leadingTrivia;
  int32_t c = lexer_current_char(*(l));
  if (char_is_laye_identifier_part(c))
  {
    token = lexer_read_laye_identifier(l);
  }
  else {
  ly_source_location location = lexer_current_location(*(l));
  ly_string locationString = source_location_to_string(location);
  printf("%.*s: error: unrecognized character `%c` in lexer%c", (locationString).length, (locationString).data, c, 10);
  lexer_advance(l);
  (token).sourceSpan = source_span_create(location, lexer_current_location(*(l)));
  (token).isValid = false;
}
  ly_slice_5 trailingTrivia = lexer_get_laye_trivia(l, true);
  (token).trailingTrivia = trailingTrivia;
  return(token);
}

ly_laye_token lexer_read_laye_identifier(ly_lexer_data* l) {
  assert(char_is_laye_identifier_part(lexer_current_char(*(l))), LYSTR_STRING("lexer_read_laye_token called when at EoF", 40));
  ly_laye_token token = {0};
  ly_source_location startLocation = lexer_current_location(*(l));
  while ((!(lexer_is_eof(*(l)))) && (char_is_laye_identifier_part(lexer_current_char(*(l)))))
    lexer_advance(l);
  ly_source_location endLocation = lexer_current_location(*(l));
  (token).isValid = true;
  (token).sourceSpan = source_span_create(startLocation, endLocation);
  return(token);
}

ly_slice_5 laye_trivia_list_get_trivia(ly_laye_trivia_list trivia) {
  return(ly_internal_bufferslice_8((trivia).buffer, 0, (trivia).length));
}

void laye_trivia_list_append_trivia(ly_laye_trivia_list* trivia, ly_laye_trivia trivium) {
  if (((*(trivia)).length) == ((*(trivia)).capacity))
  {
    ly_uint_t capacity = {0};
    if (((*(trivia)).capacity) == (0))
      capacity = 32;
    else
      capacity = ((*(trivia)).capacity) * (2);
    (*(trivia)).buffer = (ly_laye_trivia*)realloc((void*)(*(trivia)).buffer, (capacity) * (sizeof(ly_laye_trivia)));
    (*(trivia)).capacity = capacity;
  }
  ((*(trivia)).buffer)[(*(trivia)).length] = trivium;
  (*(trivia)).length = ((*(trivia)).length) + (1);
}

ly_slice_6 laye_token_list_get_tokens(ly_laye_token_list tokens) {
  return(ly_internal_bufferslice_9((tokens).buffer, 0, (tokens).length));
}

void laye_token_list_append_token(ly_laye_token_list* tokens, ly_laye_token token) {
  if (((*(tokens)).length) == ((*(tokens)).capacity))
  {
    if (((*(tokens)).capacity) == (0))
    {
      (*(tokens)).capacity = 32;
      (*(tokens)).buffer = (ly_laye_token*)malloc((32) * (sizeof(ly_laye_token)));
    }
    else {
  (*(tokens)).capacity = ((*(tokens)).capacity) * (2);
  (*(tokens)).buffer = (ly_laye_token*)realloc((void*)(*(tokens)).buffer, ((*(tokens)).capacity) * (sizeof(ly_laye_token)));
}
  }
  ((*(tokens)).buffer)[(*(tokens)).length] = token;
  (*(tokens)).length = ((*(tokens)).length) + (1);
}

ly_source source_create_from_file(ly_string fileName) {
  int32_t ly_SEEK_SET_150 = 0;
  int32_t ly_SEEK_END_151 = 2;
  ly_source resultSource = {0};
  (resultSource).name = fileName;
  void* fileHandle = fopen((uint8_t*)string_to_cstring(fileName), "rb");
  if ((fileHandle) == (NULL))
    return(resultSource);
  fseek(fileHandle, 0, ly_SEEK_END_151);
  int64_t fileSize = ftell(fileHandle);
  fseek(fileHandle, 0, ly_SEEK_SET_150);
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
  ly_string sourceText = ly_internal_slicetostring(ly_internal_bufferslice_7(fileBuffer, 0, (ly_uint_t)fileSize));
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
  return(string_builder_to_string(sb));
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
