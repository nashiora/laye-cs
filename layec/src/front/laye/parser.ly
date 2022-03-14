enum laye_delimiter_kind
{
    comma,
    semi_colon,

    close_paren,

    open_brace,
    close_brace,
}

bool parser_read_all_laye_nodes(parser_data *p, syntax_node *[dynamic] *nodes)
{
    if (not p.lexer.source.isValid)
        return false;

    p.parseContext = ::laye;
    parser_advance(p); // prime the parser with the first token

    syntax_node *lastAnnotation;

    while (not parser_is_eof(p))
    {
        syntax_token tkCurrent = parser_current_token(p);

        syntax_node *topLevelNode = parser_read_laye_top_level_declaration(p);
        if (cast(rawptr) topLevelNode == nullptr)
        {
            diagnostics_add_error(p.diagnostics, ::token(tkCurrent), "internal compiler error: top level node was not allocated");
            return false;
        }

        if (topLevelNode.kind is nil)
        {
            diagnostics_add_error(p.diagnostics, ::token(tkCurrent), "internal compiler error: failed to parse top level node");
            return false;
        }

        if (topLevelNode.kind is ::statement_expression_unfinished expressionStatement)
        {
            if (expressionStatement.expression.kind is ::expression_unknown_token)
            {
                diagnostics_add_error(p.diagnostics, ::syntax(topLevelNode), "internal compiler error: stopping the compiler when an expression fails on an invalid token");
                return false;   
            }
        }

        string topLevelNodeKindString = nameof_variant(topLevelNode.kind);
        printf("%.*s%c", topLevelNodeKindString.length, topLevelNodeKindString.data, 10);

        dynamic_append(*nodes, topLevelNode);
    }

    return true;
}

bool parser_expect_laye_identifier(parser_data *p, syntax_token *result)
{
    *result = parser_current_token(p);
    if (result.kind is ::laye_identifier)
    {
        parser_advance(p);
        return true;
    }

    return false;
}

/* A Laye annotation begins with `#[` and ends with `]` and contains either compile time statements or custom data.
 * A common compile time statement is the `if` annotation used for conditional compilation.
 * The `if` annotation requires an expression context to be enabled in the parser so some identifiers conditionally become keywords.
 */
syntax_node *parser_read_laye_annotation(parser_data *p)
{
    syntax_token tkOpenAnnotation = parser_current_token(p);

    // TODO(local): is expression
    // TODO(local): assert
    if (tkOpenAnnotation.kind is not ::hash_open_bracket)
        panic("parser_read_laye_annotation called without `#[` delimiter");

    syntax_node *result = syntax_node_alloc();
    // TODO(local): defer
    // defer return result;

    parser_advance(p); // `#[`

    if (parser_is_eof(p))
    {
        result.sourceSpan = tkOpenAnnotation.sourceSpan;
        result.kind = ::annotation_only_open(tkOpenAnnotation);

        diagnostics_add_error(p.diagnostics, ::syntax(result), "unexpected end of file in annotation");

        return result;
    }

    syntax_token tkCurrent = parser_current_token(p);
    if (tkCurrent.kind is ::close_bracket)
    {
        syntax_token tkCloseEmpty = parser_current_token(p);
        parser_advance(p); // `]`

        result.sourceSpan = source_span_combine(tkOpenAnnotation.sourceSpan, tkCloseEmpty.sourceSpan);
        result.kind = ::annotation_empty(tkOpenAnnotation, tkCloseEmpty);

        diagnostics_add_error(p.diagnostics, ::syntax(result), "annotations must not be empty");

        return result;
    }
    else if (tkCurrent.kind is ::kw_if)
    {
        syntax_token tkIf = tkCurrent;
        parser_advance(p); // `if`

        bool previousFlag = p.lexer.contextLayeCompileTimeExpression;
        p.lexer.contextLayeCompileTimeExpression = true;

        syntax_node *condition = parser_read_laye_expression(p);

        p.lexer.contextLayeCompileTimeExpression = previousFlag;

        syntax_token tkCloseAnnotation;

        tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::close_bracket)
        {
            tkCloseAnnotation = tkCurrent;
            parser_advance(p); // `]`
        }
        else
        {
            source_location location = tkCurrent.sourceSpan.startLocation;
            tkCloseAnnotation.sourceSpan = source_span_create(location, location);
            tkCloseAnnotation.kind = ::poison_token;

            diagnostics_add_error(p.diagnostics, ::token(tkCurrent), "']' expected");
        }

        result.sourceSpan = source_span_combine(tkOpenAnnotation.sourceSpan, tkCloseAnnotation.sourceSpan);
        result.kind = ::annotation_conditional(tkOpenAnnotation, tkIf, condition, tkCloseAnnotation);

        return result;
    }
    else if (tkCurrent.kind is ::laye_identifier)
    {
        syntax_token identifier = tkCurrent;
        parser_advance(p); // laye_identifier

        tkCurrent = parser_current_token(p);
        // TODO(local): parse annotation identifier invocation

        if (tkCurrent.kind is not ::close_bracket)
        {
            result.sourceSpan = source_span_combine(tkOpenAnnotation.sourceSpan, identifier.sourceSpan);
            result.kind = ::annotation_identifier_unclosed(tkOpenAnnotation, identifier);

            diagnostics_add_error(p.diagnostics, ::syntax(result), "']' expected to close annotation");

            return result;
        }

        syntax_token tkCloseEmpty = parser_current_token(p);
        parser_advance(p); // `]`

        result.sourceSpan = source_span_combine(tkOpenAnnotation.sourceSpan, tkCloseEmpty.sourceSpan);
        result.kind = ::annotation_identifier(tkOpenAnnotation, identifier, tkCloseEmpty);

        return result;
    }
    else
    {
        result.sourceSpan = tkOpenAnnotation.sourceSpan;
        result.kind = ::annotation_only_open(tkOpenAnnotation);

        diagnostics_add_error(p.diagnostics, ::syntax(result), "expected 'if' or an identifier to start annotation contents");

        return result;
    }

    return result;
}

syntax_node *parser_read_laye_namespace_path(parser_data *p, bool isExpression)
{
    syntax_node *result = syntax_node_alloc();
    // TODO(local): defer
    // defer return result;

    syntax_node *[dynamic] identifiers;
    syntax_token[dynamic] separators;

    syntax_token nextToken = parser_current_token(p);
    source_location startLocation = nextToken.sourceSpan.startLocation;

    if (nextToken.kind is ::laye_identifier)
    {
        syntax_node *identNodeFirst = syntax_node_alloc();

        identNodeFirst.sourceSpan = nextToken.sourceSpan;
        identNodeFirst.kind = ::expression_identifier_unresolved(nextToken);

        dynamic_append(identifiers, identNodeFirst);
        parser_advance(p); // laye identifier
    }
    else if (nextToken.kind is ::kw_global)
    {
        syntax_node *globalNodeFirst = syntax_node_alloc();

        globalNodeFirst.sourceSpan = nextToken.sourceSpan;
        globalNodeFirst.kind = ::expression_identifier_global(nextToken);

        dynamic_append(identifiers, globalNodeFirst);
        parser_advance(p); // `global`
    }
    else
    {
        result.kind = ::namespace_path_empty;
        result.sourceSpan = source_span_create(nextToken.sourceSpan.startLocation, nextToken.sourceSpan.startLocation);

        diagnostics_add_error(p.diagnostics, ::token(nextToken), "expected identifier to start namespace path");

        return result;
    }

    source_location endLocation = nextToken.sourceSpan.endLocation;

    while (not parser_is_eof(p))
    {
        nextToken = parser_current_token(p);
        if (nextToken.kind is ::colon)
            diagnostics_add_error(p.diagnostics, ::token(nextToken), "':' is not a valid separator for namespace names, did you mean '::'?");
        // TODO(local): is expression
        else if (nextToken.kind is ::dot)
        {
            // if we're parsing an expression, we want to allow `a::b` AND `a.b` so we can't hard error, just break
            if (not isExpression)
                diagnostics_add_error(p.diagnostics, ::token(nextToken), "'.' is not a valid separator for namespace names, did you mean '::'?");
            else break;
        }
        else if (parser_current_token(p).kind is not ::colon_colon)
            break;

        dynamic_append(separators, nextToken);
        parser_advance(p); // whatever separator we allowed to get here

        endLocation = nextToken.sourceSpan.endLocation;

        nextToken = parser_current_token(p);
        if (nextToken.kind is ::laye_identifier)
        {
            syntax_node *identNode = syntax_node_alloc();

            identNode.sourceSpan = nextToken.sourceSpan;
            identNode.kind = ::expression_identifier_unresolved(nextToken);

            dynamic_append(identifiers, identNode);
            parser_advance(p); // laye identifier
        }
        else if (nextToken.kind is ::kw_global)
        {
            syntax_node *globalNode = syntax_node_alloc();

            globalNode.sourceSpan = nextToken.sourceSpan;
            globalNode.kind = ::expression_identifier_global(nextToken);

            diagnostics_add_error(p.diagnostics, ::token(nextToken), "'global' keyword can only appear at the start of a name path");

            dynamic_append(identifiers, globalNode);
            parser_advance(p); // `global`
        }
        else
        {
            result.kind = ::namespace_path_empty;
            result.sourceSpan = source_span_create(nextToken.sourceSpan.startLocation, nextToken.sourceSpan.startLocation);

            diagnostics_add_error(p.diagnostics, ::token(nextToken), "expected identifier to continue namespace path after '::'");

            return result;
        }

        endLocation = nextToken.sourceSpan.endLocation;
    }

    result.kind = ::namespace_path(identifiers[:identifiers.length], separators[:separators.length]);
    result.sourceSpan = source_span_create(startLocation, endLocation);

    return result;
}

