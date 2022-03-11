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

        if (topLevelNode.kind is ::expression_statement expressionStatement)
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

    bool lastContextLayeCompileTimeExpression = p.lexer.contextLayeCompileTimeExpression;
    p.lexer.contextLayeCompileTimeExpression = true;
    // TODO(local): defer
    // p.lexer.contextLayeCompileTimeExpression = lastContextLayeCompileTimeExpression;

    syntax_node *result = syntax_node_alloc();
    // TODO(local): defer
    // defer return result;

    parser_advance(p); // `#[`

    if (parser_is_eof(p))
    {
        result.sourceSpan = tkOpenAnnotation.sourceSpan;
        result.kind = ::annotation_only_open(tkOpenAnnotation);

        diagnostics_add_error(p.diagnostics, ::syntax(result), "unexpected end of file in annotation");

        // TODO(local): defer
        p.lexer.contextLayeCompileTimeExpression = lastContextLayeCompileTimeExpression;
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

        // TODO(local): defer
        p.lexer.contextLayeCompileTimeExpression = lastContextLayeCompileTimeExpression;
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

            // TODO(local): defer
            p.lexer.contextLayeCompileTimeExpression = lastContextLayeCompileTimeExpression;
            return result;
        }

        syntax_token tkCloseEmpty = parser_current_token(p);
        parser_advance(p); // `]`

        result.sourceSpan = source_span_combine(tkOpenAnnotation.sourceSpan, tkCloseEmpty.sourceSpan);
        result.kind = ::annotation_identifier(tkOpenAnnotation, identifier, tkCloseEmpty);

        // TODO(local): defer
        p.lexer.contextLayeCompileTimeExpression = lastContextLayeCompileTimeExpression;
        return result;
    }
    else
    {
        result.sourceSpan = tkOpenAnnotation.sourceSpan;
        result.kind = ::annotation_only_open(tkOpenAnnotation);

        diagnostics_add_error(p.diagnostics, ::syntax(result), "expected 'if' or an identifier to start annotation contents");

        // TODO(local): defer
        p.lexer.contextLayeCompileTimeExpression = lastContextLayeCompileTimeExpression;
        return result;
    }

    // TODO(local): defer
    p.lexer.contextLayeCompileTimeExpression = lastContextLayeCompileTimeExpression;
    return result;
}

