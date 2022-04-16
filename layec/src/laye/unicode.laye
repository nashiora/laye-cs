
uint unicode_utf8_calc_encoded_byte_count(u8 byte1)
{
    if ((byte1 & 128 /* 2#1000_0000 */) == 0)
        return 1; // it's in the lower 7 bits, ASCII space
    else if ((byte1 & 224 /* 2#1110_0000 */) == 192 /* 2#110 << 5 */)
        return 2;
    else if ((byte1 & 240 /* 2#1111_0000 */) == 224 /* 2#1110_0000 */)
        return 3;
    else if ((byte1 & 248 /* 2#1111_1000 */) == 240 /* 2#1111_0000 */)
        return 4;
    else return 0; // not a valid UTF-8 byte1
}

uint unicode_utf8_calc_rune_byte_count(i32 r)
{
    if (r <= 127) // 16#007F
        return 1;
    else if (r <= 2047) // 16#07FF
        return 2;
    else if (r <= 65535) // 16#FFFF
        return 3;
    else return 4; // up to 16#10FFFF
}

i32 unicode_utf8_string_rune_at_index(string s, uint index)
{
    if (s.length <= index)
        return 0; // no valid character at that index, don't even try

    uint expectedByteCount = unicode_utf8_calc_encoded_byte_count(s[index]);
    if (expectedByteCount == 0)
        return 0; // invalid rune start

    if (index + expectedByteCount >= s.length)
        return 0; // not enough bytes left to read

    if (expectedByteCount == 1)
        return cast(i32) s[index]; // easy
    else if (expectedByteCount == 2)
    {
        i32 byte1 = cast(i32) s[index];
        i32 byte2 = cast(i32) s[index + 1];

        return (byte1 & 31 /* 2#0001_1111 */ << 6) | (byte2 & 63 /* 2#0011_1111 */);
    }
    else if (expectedByteCount == 3)
    {
        i32 byte1 = cast(i32) s[index];
        i32 byte2 = cast(i32) s[index + 1];
        i32 byte3 = cast(i32) s[index + 2];

        return (byte1 & 15 /* 2#0000_1111 */ << 12) | (byte2 & 63 /* 2#0011_1111 */ << 6) | (byte3 & 63 /* 2#0011_1111 */);
    }
    else// if (expectedByteCount == 4)
    {
        i32 byte1 = cast(i32) s[index];
        i32 byte2 = cast(i32) s[index + 1];
        i32 byte3 = cast(i32) s[index + 2];
        i32 byte4 = cast(i32) s[index + 3];

        return (byte1 & 7 /* 2#0000_0111 */ << 18) | (byte2 & 63 /* 2#0011_1111 */ << 12) | (byte3 & 63 /* 2#0011_1111 */ << 6) | (byte4 & 63 /* 2#0011_1111 */);
    }
}

// TODO(local): actually lookup the unicode categories
// NOTE(local): currently just handling ASCII ranges

bool unicode_is_digit(i32 r)
{
    return r >= 48 and r < 58;
}

bool unicode_is_letter(i32 r)
{
    return (r >= 65 /* A */ and r <= 90 /* Z */) or (r >= 97 /* a */ and r <= 122 /* z */);
}

bool unicode_is_white_space(i32 r)
{
    return r == 9 /* \t */ or r == 10 /* \n */ or r == 11 /* \v */ or r == 13 /* \r */ or r == 32 /* ' ' */;
}