/* Do not call this function directly.
 * 
 * Attempts to read a type from the source text.
 * Returns true if a type was read, even it it had errors, and false otherwise.
 * If false is returned, the lexer will have been reset to the same location it was at when this function was called.
 *
 * It's important to distinguish between the true and false return states.
 * This function will only return true if the type is unquestionably a type (mostly.)
 * For example, `ident` could be either an expression or a type, but it can always be a type so will be parsed as a type.
 * The same can be said for `name[1]` which could be an array with 1 element.
 * These are okay to assume as types since as expressions they generate no side effects.
 * 
 * If any case that could probably be a type has an error it's assumed it's no longer a type and false is returned as long as the type isn't required.
 * If a type IS required then errors are treated as errors and a dummy type is returned to keep the parsing flowing.
 */
bool parser_read_laye_type_node_impl(parser_data *p, syntax_node **result, bool isRequired)
{
    parser_mark entryMark = parser_mark_current_location(p);

    syntax_node *typeNode = syntax_node_alloc();
    *result = typeNode;
    
    syntax_token tkCurrent = parser_current_token(p);
    if (tkCurrent.kind is ::kw_var)
    {
        parser_advance(p); // `var`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_var(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_void)
    {
        parser_advance(p); // `void`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_void(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_bool)
    {
        parser_advance(p); // `bool`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_bool(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_bool_sized sb)
    {
        parser_advance(p); // sized bool

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_bool_sized(tkCurrent, sb.size);
    }
    else if (tkCurrent.kind is ::kw_int)
    {
        parser_advance(p); // `int`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_int(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_int_sized si)
    {
        parser_advance(p); // sized int

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_int_sized(tkCurrent, si.size);
    }
    else if (tkCurrent.kind is ::kw_uint)
    {
        parser_advance(p); // `uint`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_uint(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_uint_sized su)
    {
        parser_advance(p); // sized uint

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_uint_sized(tkCurrent, su.size);
    }
    else if (tkCurrent.kind is ::kw_float)
    {
        parser_advance(p); // `float`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_float(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_float_sized sf)
    {
        parser_advance(p); // sized float

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_float_sized(tkCurrent, sf.size);
    }
    else if (tkCurrent.kind is ::kw_string)
    {
        parser_advance(p); // `string`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_string(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_rune)
    {
        parser_advance(p); // `rune`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_rune(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_rawptr)
    {
        parser_advance(p); // `rawptr`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_rawptr(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_noreturn)
    {
        parser_advance(p); // `noreturn`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_noreturn(tkCurrent);
    }
    else if (tkCurrent.kind is ::laye_identifier)
    {
        syntax_node *path = parser_read_laye_namespace_path(p, false);

        typeNode.sourceSpan = path.sourceSpan;
        typeNode.kind = ::type_named(path);
    }
    else if (tkCurrent.kind is ::kw_global)
    {
        syntax_node *path = parser_read_laye_namespace_path(p, false);

        typeNode.sourceSpan = path.sourceSpan;
        typeNode.kind = ::type_named(path);
    }

    if (typeNode.kind is not nil)
    {
        if (parser_current_token(p).kind is ::question)
        {
            syntax_token tkQuestion = parser_current_token(p);
            parser_advance(p); // `?`

            syntax_node *elementTypeNode = typeNode;
        
            typeNode = syntax_node_alloc();
            *result = typeNode;

            typeNode.sourceSpan = source_span_combine(elementTypeNode.sourceSpan, tkQuestion.sourceSpan);
            typeNode.kind = ::type_nilable(elementTypeNode, tkQuestion);
        }

        if (not parser_read_laye_type_node_suffix_impl(p, result, isRequired))
        {
            if (not isRequired)
            {
                syntax_node_free(typeNode);
                parser_reset_to_mark(p, entryMark);
            }

            return isRequired;
        }

        return true;
    }
    else
    {
        if (isRequired)
        {
            typeNode.sourceSpan = tkCurrent.sourceSpan;
            typeNode.kind = ::type_empty;
        }
        else
        {
            syntax_node_free(typeNode);
            parser_reset_to_mark(p, entryMark);
        }

        return isRequired;
    }
}

bool parser_read_laye_type_node_suffix_impl(parser_data *p, syntax_node **result, bool isRequired)
{
    syntax_node *[] modifiers = parser_read_laye_modifiers(p);

    if (parser_current_token(p).kind is ::question)
    {
        syntax_token tkQuestion = parser_current_token(p);
        parser_advance(p); // `?`

        syntax_node *elementTypeNode = *result;

        syntax_node *nilableTypeNode = syntax_node_alloc();
        *result = nilableTypeNode;
    
        nilableTypeNode.sourceSpan = source_span_combine(elementTypeNode.sourceSpan, tkQuestion.sourceSpan);
        nilableTypeNode.kind = ::type_nilable(elementTypeNode, tkQuestion);

        return parser_read_laye_type_node_suffix_impl(p, result, isRequired);
    }
    else if (parser_current_token(p).kind is ::star)
    {
        syntax_token tkPointer = parser_current_token(p);
        parser_advance(p); // `*`

        syntax_node *elementTypeNode = *result;

        syntax_node *pointerTypeNode = syntax_node_alloc();
        *result = pointerTypeNode;

        pointerTypeNode.sourceSpan = source_span_combine(elementTypeNode.sourceSpan, tkPointer.sourceSpan);
        pointerTypeNode.kind = ::type_pointer(elementTypeNode, modifiers, tkPointer);

        if (parser_current_token(p).kind is ::question)
        {
            syntax_token tkQuestion = parser_current_token(p);
            parser_advance(p); // `?`

            syntax_node *elementTypeNode = pointerTypeNode;
        
            syntax_node *nullableTypeNode = syntax_node_alloc();
            *result = nullableTypeNode;

            nullableTypeNode.sourceSpan = source_span_combine(elementTypeNode.sourceSpan, tkQuestion.sourceSpan);
            nullableTypeNode.kind = ::type_nullable(elementTypeNode, tkQuestion);
        }

        return parser_read_laye_type_node_suffix_impl(p, result, isRequired);
    }
    else if (parser_current_token(p).kind is ::open_bracket)
    {
        syntax_token tkOpenContainer = parser_current_token(p);
        parser_advance(p); // `[`

        syntax_node *elementTypeNode = *result;

        syntax_node *containerTypeNode = syntax_node_alloc();
        *result = containerTypeNode;

        if (parser_current_token(p).kind is ::star)
        {
            syntax_token tkBuffer = parser_current_token(p);
            parser_advance(p); // `*`

            if (parser_current_token(p).kind is not ::close_bracket)
            {
                containerTypeNode.sourceSpan = source_span_combine(elementTypeNode.sourceSpan, tkBuffer.sourceSpan);
                containerTypeNode.kind = ::type_buffer_unfinished(elementTypeNode, modifiers, tkOpenContainer, tkBuffer);

                diagnostics_add_error(p.diagnostics, ::syntax(containerTypeNode), "']' expected to close buffer type");
            }
            else
            {
                syntax_token tkCloseBuffer = parser_current_token(p);
                parser_advance(p); // `]`

                containerTypeNode.sourceSpan = source_span_combine(elementTypeNode.sourceSpan, tkCloseBuffer.sourceSpan);
                containerTypeNode.kind = ::type_buffer(elementTypeNode, modifiers, tkOpenContainer, tkBuffer, tkCloseBuffer);

                if (parser_current_token(p).kind is ::question)
                {
                    syntax_token tkQuestion = parser_current_token(p);
                    parser_advance(p); // `?`

                    syntax_node *elementTypeNode = containerTypeNode;
                
                    syntax_node *nullableTypeNode = syntax_node_alloc();
                    *result = nullableTypeNode;

                    nullableTypeNode.sourceSpan = source_span_combine(elementTypeNode.sourceSpan, tkQuestion.sourceSpan);
                    nullableTypeNode.kind = ::type_nullable(elementTypeNode, tkQuestion);
                }
            }

            return parser_read_laye_type_node_suffix_impl(p, result, isRequired);
        }
        else if (parser_current_token(p).kind is ::close_bracket)
        {
            syntax_token tkCloseSlice = parser_current_token(p);
            parser_advance(p); // `]`

            containerTypeNode.sourceSpan = source_span_combine(elementTypeNode.sourceSpan, tkCloseSlice.sourceSpan);
            containerTypeNode.kind = ::type_slice(elementTypeNode, modifiers, tkOpenContainer, tkCloseSlice);
        }
        else
        {
            diagnostics_add_error(p.diagnostics, ::syntax(containerTypeNode), "'*', ']', 'dynamic', a type or an expression expected to continue container type");
        }

        return parser_read_laye_type_node_suffix_impl(p, result, isRequired);
    }

    if (modifiers.length > 0)
    {
        syntax_node *elementTypeNode = *result;

        syntax_node *danglingTypeNode = syntax_node_alloc();
        danglingTypeNode.sourceSpan = source_span_combine(elementTypeNode.sourceSpan, modifiers[modifiers.length - 1].sourceSpan);
        danglingTypeNode.kind = ::type_dangling_modifiers(elementTypeNode, modifiers);

        *result = danglingTypeNode;

        diagnostics_add_error(p.diagnostics, ::syntax(danglingTypeNode), "expected container type syntax following type modifiers");
    }

    return true;
}

syntax_node *parser_read_laye_type_node(parser_data *p)
{
    syntax_node *typeNode;
    bool hasValue = parser_read_laye_type_node_impl(p, &typeNode, true);
    assert(hasValue, "parser_read_laye_type_node_impl should always return true if `isRequired` is set to true");
    if (typeNode.kind is nil)
        panic("parser_read_laye_type_node_impl should never generate a nil when isRequired is true");
    return typeNode;
}

bool parser_maybe_read_laye_type_node(parser_data *p, syntax_node **result)
{
    syntax_node *typeNode = nullptr;
    bool hasValue = parser_read_laye_type_node_impl(p, &typeNode, false);
    if (hasValue)
    {
        assert(cast(rawptr) typeNode != nullptr, "parser_read_laye_type_node_impl should never generate a nullptr if it returns true");
        if (typeNode.kind is nil)
            panic("parser_read_laye_type_node_impl should never generate a nil node if it returns true");
    }

    *result = typeNode;
    return hasValue;
}

syntax_node *[] parser_read_laye_annotations(parser_data *p)
{
    syntax_node *[dynamic] annotationStorage;
    while (not parser_is_eof(p))
    {
        syntax_token tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::hash_open_bracket)
            dynamic_append(annotationStorage, parser_read_laye_annotation(p));
        else break;
    }

    syntax_node *[] annotations = annotationStorage[:annotationStorage.length];
    return annotations;
}


syntax_node *[] parser_read_laye_modifiers(parser_data *p)
{
    syntax_node *[dynamic] modifierStorage;
    while (not parser_is_eof(p))
    {
        syntax_token tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::kw_const)
        {
            parser_advance(p); // `const`

            syntax_node *constModifier = syntax_node_alloc();
            constModifier.kind = ::modifier_const(tkCurrent);
            constModifier.sourceSpan = tkCurrent.sourceSpan;

            dynamic_append(modifierStorage, constModifier);
        }
        else if (tkCurrent.kind is ::kw_inline)
        {
            parser_advance(p); // `inline`

            syntax_node *inlineModifier = syntax_node_alloc();
            inlineModifier.kind = ::modifier_inline(tkCurrent);
            inlineModifier.sourceSpan = tkCurrent.sourceSpan;

            dynamic_append(modifierStorage, inlineModifier);
        }
        else if (tkCurrent.kind is ::kw_public)
        {
            parser_advance(p); // `public`

            syntax_node *publicModifier = syntax_node_alloc();
            publicModifier.kind = ::modifier_public(tkCurrent);
            publicModifier.sourceSpan = tkCurrent.sourceSpan;

            dynamic_append(modifierStorage, publicModifier);
        }
        else if (tkCurrent.kind is ::kw_private)
        {
            parser_advance(p); // `private`

            syntax_node *privateModifier = syntax_node_alloc();
            privateModifier.kind = ::modifier_private(tkCurrent);
            privateModifier.sourceSpan = tkCurrent.sourceSpan;

            dynamic_append(modifierStorage, privateModifier);
        }
        else if (tkCurrent.kind is ::kw_readonly)
        {
            parser_advance(p); // `readonly`

            syntax_node *readonlyModifier = syntax_node_alloc();
            readonlyModifier.kind = ::modifier_readonly(tkCurrent);
            readonlyModifier.sourceSpan = tkCurrent.sourceSpan;

            dynamic_append(modifierStorage, readonlyModifier);
        }
        else if (tkCurrent.kind is ::kw_writeonly)
        {
            parser_advance(p); // `writeonly`

            syntax_node *writeonlyModifier = syntax_node_alloc();
            writeonlyModifier.kind = ::modifier_writeonly(tkCurrent);
            writeonlyModifier.sourceSpan = tkCurrent.sourceSpan;

            dynamic_append(modifierStorage, writeonlyModifier);
        }
        else if (tkCurrent.kind is ::kw_foreign)
        {
            syntax_token tkForeign = tkCurrent;
            parser_advance(p); // `foreign`

            tkCurrent = parser_current_token(p);
            if (tkCurrent.kind is ::literal_string)
            {
                parser_advance(p); // string

                syntax_node *foreignModifier = syntax_node_alloc();
                foreignModifier.kind = ::modifier_foreign_named(tkForeign, tkCurrent);
                foreignModifier.sourceSpan = source_span_combine(tkForeign.sourceSpan, tkCurrent.sourceSpan);

                dynamic_append(modifierStorage, foreignModifier);
            }
            else
            {
                syntax_node *foreignModifier = syntax_node_alloc();
                foreignModifier.kind = ::modifier_foreign(tkForeign);
                foreignModifier.sourceSpan = tkCurrent.sourceSpan;

                dynamic_append(modifierStorage, foreignModifier);
            }
        }
        else if (tkCurrent.kind is ::kw_callconv)
        {
            syntax_node *callconvModifier = syntax_node_alloc();

            syntax_token tkCallConv = tkCurrent;
            parser_advance(p); // `callconv`

            tkCurrent = parser_current_token(p);
            if (tkCurrent.kind is not ::open_paren)
            {
                callconvModifier = syntax_node_alloc();
                callconvModifier.kind = ::modifier_callconv_keyword_only(tkCallConv);
                callconvModifier.sourceSpan = tkCurrent.sourceSpan;

                dynamic_append(modifierStorage, callconvModifier);
                continue;
            }

            syntax_token tkOpenParen = tkCurrent;
            parser_advance(p); // `(`

            tkCurrent = parser_current_token(p);
            if (tkCurrent.kind is ::close_paren)
            {
                parser_advance(p); // `)`

                callconvModifier.kind = ::modifier_callconv_empty(tkCallConv, tkOpenParen, tkCurrent);
                callconvModifier.sourceSpan = source_span_combine(tkCallConv.sourceSpan, tkCurrent.sourceSpan);

                diagnostics_add_error(p.diagnostics, ::syntax(callconvModifier), "expected calling convention kind in callconv modifier");

                dynamic_append(modifierStorage, callconvModifier);
                continue;
            }

            tkCurrent = parser_current_token(p);
            if (tkCurrent.kind is not ::laye_identifier)
            {
                callconvModifier.kind = ::modifier_callconv_unfinished_noname(tkCallConv, tkOpenParen);
                callconvModifier.sourceSpan = source_span_combine(tkCallConv.sourceSpan, tkOpenParen.sourceSpan);

                diagnostics_add_error(p.diagnostics, ::syntax(callconvModifier), "expected calling convention kind in callconv modifier");

                dynamic_append(modifierStorage, callconvModifier);
                continue;
            }

            syntax_token identifier = tkCurrent;
            parser_advance(p); // identifier

            tkCurrent = parser_current_token(p);
            if (tkCurrent.kind is not ::close_paren)
            {
                parser_advance(p); // `)`

                callconvModifier.kind = ::modifier_callconv_unfinished(tkCallConv, tkOpenParen, identifier);
                callconvModifier.sourceSpan = source_span_combine(tkCallConv.sourceSpan, identifier.sourceSpan);

                diagnostics_add_error(p.diagnostics, ::syntax(callconvModifier), "expected calling convention kind in callconv modifier");

                dynamic_append(modifierStorage, callconvModifier);
                continue;
            }

            parser_advance(p); // `)`

            callconvModifier.kind = ::modifier_callconv_identifier(tkCallConv, tkOpenParen, identifier, tkCurrent);
            callconvModifier.sourceSpan = source_span_combine(tkCallConv.sourceSpan, tkCurrent.sourceSpan);

            dynamic_append(modifierStorage, callconvModifier);
        }
        else break;
    }

    syntax_node *[] modifiers = modifierStorage[:modifierStorage.length];
    return modifiers;
}

syntax_node *parser_read_laye_top_level_declaration(parser_data *p)
{
    syntax_node *[] annotations = parser_read_laye_annotations(p);
    syntax_node *[] modifiers = parser_read_laye_modifiers(p);

    syntax_token tkCurrent = parser_current_token(p);

    // TODO(local): switch
    // TODO(local): switch
    if (tkCurrent.kind is ::kw_namespace)
        return parser_read_laye_namespace(p, annotations);
    else if (tkCurrent.kind is ::kw_using)
        return parser_read_laye_using(p, annotations);
    else if (tkCurrent.kind is ::kw_struct)
    {
    }
    else if (tkCurrent.kind is ::kw_enum)
    {
    }

    syntax_node *statement = parser_read_laye_statement_with_annotations_and_modifiers(p, annotations, modifiers);
    if (statement.kind is ::statement_expression)
        diagnostics_add_error(p.diagnostics, ::syntax(statement), "expected a declaration at the top level, got an expression instead");

    return statement;
}

syntax_node *parser_read_laye_statement(parser_data *p)
{
    source_location location = parser_current_token(p).sourceSpan.startLocation;

    // we currently only allow annotations at top level, so we dummy out an annotation slice
    syntax_node *[dynamic] annotationStorage;
    syntax_node *[] annotations = annotationStorage[:annotationStorage.length];

    // we can still expect modifiers, though
    syntax_node *[] modifiers = parser_read_laye_modifiers(p);

    return parser_read_laye_statement_with_annotations_and_modifiers(p, annotations, modifiers);
}

syntax_node *parser_read_laye_statement_with_annotations_and_modifiers(
    parser_data *p,
    syntax_node *[] annotations,
    syntax_node *[] modifiers)
{
    syntax_token tkCurrent = parser_current_token(p);

    // TODO(local): switch
    // TODO(local): switch
    // TODO(local): switch
    if (tkCurrent.kind is ::semi_colon)
    {
        syntax_node *resultEmpty = syntax_node_alloc();

        resultEmpty.annotations = annotations;
        resultEmpty.modifiers = modifiers;

        syntax_token tkSemiColon = parser_current_token(p);
        parser_advance(p); // `;`

        resultEmpty.sourceSpan = tkSemiColon.sourceSpan;
        resultEmpty.kind = ::statement_empty(tkSemiColon);

        return resultEmpty;
    }
    else if (tkCurrent.kind is ::open_brace)
    {
        syntax_token tkOpenBlock = tkCurrent;
        parser_advance(p); // `{`

        syntax_node *[dynamic] blockNodeStorage;

        while (not parser_is_eof(p) and not parser_check_laye_delimiter(p, ::close_brace))
        {
            syntax_node *stmt = parser_read_laye_statement(p);
            dynamic_append(blockNodeStorage, stmt);
        }

        syntax_node *[] blockNodes = blockNodeStorage[:];
        if (blockNodeStorage.length > 0) dynamic_free(blockNodeStorage);

        syntax_node *resultBlock = syntax_node_alloc();
        
        resultBlock.annotations = annotations;
        resultBlock.modifiers = modifiers;

        if (not parser_check_laye_delimiter(p, ::close_brace))
        {
            if (blockNodes.length > 0)
                resultBlock.sourceSpan = source_span_combine(tkOpenBlock.sourceSpan, blockNodes[blockNodes.length - 1].sourceSpan);
            else resultBlock.sourceSpan = tkOpenBlock.sourceSpan;

            resultBlock.kind = ::statement_block_unfinished(tkOpenBlock, blockNodes);

            diagnostics_add_error(p.diagnostics, ::token(parser_current_token(p)), "'}' expected to end block statement");
        }
        else
        {
            syntax_token tkCloseBlock = parser_current_token(p);
            parser_advance(p); // `}`

            resultBlock.sourceSpan = source_span_combine(tkOpenBlock.sourceSpan, tkCloseBlock.sourceSpan);
            resultBlock.kind = ::statement_block(tkOpenBlock, blockNodes, tkCloseBlock);
        }

        return resultBlock;
    }
    else if (tkCurrent.kind is ::kw_defer)
    {
    }
    else if (tkCurrent.kind is ::kw_if)
    {
        syntax_node *resultIf = syntax_node_alloc();

        syntax_token tkIf = tkCurrent;
        parser_advance(p); // `if`

        syntax_token tkOpenParen;
        syntax_token tkCloseParen;

        tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::open_paren)
        {
            tkOpenParen = tkCurrent;
            parser_advance(p); // `(`
        }
        else
        {
            source_location location = tkCurrent.sourceSpan.startLocation;
            tkOpenParen.sourceSpan = source_span_create(location, location);
            tkOpenParen.kind = ::poison_token;

            diagnostics_add_error(p.diagnostics, ::token(tkCurrent), "'(' expected");
        }

        syntax_node *condition = parser_read_laye_expression(p);

        tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::close_paren)
        {
            tkCloseParen = tkCurrent;
            parser_advance(p); // `)`
        }
        else
        {
            source_location location = tkCurrent.sourceSpan.startLocation;
            tkCloseParen.sourceSpan = source_span_create(location, location);
            tkCloseParen.kind = ::poison_token;

            diagnostics_add_error(p.diagnostics, ::token(tkCurrent), "')' expected");
        }
        
        syntax_node *passBody = parser_read_laye_statement(p);

        resultIf.sourceSpan = source_span_combine(tkIf.sourceSpan, passBody.sourceSpan);
        resultIf.kind = ::statement_if(tkIf, tkOpenParen, condition, tkCloseParen, passBody);

        return resultIf;
    }
    else if (tkCurrent.kind is ::kw_while)
    {
    }
    else if (tkCurrent.kind is ::kw_for)
    {
    }
    else if (tkCurrent.kind is ::kw_switch)
    {
    }
    else if (tkCurrent.kind is ::kw_return)
    {
        syntax_token tkReturn = tkCurrent;
        parser_advance(p); // `return`

        syntax_token tkSemiColon;

        tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::semi_colon)
        {
            tkSemiColon = tkCurrent;
            parser_advance(p); // `;`
        }
        else
        {
            source_location location = tkCurrent.sourceSpan.startLocation;
            tkSemiColon.sourceSpan = source_span_create(location, location);
            tkSemiColon.kind = ::poison_token;

            diagnostics_add_error(p.diagnostics, ::token(tkCurrent), "';' expected");
        }

        syntax_node *resultReturn = syntax_node_alloc();

        resultReturn.sourceSpan = source_span_combine(tkReturn.sourceSpan, tkSemiColon.sourceSpan);
        resultReturn.kind = ::statement_return(tkReturn, tkSemiColon);

        return resultReturn;
    }
    else if (tkCurrent.kind is ::kw_break)
    {
    }
    else if (tkCurrent.kind is ::kw_continue)
    {
    }
    else if (tkCurrent.kind is ::kw_yield)
    {
    }
    else if (tkCurrent.kind is ::kw_unreachable)
    {
    }
    else if (tkCurrent.kind is ::kw_dynamic_append)
    {
    }
    else if (tkCurrent.kind is ::kw_dynamic_free)
    {
    }

    parser_mark entryMark = parser_mark_current_location(p);

    syntax_node *typeNode;
    if (parser_maybe_read_laye_type_node(p, &typeNode))
    {
        tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::laye_identifier)
        {
            parser_advance(p); // identifier
            return parser_read_laye_function_or_binding_declaration(p, annotations, modifiers, typeNode, tkCurrent);
        }
    }

    parser_reset_to_mark(p, entryMark);

    syntax_node *result = syntax_node_alloc();
    result.annotations = annotations;
    result.modifiers = modifiers;

    if (annotations.length > 0 or modifiers.length > 0)
    {
        source_span startSpan;
        source_span endSpan;

        if (annotations.length > 0 and modifiers.length > 0)
        {
            startSpan = source_span_combine(annotations[0].sourceSpan, modifiers[0].sourceSpan);
            endSpan = source_span_combine(annotations[annotations.length - 1].sourceSpan, modifiers[modifiers.length - 1].sourceSpan);
        }
        else if (annotations.length > 0)
        {
            startSpan = annotations[0].sourceSpan;
            endSpan = annotations[annotations.length - 1].sourceSpan;
        }
        else
        {
            startSpan = modifiers[0].sourceSpan;
            endSpan = modifiers[modifiers.length - 1].sourceSpan;
        }

        result.sourceSpan = source_span_combine(startSpan, endSpan);
        result.kind = ::statement_empty_unfinished;
        
        if (annotations.length > 0 and modifiers.length > 0)
            diagnostics_add_error(p.diagnostics, ::syntax(result), "annotations and modifiers can only be applied to declarations");
        else if (annotations.length > 0)
            diagnostics_add_error(p.diagnostics, ::syntax(result), "annotations can only be applied to declarations");
        else diagnostics_add_error(p.diagnostics, ::syntax(result), "modifiers can only be applied to declarations");

        return result;
    }

    syntax_node *expression = parser_read_laye_expression(p);

    // TODO(local): check for things like assignment

    if (parser_current_token(p).kind is ::semi_colon)
    {
        syntax_token tkSemiColon = parser_current_token(p);
        parser_advance(p); // `;`

        result.sourceSpan = source_span_combine(expression.sourceSpan, tkSemiColon.sourceSpan);
        result.kind = ::statement_expression(expression, tkSemiColon);
    }
    else
    {
        result.sourceSpan = expression.sourceSpan;
        result.kind = ::statement_expression_unfinished(expression);

        diagnostics_add_error(p.diagnostics, ::syntax(result), "';' expected to terminate expression statement");
    }

    return result;
}

syntax_node *parser_read_laye_expression(parser_data *p)
{
    if (parser_is_eof(p))
    {
        syntax_node *result = syntax_node_alloc();

        result.sourceSpan = parser_current_token(p).sourceSpan;
        result.kind = ::expression_eof;

        return result;
    }

    syntax_node *expression = parser_read_laye_secondary_expression(p);

    while (not parser_is_eof(p))
    {
        syntax_token tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::kw_and)
        {
            parser_advance(p); // `and`

            syntax_node *logAnd = syntax_node_alloc();
            syntax_node *rhs = parser_read_laye_secondary_expression(p);

            logAnd.sourceSpan = source_span_combine(expression.sourceSpan, rhs.sourceSpan);
            logAnd.kind = ::expression_logical_and(expression, tkCurrent, rhs);

            expression = rhs;
        }
        else if (tkCurrent.kind is ::kw_or)
        {
            parser_advance(p); // `or`

            syntax_node *logAnd = syntax_node_alloc();
            syntax_node *rhs = parser_read_laye_secondary_expression(p);

            logAnd.sourceSpan = source_span_combine(expression.sourceSpan, rhs.sourceSpan);
            logAnd.kind = ::expression_logical_or(expression, tkCurrent, rhs);

            expression = rhs;
        }
        else if (tkCurrent.kind is ::kw_xor)
        {
            parser_advance(p); // `xor`

            syntax_node *logAnd = syntax_node_alloc();
            syntax_node *rhs = parser_read_laye_secondary_expression(p);

            logAnd.sourceSpan = source_span_combine(expression.sourceSpan, rhs.sourceSpan);
            logAnd.kind = ::expression_logical_xor(expression, tkCurrent, rhs);

            expression = rhs;
        }
        else break;
    }

    return expression;
}

syntax_node *parser_read_laye_using(parser_data *p, syntax_node *[] annotations)
{
    source_span startSpan;
    if (annotations.length > 0)
        startSpan = annotations[0].sourceSpan;
    else startSpan = parser_current_token(p).sourceSpan;

    syntax_token nextToken;
    syntax_token tkUsing = parser_current_token(p);

    // TODO(local): is expression
    // TODO(local): assert
    if (tkUsing.kind is not ::kw_using)
        panic("parser_read_laye_using called without `using` keyword");

    syntax_node *result = syntax_node_alloc();
    result.annotations = annotations;
    // TODO(local): defer
    // defer return result;

    parser_advance(p); // `using`

    nextToken = parser_current_token(p);
    if (nextToken.kind is ::semi_colon)
    {
        syntax_token semiColon = nextToken;
        parser_advance(p); // `;`

        result.kind = ::using_namespace_empty(tkUsing, nextToken);
        result.sourceSpan = source_span_combine(startSpan, nextToken.sourceSpan);
        
        diagnostics_add_error(p.diagnostics, ::syntax(result), "using statements must not be empty");

        return result;
    }

    syntax_node *namespacePath = parser_read_laye_namespace_path(p, false);

    nextToken = parser_current_token(p);
    if (nextToken.kind is not ::semi_colon)
    {
        result.kind = ::using_namespace_unfinished(tkUsing, namespacePath);
        result.sourceSpan = source_span_combine(startSpan, namespacePath.sourceSpan);

        diagnostics_add_error(p.diagnostics, ::token(nextToken), "expected ';' to end using statement");
    }
    else
    {
        parser_advance(p); // `;`

        result.kind = ::using_namespace(tkUsing, namespacePath, nextToken);
        result.sourceSpan = source_span_combine(startSpan, nextToken.sourceSpan);
    }

    return result;
}

syntax_node *parser_read_laye_namespace(parser_data *p, syntax_node *[] annotations)
{
    source_span startSpan;
    if (annotations.length > 0)
        startSpan = annotations[0].sourceSpan;
    else startSpan = parser_current_token(p).sourceSpan;

    syntax_token nextToken;
    syntax_token tkNamespace = parser_current_token(p);

    // TODO(local): is expression
    // TODO(local): assert
    if (tkNamespace.kind is not ::kw_namespace)
        panic("parser_read_laye_namespace called without `namespace` keyword");

    syntax_node *result = syntax_node_alloc();
    result.annotations = annotations;
    // TODO(local): defer
    // defer return result;

    parser_advance(p); // `namespace`

    nextToken = parser_current_token(p);
    if (nextToken.kind is ::semi_colon)
    {
        syntax_token semiColon = nextToken;
        parser_advance(p); // `;`

        result.kind = ::using_namespace_empty(tkNamespace, nextToken);
        result.sourceSpan = source_span_combine(startSpan, nextToken.sourceSpan);
        
        diagnostics_add_error(p.diagnostics, ::syntax(result), "using statements must not be empty");

        return result;
    }

    syntax_node *namespacePath = parser_read_laye_namespace_path(p, false);

    nextToken = parser_current_token(p);
    if (nextToken.kind is ::semi_colon)
    {
        parser_advance(p); // `;`

        result.kind = ::namespace_unscoped(tkNamespace, namespacePath, nextToken);
        result.sourceSpan = source_span_combine(startSpan, nextToken.sourceSpan);
    }
    else if (nextToken.kind is ::open_brace)
    {
        syntax_token tkOpenScope = nextToken;
        parser_advance(p); // `{`

        // TODO(local): parse top level namespace contents
        // TODO(local): parse top level namespace contents
        syntax_node *[dynamic] namespaceNodeStorage;

        while (not parser_is_eof(p) and not parser_check_laye_delimiter(p, ::close_brace))
        {
            syntax_node *stmt = parser_read_laye_statement(p);
            dynamic_append(namespaceNodeStorage, stmt);
        }

        syntax_node *[] namespaceNodes = namespaceNodeStorage[:];
        if (namespaceNodeStorage.length > 0) dynamic_free(namespaceNodeStorage);

        if (not parser_check_laye_delimiter(p, ::close_brace))
        {
            if (namespaceNodes.length > 0)
                result.sourceSpan = source_span_combine(tkOpenScope.sourceSpan, namespaceNodes[namespaceNodes.length - 1].sourceSpan);
            else result.sourceSpan = tkOpenScope.sourceSpan;

            result.kind = ::namespace_scoped_unfinished(tkNamespace, namespacePath, tkOpenScope, namespaceNodes);

            diagnostics_add_error(p.diagnostics, ::token(parser_current_token(p)), "'}' expected to end namespace scope");
        }
        else
        {
            syntax_token tkCloseScope = parser_current_token(p);
            parser_advance(p); // `}`

            result.sourceSpan = source_span_combine(tkOpenScope.sourceSpan, tkCloseScope.sourceSpan);
            result.kind = ::namespace_scoped(tkNamespace, namespacePath, tkOpenScope, namespaceNodes, tkCloseScope);
        }
    }
    else
    {
        result.kind = ::namespace_unscoped_unfinished(tkNamespace, namespacePath);
        result.sourceSpan = source_span_combine(startSpan, namespacePath.sourceSpan);

        diagnostics_add_error(p.diagnostics, ::syntax(result), "expected ';' to end namespace statement or '{' to open namespace scope");
    }

    return result;
}

syntax_node *parser_read_laye_function_or_binding_declaration(
    parser_data *p,
    syntax_node *[] annotations,
    syntax_node *[] modifiers,
    syntax_node *typeNode,
    syntax_token name)
{
    if (parser_current_token(p).kind is ::open_paren)
        return parser_read_laye_function_declaration(p, annotations, modifiers, typeNode, name);
    else return parser_read_laye_binding_declaration(p, annotations, modifiers, typeNode, name);
}

syntax_node *parser_read_laye_function_declaration(
    parser_data *p,
    syntax_node *[] annotations,
    syntax_node *[] modifiers,
    syntax_node *typeNode,
    syntax_token tkName)
{
    if (parser_current_token(p).kind is not ::open_paren)
        panic("assertion failed: parser_read_laye_function_declaration called when not at `(`");

    syntax_node *result = syntax_node_alloc();
    result.annotations = annotations;
    result.modifiers = modifiers;

    source_span startSpan = typeNode.sourceSpan;
    get_source_span_from_laye_annotations_and_modifiers(&startSpan, annotations, modifiers);

    syntax_token tkOpenParams = parser_current_token(p);
    parser_advance(p); // `(`

    syntax_token[] paramDelimiters;
    binding_data *[] parameters = parser_read_laye_bindings_delimited(p, ::comma, &paramDelimiters, false, ::close_paren);

    syntax_token tkCloseParams = parser_current_token(p);
    // NOTE(local): parser_read_laye_bindings_delimited does NOT consume the ending delimiter
    if (tkCloseParams.kind is not ::close_paren)
    {
        result.sourceSpan = source_span_combine(startSpan, tkName.sourceSpan);
        result.kind = ::function_declaration_unfinished(typeNode, tkName, tkOpenParams);

        diagnostics_add_error(p.diagnostics, ::syntax(result), "expected ')' to close function paramter list");

        return result;
    }

    parser_advance(p); // `)`

    syntax_node *body;
    if (   parser_check_laye_delimiter(p, ::semi_colon)
        or parser_check_laye_delimiter(p, ::open_brace))
    {
        body = parser_read_laye_statement(p);
    }
    else if (parser_current_token(p).kind is ::equal_greater)
    {
        syntax_token tkArrow = parser_current_token(p);
        parser_advance(p); // `=>`

        body = syntax_node_alloc();
        syntax_node *expression = parser_read_laye_expression(p);

        // TODO(local): check for things like assignment

        if (parser_current_token(p).kind is ::semi_colon)
        {
            syntax_token tkSemiColon = parser_current_token(p);
            parser_advance(p); // `;`

            body.sourceSpan = source_span_combine(expression.sourceSpan, tkSemiColon.sourceSpan);
            body.kind = ::statement_arrow_expression(tkArrow, expression, tkSemiColon);
        }
        else
        {
            body.sourceSpan = expression.sourceSpan;
            body.kind = ::statement_arrow_expression_unfinished(tkArrow, expression);

            diagnostics_add_error(p.diagnostics, ::syntax(body), "';' expected to terminate expression function body");
        }
    }
    else
    {
        body = syntax_node_alloc();
        body.sourceSpan = tkCloseParams.sourceSpan;
        body.kind = ::statement_empty_unfinished;

        diagnostics_add_error(p.diagnostics, ::syntax(body), "function body expected");
    }

    result.sourceSpan = source_span_combine(startSpan, tkName.sourceSpan);
    result.kind = ::function_declaration(typeNode, tkName, tkOpenParams,
        parameters, paramDelimiters, tkCloseParams, body);

    return result;
}

syntax_node *parser_read_laye_binding_declaration(
    parser_data *p,
    syntax_node *[] annotations,
    syntax_node *[] modifiers,
    syntax_node *typeNode,
    syntax_token tkName)
{
    syntax_node *result = syntax_node_alloc();
    result.annotations = annotations;
    result.modifiers = modifiers;

    source_span startSpan = typeNode.sourceSpan;
    get_source_span_from_laye_annotations_and_modifiers(&startSpan, annotations, modifiers);

    binding_data *data = binding_data_alloc();
    data.modifiers = modifiers;
    data.typeNode = typeNode;
    data.name = tkName;

    if (parser_current_token(p).kind is ::semi_colon)
    {
        syntax_token tkSemiColon = parser_current_token(p);
        parser_advance(p); // `;`

        result.sourceSpan = source_span_combine(startSpan, tkSemiColon.sourceSpan);
        result.kind = ::binding_declaration(data, tkSemiColon);

        return result;
    }

    if (parser_current_token(p).kind is ::equal)
    {
        syntax_token tkAssign = parser_current_token(p);
        parser_advance(p); // `=`

        syntax_node *expression = parser_read_laye_expression(p);

        if (parser_current_token(p).kind is ::semi_colon)
        {
            syntax_token tkSemiColon = parser_current_token(p);
            parser_advance(p); // `;`

            result.sourceSpan = source_span_combine(startSpan, tkSemiColon.sourceSpan);
            result.kind = ::binding_declaration_and_assignment(data, tkAssign, expression, tkSemiColon);

            return result;
        }

        result.sourceSpan = source_span_combine(startSpan, tkName.sourceSpan);
        result.kind = ::binding_declaration_and_assignment_unfinished(data, tkAssign, expression);

        return result;
    }

    result.sourceSpan = source_span_combine(startSpan, tkName.sourceSpan);
    result.kind = ::binding_declaration_unfinished(data);

    return result;
}

syntax_node *parser_read_laye_primary_expression(parser_data *p)
{
    syntax_token tkCurrent = parser_current_token(p);
    syntax_node *result = syntax_node_alloc();

    // TODO(local): switch
    // TODO(local): switch
    // TODO(local): switch
    if (tkCurrent.kind is ::laye_identifier)
    {
        syntax_node *path = parser_read_laye_namespace_path(p, true);

        result.sourceSpan = path.sourceSpan;
        result.kind = ::expression_lookup(path);
    }
    else if (tkCurrent.kind is ::kw_global)
    {
        syntax_node *path = parser_read_laye_namespace_path(p, true);

        result.sourceSpan = path.sourceSpan;
        result.kind = ::expression_lookup(path);
    }
    else if (tkCurrent.kind is ::literal_integer)
    {
        parser_advance(p); // integer literal

        result.sourceSpan = tkCurrent.sourceSpan;
        result.kind = ::untyped_literal_integer(tkCurrent);
    }
    else if (tkCurrent.kind is ::literal_float)
    {
        parser_advance(p); // integer float

        result.sourceSpan = tkCurrent.sourceSpan;
        result.kind = ::untyped_literal_float(tkCurrent);
    }
    else if (tkCurrent.kind is ::literal_string)
    {
        parser_advance(p); // string literal

        result.sourceSpan = tkCurrent.sourceSpan;
        result.kind = ::untyped_literal_string(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_true)
    {
        parser_advance(p); // `true`

        result.sourceSpan = tkCurrent.sourceSpan;
        result.kind = ::untyped_literal_bool(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_false)
    {
        parser_advance(p); // `false`

        result.sourceSpan = tkCurrent.sourceSpan;
        result.kind = ::untyped_literal_bool(tkCurrent);
    }
    else if (tkCurrent.kind is ::literal_rune)
    {
        parser_advance(p); // rune literal

        result.sourceSpan = tkCurrent.sourceSpan;
        result.kind = ::untyped_literal_rune(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_nil)
    {
        parser_advance(p); // `nil`

        result.sourceSpan = tkCurrent.sourceSpan;
        result.kind = ::untyped_literal_nil(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_nullptr)
    {
        parser_advance(p); // `nullptr`

        result.sourceSpan = tkCurrent.sourceSpan;
        result.kind = ::untyped_literal_nullptr(tkCurrent);
    }
    else if (tkCurrent.kind is ::kw_context)
    {
        parser_advance(p); // `context`

        result.sourceSpan = tkCurrent.sourceSpan;
        result.kind = ::literal_context(tkCurrent);
    }
    else if (tkCurrent.kind is ::colon_colon)
    {
        syntax_token tkLeadingDelimiter = tkCurrent;
        parser_advance(p); // `::`

        syntax_token name;

        tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::laye_identifier)
        {
            name = tkCurrent;
            parser_advance(p); // laye identifier
        }
        else
        {
            source_location location = tkCurrent.sourceSpan.startLocation;
            name.sourceSpan = source_span_create(location, location);
            name.kind = ::poison_token;

            diagnostics_add_error(p.diagnostics, ::token(tkCurrent), "identifier expected");
        }

        result.sourceSpan = source_span_combine(tkLeadingDelimiter.sourceSpan, name.sourceSpan);
        result.kind = ::expression_lookup_implicit(tkCurrent, name);
    }
    else if (tkCurrent.kind is ::open_paren)
    {
        syntax_token tkOpenGrouped = tkCurrent;
        parser_advance(p); // `(`

        syntax_node *expression = parser_read_laye_expression(p);

        if (parser_current_token(p).kind is ::close_paren)
        {
            syntax_token tkCloseGrouped = parser_current_token(p);
            parser_advance(p); // `)`

            result.sourceSpan = source_span_combine(tkOpenGrouped.sourceSpan, tkCloseGrouped.sourceSpan);
            result.kind = ::expression_grouped(tkOpenGrouped, expression, tkCloseGrouped);
        }
        else
        {
            result.sourceSpan = source_span_combine(tkOpenGrouped.sourceSpan, expression.sourceSpan);
            result.kind = ::expression_grouped_unfinished(tkOpenGrouped, expression);

            diagnostics_add_error(p.diagnostics, ::syntax(result), "')' expected to end grouped expression");

            return result;
        }
    }
    else if (tkCurrent.kind is ::kw_not)
    {
        syntax_token tkNot = tkCurrent;
        parser_advance(p); // `not`

        syntax_node *target = parser_read_laye_primary_expression(p);

        result.sourceSpan = source_span_combine(tkNot.sourceSpan, target.sourceSpan);
        result.kind = ::expression_logical_not(tkNot, target);

        // we can early return from here since the recursive nature already catches all possible suffixes
        return result;
    }
    else if (tkCurrent.kind is ::minus)
    {
        syntax_token tkMinus = tkCurrent;
        parser_advance(p); // `-`

        syntax_node *target = parser_read_laye_primary_expression(p);

        result.sourceSpan = source_span_combine(tkMinus.sourceSpan, target.sourceSpan);
        result.kind = ::expression_negate(tkMinus, target);

        // we can early return from here since the recursive nature already catches all possible suffixes
        return result;
    }
    else if (tkCurrent.kind is ::tilde)
    {
        syntax_token tkTilde = tkCurrent;
        parser_advance(p); // `~`

        syntax_node *target = parser_read_laye_primary_expression(p);

        result.sourceSpan = source_span_combine(tkTilde.sourceSpan, target.sourceSpan);
        result.kind = ::expression_complement(tkTilde, target);

        // we can early return from here since the recursive nature already catches all possible suffixes
        return result;
    }
    else if (tkCurrent.kind is ::amp)
    {
        syntax_token tkAmp = tkCurrent;
        parser_advance(p); // `&`

        syntax_node *target = parser_read_laye_primary_expression(p);

        result.sourceSpan = source_span_combine(tkAmp.sourceSpan, target.sourceSpan);
        result.kind = ::expression_address_of(tkAmp, target);

        // we can early return from here since the recursive nature already catches all possible suffixes
        return result;
    }
    else if (tkCurrent.kind is ::star)
    {
        syntax_token tkStar = tkCurrent;
        parser_advance(p); // `*`

        syntax_node *target = parser_read_laye_primary_expression(p);

        result.sourceSpan = source_span_combine(tkStar.sourceSpan, target.sourceSpan);
        result.kind = ::expression_dereference(tkStar, target);

        // we can early return from here since the recursive nature already catches all possible suffixes
        return result;
    }
    else if (tkCurrent.kind is ::kw_cast)
    {
        syntax_token tkCast = tkCurrent;
        parser_advance(p); // `cast`

        if (parser_current_token(p).kind is not ::open_paren)
        {
            syntax_node *target = parser_read_laye_primary_expression(p);

            result.sourceSpan = source_span_combine(tkCast.sourceSpan, target.sourceSpan);
            result.kind = ::expression_explicit_cast_missing_type(tkCast, target);

            diagnostics_add_error(p.diagnostics, ::syntax(result), "cast expression is missing destination type");
            diagnostics_add_info(p.diagnostics, ::syntax(result), "expected to be in the form of 'cast(type) value'");
        }
        else
        {
            syntax_token tkOpenType = parser_current_token(p);
            parser_advance(p); // `(`

            syntax_node *typeNode = parser_read_laye_type_node(p);

            syntax_token tkCloseType = parser_current_token(p);
            if (tkCloseType.kind is not ::close_paren)
            {
                syntax_node *target = parser_read_laye_primary_expression(p);

                result.sourceSpan = source_span_combine(tkCast.sourceSpan, target.sourceSpan);
                result.kind = ::expression_explicit_cast_unfinished(tkCast, tkOpenType, typeNode, target);

                diagnostics_add_error(p.diagnostics, ::token(tkCloseType), "')' expected to close type in cast operator");
            }
            else
            {
                parser_advance(p); // `)`

                syntax_node *target = parser_read_laye_primary_expression(p);

                result.sourceSpan = source_span_combine(tkCast.sourceSpan, target.sourceSpan);
                result.kind = ::expression_explicit_cast(tkCast, tkOpenType, typeNode, tkCloseType, target);
            }
        }

        // we can early return from here since the recursive nature already catches all possible suffixes
        return result;
    }
    else
    {
        syntax_token tkUnknown = parser_current_token(p);
        parser_advance(p);

        result.sourceSpan = tkUnknown.sourceSpan;
        result.kind = ::expression_unknown_token(tkUnknown);

        diagnostics_add_error(p.diagnostics, ::syntax(result), "unknown token in expression context");

        // TODO(local): return here? instead of parsing suffix of invalid expression? unsure
    }

    return parser_read_laye_primary_expression_suffix(p, result);
}

syntax_node *parser_read_laye_primary_expression_suffix(parser_data *p, syntax_node *primaryExpression)
{
    syntax_token tkCurrent = parser_current_token(p);

    if (tkCurrent.kind is ::dot)
    {
        syntax_node *result = syntax_node_alloc();

        syntax_token tkDot = tkCurrent;
        parser_advance(p); // `.`

        tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::laye_identifier)
        {
            result.sourceSpan = source_span_combine(primaryExpression.sourceSpan, tkCurrent.sourceSpan);
            result.kind = ::expression_static_named_index(primaryExpression, tkDot, tkCurrent);

            parser_advance(p); // laye identifier
        }
        else
        {
            result.sourceSpan = source_span_combine(primaryExpression.sourceSpan, tkDot.sourceSpan);
            result.kind = ::expression_static_named_index_unfinished(primaryExpression, tkDot);

            diagnostics_add_error(p.diagnostics, ::syntax(result), "identifier expected for field index");
        }

        return parser_read_laye_primary_expression_suffix(p, result);
    }
    else if (tkCurrent.kind is ::open_paren)
    {
        syntax_token tkOpenParen = tkCurrent;
        parser_advance(p); // `(`

        syntax_node *result = syntax_node_alloc();

        syntax_node *[dynamic] argumentStorage;
        syntax_token[dynamic] delimiterStorage;

        if (not parser_check_laye_delimiter(p, ::close_paren))
        {
            while (not parser_is_eof(p))
            {
                if (parser_current_token(p).kind is ::comma)
                {
                    syntax_node *missingExpression = syntax_node_alloc();

                    source_location missingLocation = parser_current_token(p).sourceSpan.startLocation;

                    missingExpression.sourceSpan = source_span_create(missingLocation, missingLocation);
                    missingExpression.kind = ::expression_missing;

                    dynamic_append(argumentStorage, missingExpression);

                    syntax_token tkComma = parser_current_token(p);
                    dynamic_append(delimiterStorage, tkComma);
                    parser_advance(p); // `,`

                    diagnostics_add_error(p.diagnostics, ::token(tkComma), "argument missing");

                    continue;
                }
                else if (parser_current_token(p).kind is ::close_paren)
                {
                    syntax_node *missingExpression = syntax_node_alloc();

                    source_location missingLocation = parser_current_token(p).sourceSpan.startLocation;

                    missingExpression.sourceSpan = source_span_create(missingLocation, missingLocation);
                    missingExpression.kind = ::expression_missing;

                    dynamic_append(argumentStorage, missingExpression);

                    diagnostics_add_error(p.diagnostics, ::token(parser_current_token(p)), "argument missing");

                    break;
                }

                syntax_node *argument = parser_read_laye_expression(p);
                dynamic_append(argumentStorage, argument);

                if (parser_current_token(p).kind is ::comma)
                {
                    dynamic_append(delimiterStorage, parser_current_token(p));
                    parser_advance(p); // `,`
                }
                else break;
            }
        }

        syntax_node *[] arguments = argumentStorage[:];
        if (argumentStorage.length > 0) dynamic_free(argumentStorage);

        syntax_token[] delimiters = delimiterStorage[:];
        if (delimiterStorage.length > 0) dynamic_free(delimiterStorage);

        if (parser_current_token(p).kind is not ::close_paren)
        {
            if (arguments.length > 0)
                result.sourceSpan = source_span_combine(primaryExpression.sourceSpan, arguments[arguments.length - 1].sourceSpan);
            else result.sourceSpan = source_span_combine(primaryExpression.sourceSpan, tkOpenParen.sourceSpan);

            result.kind = ::expression_invoke_unfinished(primaryExpression, tkOpenParen, arguments, delimiters);

            diagnostics_add_error(p.diagnostics, ::syntax(result), "')' expected to close invocation arguments");

            return result;
        }

        syntax_token tkCloseParen = parser_current_token(p);
        parser_advance(p); // `)`

        result.sourceSpan = source_span_combine(primaryExpression.sourceSpan, tkCloseParen.sourceSpan);
        result.kind = ::expression_invoke(primaryExpression, tkOpenParen, arguments, delimiters, tkCloseParen);

        return parser_read_laye_primary_expression_suffix(p, result);
    }

    return primaryExpression;
}

syntax_node *parser_read_laye_secondary_expression(parser_data *p)
{
    syntax_node *primaryExpression = parser_read_laye_primary_expression(p);
    return parser_read_laye_secondary_expression_infix(p, primaryExpression);
}

syntax_node *parser_read_laye_secondary_expression_infix(parser_data *p, syntax_node *lhs)
{
    syntax_node *secondary = lhs;

    while (not parser_is_eof(p))
    {
        syntax_token tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::equal_equal)
        {
            parser_advance(p); // `==`

            syntax_node *rhs = parser_read_laye_primary_expression(p);
            syntax_node *infix = syntax_node_alloc();

            infix.sourceSpan = source_span_combine(secondary.sourceSpan, rhs.sourceSpan);
            infix.kind = ::expression_compare_equal(secondary, tkCurrent, rhs);

            secondary = infix;
        }
        else break;
    }

    return secondary;
}

bool parser_check_laye_delimiter(parser_data *p, laye_delimiter_kind delimiter)
{
    if (delimiter == laye_delimiter_kind::comma)
    {
        if (parser_current_token(p).kind is ::comma)
            return true;
    }
    else if (delimiter == laye_delimiter_kind::semi_colon)
    {
        if (parser_current_token(p).kind is ::semi_colon)
            return true;
    }
    else if (delimiter == laye_delimiter_kind::close_paren)
    {
        if (parser_current_token(p).kind is ::close_paren)
            return true;
    }
    else if (delimiter == laye_delimiter_kind::open_brace)
    {
        if (parser_current_token(p).kind is ::open_brace)
            return true;
    }
    else if (delimiter == laye_delimiter_kind::close_brace)
    {
        if (parser_current_token(p).kind is ::close_brace)
            return true;
    }

    return false;
}

binding_data *[] parser_read_laye_bindings_delimited(parser_data *p, laye_delimiter_kind delimiter, syntax_token[] *delimiters, bool allowTrailingDelimiter, laye_delimiter_kind closingDelimiter)
{
    binding_data *[dynamic] bindings;
    if (parser_check_laye_delimiter(p, closingDelimiter))
        return bindings[:0];

    syntax_token[dynamic] delimiterBuffer;

    while (not parser_is_eof(p) and not parser_check_laye_delimiter(p, closingDelimiter))
    {
        binding_data *binding = binding_data_alloc();
        dynamic_append(bindings, binding);

        if (parser_check_laye_delimiter(p, delimiter))
        {
            syntax_token tkDelimiter = parser_current_token(p);

            // TODO(local): better errors on delimiters
            // TODO(local): better errors on delimiters
            diagnostics_add_error(p.diagnostics, ::token(tkDelimiter), "expected binding information (either a type or modifiers,) but got a delimiter instead");

            dynamic_append(delimiterBuffer, tkDelimiter);
            parser_advance(p); // delimiter

            continue;
        }

        binding.modifiers = parser_read_laye_modifiers(p);
        binding.typeNode = parser_read_laye_type_node(p);

        if (parser_current_token(p).kind is ::laye_identifier)
        {
            binding.name = parser_current_token(p);
            parser_advance(p); // binding name
        }
        // TODO(local): better errors on delimiters
        // TODO(local): better errors on delimiters
        else diagnostics_add_error(p.diagnostics, ::token(parser_current_token(p)), "expected identifier for binding name");

        if (not parser_check_laye_delimiter(p, delimiter))
            break;

        dynamic_append(delimiterBuffer, parser_current_token(p));
        parser_advance(p); // delimiter

        if (parser_check_laye_delimiter(p, closingDelimiter))
        {
            // TODO(local): better errors on delimiters
            // TODO(local): better errors on delimiters
            if (not allowTrailingDelimiter)
                diagnostics_add_error(p.diagnostics, ::token(parser_current_token(p)), "trailing delimiters are not allowed in this context");
        }
    }

    // TODO(local): better errors on delimiters
    // TODO(local): better errors on delimiters
    //if (not parser_check_laye_delimiter(p, closingDelimiter))
    //    diagnostics_add_error(p.diagnostics, ::token(parser_current_token(p)), "expected delimiter to close bindings");
    //else parser_advance(p); // closing delimiter

    *delimiters = delimiterBuffer[:delimiterBuffer.length];
    return bindings[:bindings.length];
}

void get_source_span_from_laye_annotations_and_modifiers(source_span *result, syntax_node *[] annotations, syntax_node *[] modifiers)
{
    if (annotations.length > 0 and modifiers.length > 0)
        *result = source_span_combine(annotations[0].sourceSpan, modifiers[modifiers.length - 1].sourceSpan);
    else if (annotations.length > 0)
        *result = source_span_combine(annotations[0].sourceSpan, annotations[annotations.length - 1].sourceSpan);
    else if (modifiers.length > 0)
        *result = source_span_combine(modifiers[0].sourceSpan, modifiers[modifiers.length - 1].sourceSpan);
}