syntax_node *parser_read_laye_namespace_path(parser_data *p, bool isExpression)
{
    syntax_node *result = syntax_node_alloc();
    // TODO(local): defer
    // defer return result;

    syntax_token[dynamic] identifiers;
    syntax_token[dynamic] separators;

    syntax_token nextToken = parser_current_token(p);
    source_location startLocation = nextToken.sourceSpan.startLocation;

    if (nextToken.kind is not ::laye_identifier)
    {
        result.kind = ::namespace_path_empty;
        result.sourceSpan = source_span_create(nextToken.sourceSpan.startLocation, nextToken.sourceSpan.startLocation);

        diagnostics_add_error(p.diagnostics, ::token(nextToken), "expected identifier to start namespace path");

        return result;
    }

    dynamic_append(identifiers, nextToken);
    parser_advance(p); // laye identifier

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
        if (nextToken.kind is not ::laye_identifier)
        {
            result.kind = ::namespace_path_empty;
            result.sourceSpan = source_span_create(nextToken.sourceSpan.startLocation, nextToken.sourceSpan.startLocation);

            diagnostics_add_error(p.diagnostics, ::token(nextToken), "expected identifier to continue namespace path after '::'");

            return result;
        }

        dynamic_append(identifiers, nextToken);
        parser_advance(p); // laye identifier

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
bool parser_read_type_node_impl(parser_data *p, syntax_node **result, bool isRequired)
{
    uint entryLexerPosition = p.lexer.currentIndex;

    syntax_node *typeNode = syntax_node_alloc();
    *result = typeNode;
    
    syntax_token tkCurrent = parser_current_token(p);
    if (tkCurrent.kind is ::kw_void)
    {
        parser_advance(p); // `void`

        typeNode.sourceSpan = tkCurrent.sourceSpan;
        typeNode.kind = ::type_void(tkCurrent);
    }

    if (typeNode.kind is not nil)
    {
        if (not parser_read_type_node_suffix_impl(p, result, isRequired))
        {
            if (not isRequired)
            {
                syntax_node_free(typeNode);
                p.lexer.currentIndex = entryLexerPosition;
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
            p.lexer.currentIndex = entryLexerPosition;
        }

        return isRequired;
    }
}

bool parser_read_type_node_suffix_impl(parser_data *p, syntax_node **result, bool isRequired)
{
    syntax_node *typeNode = *result;
    return true;
}

syntax_node *parser_read_type_node(parser_data *p)
{
    syntax_node *typeNode;
    bool hasValue = parser_read_type_node_impl(p, &typeNode, true);
    assert(hasValue, "parser_read_type_node_impl should always return true if `isRequired` is set to true");
    if (typeNode.kind is nil)
        panic("parser_read_type_node_impl should never generate a nil when isRequired is true");
    return typeNode;
}

bool parser_maybe_read_type_node(parser_data *p, syntax_node **result)
{
    syntax_node *typeNode = nullptr;
    bool hasValue = parser_read_type_node_impl(p, &typeNode, false);
    if (hasValue)
    {
        assert(cast(rawptr) typeNode != nullptr, "parser_read_type_node_impl should never generate a nullptr if it returns true");
        if (typeNode.kind is nil)
            panic("parser_read_type_node_impl should never generate a nil node if it returns true");
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

    if (tkCurrent.kind is ::kw_namespace)
        return parser_read_laye_namespace(p, annotations);
    else if (tkCurrent.kind is ::kw_using)
        return parser_read_laye_using(p, annotations);
    else
    {
        syntax_node *statement = parser_read_statement_with_annotations_and_modifiers(p, annotations, modifiers);
        if (statement.kind is ::expression_statement)
            diagnostics_add_error(p.diagnostics, ::syntax(statement), "expected a declaration at the top level, got an expression instead");

        return statement;
    }
}

syntax_node *parser_read_statement(parser_data *p)
{
    source_location location = parser_current_token(p).sourceSpan.startLocation;

    // we currently only allow annotations at top level, so we dummy out an annotation slice
    syntax_node *[dynamic] annotationStorage;
    syntax_node *[] annotations = annotationStorage[:annotationStorage.length];

    // we can still expect modifiers, though
    syntax_node *[] modifiers = parser_read_laye_modifiers(p);

    return parser_read_statement_with_annotations_and_modifiers(p, annotations, modifiers);
}

syntax_node *parser_read_statement_with_annotations_and_modifiers(
    parser_data *p,
    syntax_node *[] annotations,
    syntax_node *[] modifiers)
{
    uint entryLexerPosition = p.lexer.currentIndex;

    syntax_node *typeNode;
    if (parser_maybe_read_type_node(p, &typeNode))
    {
        syntax_token tkCurrent = parser_current_token(p);
        if (tkCurrent.kind is ::laye_identifier)
        {
            parser_advance(p); // identifier
            return parser_read_laye_function_or_binding_declaration(p, annotations, modifiers, typeNode, tkCurrent);
        }
    }

    p.lexer.currentIndex = entryLexerPosition;

    syntax_node *result = syntax_node_alloc();

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
        result.kind = ::statement_empty_with_annotations_and_modifiers(annotations, modifiers);
        
        if (annotations.length > 0 and modifiers.length > 0)
            diagnostics_add_error(p.diagnostics, ::syntax(result), "annotations and modifiers can only be applied to declarations");
        else if (annotations.length > 0)
            diagnostics_add_error(p.diagnostics, ::syntax(result), "annotations can only be applied to declarations");
        else diagnostics_add_error(p.diagnostics, ::syntax(result), "modifiers can only be applied to declarations");

        return result;
    }

    syntax_node *expression = parser_read_laye_expression(p);

    result.sourceSpan = expression.sourceSpan;
    result.kind = ::expression_statement(expression);

    return result;
}

syntax_node *parser_read_laye_expression(parser_data *p)
{
    syntax_node *result = syntax_node_alloc();

    syntax_token tkUnknown = parser_current_token(p);
    parser_advance(p);

    result.sourceSpan = tkUnknown.sourceSpan;
    result.kind = ::expression_unknown_token(tkUnknown);

    diagnostics_add_error(p.diagnostics, ::syntax(result), "unknown token in expression context");

    return result;
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
    // TODO(local): defer
    // defer return result;

    parser_advance(p); // `using`

    nextToken = parser_current_token(p);
    if (nextToken.kind is ::semi_colon)
    {
        syntax_token semiColon = nextToken;
        parser_advance(p); // `;`

        result.kind = ::using_namespace_empty(annotations, tkUsing, nextToken);
        result.sourceSpan = source_span_combine(startSpan, nextToken.sourceSpan);
        
        diagnostics_add_error(p.diagnostics, ::syntax(result), "using statements must not be empty");

        return result;
    }

    syntax_node *namespacePath = parser_read_laye_namespace_path(p, false);

    nextToken = parser_current_token(p);
    if (nextToken.kind is not ::semi_colon)
    {
        result.kind = ::using_namespace_unfinished(annotations, tkUsing, namespacePath);
        result.sourceSpan = source_span_combine(startSpan, namespacePath.sourceSpan);

        diagnostics_add_error(p.diagnostics, ::token(nextToken), "expected ';' to end using statement");
    }
    else
    {
        parser_advance(p); // `;`

        result.kind = ::using_namespace(annotations, tkUsing, namespacePath, nextToken);
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
    // TODO(local): defer
    // defer return result;

    parser_advance(p); // `namespace`

    nextToken = parser_current_token(p);
    if (nextToken.kind is ::semi_colon)
    {
        syntax_token semiColon = nextToken;
        parser_advance(p); // `;`

        result.kind = ::using_namespace_empty(annotations, tkNamespace, nextToken);
        result.sourceSpan = source_span_combine(startSpan, nextToken.sourceSpan);
        
        diagnostics_add_error(p.diagnostics, ::syntax(result), "using statements must not be empty");

        return result;
    }

    syntax_node *namespacePath = parser_read_laye_namespace_path(p, false);

    nextToken = parser_current_token(p);
    if (nextToken.kind is ::semi_colon)
    {
        parser_advance(p); // `;`

        result.kind = ::namespace_unscoped(annotations, tkNamespace, namespacePath, nextToken);
        result.sourceSpan = source_span_combine(startSpan, nextToken.sourceSpan);
    }
    else if (nextToken.kind is ::open_brace)
    {
        syntax_token tkOpenScope = nextToken;
        parser_advance(p); // `{`

        // TODO(local): parse top level namespace contents
        // TODO(local): parse top level namespace contents
        // TODO(local): parse top level namespace contents
        // TODO(local): parse top level namespace contents

        nextToken = parser_current_token(p);
        if (nextToken.kind is not ::close_brace)
        {
            result.kind = ::namespace_scoped_unfinished(annotations, tkNamespace, namespacePath, tkOpenScope);
            result.sourceSpan = source_span_combine(startSpan, tkOpenScope.sourceSpan);

            diagnostics_add_error(p.diagnostics, ::token(nextToken), "expected '}' to end namespace scope");
        }
        else
        {
            parser_advance(p); // `}`

            result.kind = ::namespace_scoped(annotations, tkNamespace, namespacePath, tkOpenScope, nextToken);
            result.sourceSpan = source_span_combine(startSpan, nextToken.sourceSpan);
        }
    }
    else
    {
        result.kind = ::namespace_unscoped_unfinished(annotations, tkNamespace, namespacePath);
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
    syntax_node *result = syntax_node_alloc();

    source_span startSpan = typeNode.sourceSpan;
    get_source_span_from_laye_annotations_and_modifiers(&startSpan, annotations, modifiers);

    result.sourceSpan = source_span_combine(startSpan, tkName.sourceSpan);
    result.kind = ::binding_declaration(annotations, typeNode, tkName);

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

    source_span startSpan = typeNode.sourceSpan;
    get_source_span_from_laye_annotations_and_modifiers(&startSpan, annotations, modifiers);

    result.sourceSpan = source_span_combine(startSpan, tkName.sourceSpan);
    result.kind = ::binding_declaration(annotations, typeNode, tkName);

    return result;
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
