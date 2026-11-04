// SPDX-FileCopyrightText: 2026 HAIRS contributors
// SPDX-License-Identifier: Apache-2.0
//
// HAIRS Code Assurance
// ----------------------
// Relevant units:   62
// Annotated:        62/62
// Exempt:           13
// Human-reviewed:   0/62
// IP risk:          Low
// Security risk:    High
// Criteria:         61/50
// Resource impact:  6/10 max
// Unverified:       62
//
// GENERATED - DO NOT EDIT MANUALLY

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Hairs.Assurance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hairs.Language.CSharp;

/// <summary>
/// Finds the code units of a C# file and fingerprints them the way the component
/// that owns the HAIRS Code Assurance format does.
///
/// This is a deliberate second implementation of somebody else's algorithm, and
/// that is worth stating plainly because a second implementation is usually the
/// wrong answer. The first lives inside the annotated component's own
/// architecture-test assembly, which is a test project in another repository:
/// this editor cannot reference it, and the component's own decision record
/// explains why it will not be extracted into a shared tool until two products
/// need it.
///
/// So the two must agree by construction, and every part of this file exists to
/// make disagreement visible rather than silent. The parse options are part of
/// the algorithm and are copied whole — a file with a conditional region parses
/// differently under the defaults, and its tokens would quietly leave the hash.
/// The fingerprint is <see cref="SyntaxToken.Text"/> joined by one space, which
/// is what keeps every comment, the generated header and the annotation itself
/// out of the value they describe; without that the generator could never reach
/// a fixed point. And the exemption predicate is ported case by case in the same
/// order, because the reason a unit is exempt is reported to a reviewer and a
/// plausible wrong reason is worse than none.
///
/// Three things go further than the owning component, each invisible on its
/// tree and each needed on the others'. None changes a name, a fingerprint or
/// an exemption of a file that has no directive, no line break inside a
/// literal or a parameter type, no top-level statement and no two units of one
/// name — which is every file the owning component covers:
/// <list type="bullet">
/// <item>Line breaks inside a token (a verbatim or raw string) are hashed as
/// LF, so a CRLF checkout of an LF blob fingerprints the same, and no name
/// spans lines.</item>
/// <item>Directives and disabled text inside a unit are hashed with it, so code
/// under a <c>#else</c> cannot change under an approval.</item>
/// <item>A file's top-level statements form one unit, and a name that would
/// repeat within a file is made unique.</item>
/// </list>
///
/// Nothing here decides a review. It reports what is, and the two things a
/// mistake in it can cost are a unit shown under the wrong heading and a file
/// header this editor then declines to recount — never an approval.
///
/// The editor and the command-line review tool share this one implementation.
/// That is why it sits in an assembly whose only package is Roslyn, apart from
/// the language service and its UI dependencies.
/// </summary>
// Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=BB1981
// Hairs-Falsified-If: under the owning component's predicate, a file with no directive, top-level statement, multi-line literal or repeated name yields a unit whose name, exemption answer or fingerprint differs from what the owning component's scanner reports for it
// Hairs-Human:        PENDING
public sealed class CSharpAssuranceScanner : IAssuranceUnitScanner
{
    /// <summary>
    /// The preprocessor symbols the owning component parses under.
    ///
    /// Part of the algorithm rather than a detail of it. Code inside a region
    /// whose symbol is undefined parses as disabled text, which is not a unit
    /// of its own — so a scanner using the defaults would leave shipping code
    /// out of every unit boundary and would agree with nobody.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=75F133
    // Hairs-Falsified-If: a symbol a net10.0 build defines, or one of DEBUG, RELEASE and TRACE, is missing from the list, so code under it scans as disabled text that belongs to no unit
    // Hairs-Human:        PENDING
    private static readonly string[] DefaultSymbols =
    [
        "NET",
        "NETCOREAPP",
        "NETCOREAPP1_0_OR_GREATER",
        "NETCOREAPP1_1_OR_GREATER",
        "NETCOREAPP2_0_OR_GREATER",
        "NETCOREAPP2_1_OR_GREATER",
        "NETCOREAPP2_2_OR_GREATER",
        "NETCOREAPP3_0_OR_GREATER",
        "NETCOREAPP3_1_OR_GREATER",
        "NET5_0_OR_GREATER",
        "NET6_0_OR_GREATER",
        "NET7_0_OR_GREATER",
        "NET8_0_OR_GREATER",
        "NET9_0_OR_GREATER",
        "NET10_0_OR_GREATER",
        "NET10_0",
        "DEBUG",
        "RELEASE",
        "TRACE",
    ];

    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=1; Fingerprint=B30BF9
    // Hairs-Falsified-If: the parameterless scanner or FingerprintOfFile parses under preprocessor symbols other than DefaultSymbols
    // Hairs-Human:        PENDING
    private static readonly CSharpParseOptions DefaultParseOptions = OptionsFor(DefaultSymbols);

    private readonly CSharpParseOptions _parseOptions;
    private readonly AssuranceExemptionPredicate _predicate;
    private readonly AssuranceNamedValues _namedValues;

    /// <summary>A scanner parsing under <see cref="DefaultPreprocessorSymbols"/>, with the owning component's predicate.</summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=B10EBD
    // Hairs-Falsified-If: the parameterless scanner applies the strict predicate instead of the owning component's
    // Hairs-Human:        PENDING
    public CSharpAssuranceScanner()
        : this(null)
    {
    }

    /// <summary>
    /// A scanner parsing under <paramref name="preprocessorSymbols"/>, or under
    /// <see cref="DefaultPreprocessorSymbols"/> when that is null.
    ///
    /// A component that builds with other symbols has to say so, because
    /// code under an undefined symbol is disabled text: it is no unit of its
    /// own and J1 never asks for a block on it.
    /// </summary>
    /// <param name="preprocessorSymbols">The symbols, or null for the owning component's.</param>
    /// <param name="predicate">
    /// Which exemption predicate to apply. The owning component's by default,
    /// because the editor shows that component's files as its tools see them.
    /// </param>
    /// <param name="namedValues">
    /// Whether named values are exempt as <c>NamedValue</c>. Assessed by
    /// default, which is the owning component's answer.
    /// </param>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=99A0BA
    // Hairs-Falsified-If: an empty symbol list is treated like null and parses under the default symbols instead of under none
    // Hairs-Human:        PENDING
    public CSharpAssuranceScanner(
        IEnumerable<string>? preprocessorSymbols,
        AssuranceExemptionPredicate predicate = AssuranceExemptionPredicate.OwningComponent,
        AssuranceNamedValues namedValues = AssuranceNamedValues.Reviewed)
    {
        _parseOptions = preprocessorSymbols is null
            ? DefaultParseOptions
            : OptionsFor(preprocessorSymbols);
        _predicate = predicate;
        _namedValues = namedValues;
    }

    /// <summary>
    /// The symbols the owning component parses under: every symbol a net10.0
    /// build defines, plus DEBUG, RELEASE and TRACE.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=High; Resources=0; Fingerprint=81A57E
    // Hairs-Falsified-If: the list it returns differs from the symbols the default parse options were built from
    // Hairs-Human:        PENDING
    public static IReadOnlyList<string> DefaultPreprocessorSymbols => DefaultSymbols;

    /// <inheritdoc/>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=6F5C93
    // Hairs-Falsified-If: a file of about 20,000 nested parentheses ends the process with a stack overflow while it is parsed instead of returning units or throwing
    // Hairs-Human:        PENDING
    public IReadOnlyList<AssuranceScannedUnit> Scan(string text, string path)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(path);

        SyntaxTree tree = CSharpSyntaxTree.ParseText(text, _parseOptions, path: path);
        return [.. Units(tree, _predicate, _namedValues).Select(static found => found.Unit)];
    }

    /// <summary>Parse options for one set of preprocessor symbols.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=C3F494
    // Hairs-Falsified-If: the options it returns parse under a language version other than Latest, so a file using current syntax loses declarations to error recovery
    // Hairs-Human:        PENDING
    internal static CSharpParseOptions OptionsFor(IEnumerable<string> preprocessorSymbols) =>
        CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithPreprocessorSymbols(preprocessorSymbols);

    /// <summary>
    /// Every code unit under <paramref name="root"/>, in document order. The walk
    /// is pre-order and does not descend into trivia, so a type comes before its
    /// members and a declaration in disabled text is not found.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=FAC37C
    // Hairs-Falsified-If: a member of a nested type, or a declaration inside a file-scoped namespace, is not yielded and so belongs to no unit
    // Hairs-Human:        PENDING
    internal static IEnumerable<MemberDeclarationSyntax> CodeUnits(SyntaxNode root) =>
        root.DescendantNodes().OfType<MemberDeclarationSyntax>().Where(IsCodeUnit);

    /// <summary>
    /// Every unit of one file, in document order, with the syntax it was read
    /// from: the file's top-level statements first when it has any, then each
    /// declaration. Names are unique within the file.
    ///
    /// Both scanners enumerate through here, so the editor and the command-line
    /// tool cannot name, bound or fingerprint a unit differently.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=0A09EC
    // Hairs-Falsified-If: two units of one file are returned under the same name
    // Hairs-Human:        PENDING
    internal static IReadOnlyList<ScannedDeclaration> Units(
        SyntaxTree tree, AssuranceExemptionPredicate predicate, AssuranceNamedValues namedValues)
    {
        SyntaxNode root = tree.GetRoot();
        var found = new List<ScannedDeclaration>();

        if (root is CompilationUnitSyntax compilationUnit &&
            compilationUnit.Members.OfType<GlobalStatementSyntax>().ToList() is { Count: > 0 } statements)
        {
            found.Add(TopLevel(tree, statements));
        }

        foreach (MemberDeclarationSyntax declaration in CodeUnits(root))
            found.Add(new ScannedDeclaration(declaration, Describe(tree, declaration, predicate, namedValues), IsTopLevel: false));

        return Disambiguated(found);
    }

    /// <summary>One unit as <see cref="IAssuranceUnitScanner"/> reports it, under its plain name.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=EA9465
    // Hairs-Falsified-If: a declaration with attributes reports its start line at its first modifier or keyword instead of at its first attribute, so its block would land between the attribute and the declaration
    // Hairs-Human:        PENDING
    private static AssuranceScannedUnit Describe(
        SyntaxTree tree,
        MemberDeclarationSyntax declaration,
        AssuranceExemptionPredicate predicate,
        AssuranceNamedValues namedValues)
    {
        FileLinePositionSpan span = tree.GetLineSpan(declaration.Span);
        AssuranceExemption exemption = ExemptionFor(
            declaration,
            predicate == AssuranceExemptionPredicate.Strict,
            namedValues == AssuranceNamedValues.Watched);

        return new AssuranceScannedUnit(
            NameOf(declaration, detailed: false),
            OneLine(MemberNameOf(declaration, detailed: false) + ParametersOf(declaration)),
            span.StartLinePosition.Line,
            span.EndLinePosition.Line,
            exemption != AssuranceExemption.None,
            exemption.ToString(),
            Fingerprint(declaration))
        {
            Kind = KindOf(declaration),
            DeclarationColumn = span.StartLinePosition.Character,
        };
    }

    /// <summary>
    /// A file's top-level statements, as one relevant unit.
    ///
    /// A statement is not a declaration, so no case of the whitelist names it,
    /// and a program written as top-level statements would otherwise be a
    /// covered file of executable code with nothing in it to review. Together
    /// the statements are the entry point the compiler generates, so they are
    /// one unit: its extent runs from the first statement to the last, its
    /// block goes above the first, and its fingerprint covers all of them,
    /// local functions included, because those are statements too.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=93C0F6
    // Hairs-Falsified-If: editing a local function declared among a file's top-level statements leaves the top-level unit's fingerprint unchanged
    // Hairs-Human:        PENDING
    private static ScannedDeclaration TopLevel(SyntaxTree tree, IReadOnlyList<GlobalStatementSyntax> statements)
    {
        FileLinePositionSpan first = tree.GetLineSpan(statements[0].Span);
        FileLinePositionSpan last = tree.GetLineSpan(statements[^1].Span);

        var unit = new AssuranceScannedUnit(
            AssuranceVocabulary.TopLevelStatements,
            AssuranceVocabulary.TopLevelStatements,
            first.StartLinePosition.Line,
            last.EndLinePosition.Line,
            IsExempt: false,
            nameof(AssuranceExemption.None),
            Hash(StreamOf(statements.SelectMany(static statement => statement.DescendantTokens()), includeFirstLeading: false)))
        {
            Kind = "top-level statements",
            DeclarationColumn = first.StartLinePosition.Character,
        };

        return new ScannedDeclaration(statements[0], unit, IsTopLevel: true);
    }

    /// <summary>
    /// The units with every name made unique within the file, changing only
    /// the names that would otherwise repeat.
    ///
    /// A manifest entry, an insert and a check message all address a unit by
    /// its file and its name, and three shapes give two units one name: a
    /// partial type declared twice in one file, overloaded indexers (the plain
    /// name of each is <c>this[]</c>), and members of <c>Foo</c> and
    /// <c>Foo&lt;T&gt;</c> declared side by side (the plain name drops the
    /// containing type's type parameters). A repeated name first gets the
    /// detail the plain name leaves out — the containing types' type parameters
    /// and an indexer's parameter types — and a name that still repeats gets
    /// <c>#2</c>, <c>#3</c> and so on, in document order, after its first
    /// occurrence. A file where no name repeats keeps the plain names, which
    /// are the owning component's.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=04675B
    // Hairs-Falsified-If: a unit whose plain name is not repeated in its file is renamed, so a manifest entry naming it no longer matches
    // Hairs-Human:        PENDING
    private static IReadOnlyList<ScannedDeclaration> Disambiguated(List<ScannedDeclaration> found)
    {
        HashSet<string> repeated = Repeated(found);
        if (repeated.Count == 0)
            return found;

        for (int index = 0; index < found.Count; index++)
        {
            ScannedDeclaration unit = found[index];
            if (unit.IsTopLevel || !repeated.Contains(unit.Unit.Name))
                continue;

            found[index] = unit with
            {
                Unit = unit.Unit with
                {
                    Name = NameOf(unit.Declaration, detailed: true),
                    DisplayName = OneLine(MemberNameOf(unit.Declaration, detailed: true) + ParametersOf(unit.Declaration)),
                },
            };
        }

        repeated = Repeated(found);
        if (repeated.Count == 0)
            return found;

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < found.Count; index++)
        {
            ScannedDeclaration unit = found[index];
            string name = unit.Unit.Name;
            if (!repeated.Contains(name))
                continue;

            int occurrence = seen.TryGetValue(name, out int before) ? before + 1 : 1;
            seen[name] = occurrence;
            if (occurrence == 1)
                continue;

            string suffix = "#" + occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture);
            found[index] = unit with
            {
                Unit = unit.Unit with { Name = name + suffix, DisplayName = unit.Unit.DisplayName + suffix },
            };
        }

        return found;

        static HashSet<string> Repeated(IEnumerable<ScannedDeclaration> units) =>
            units.GroupBy(static unit => unit.Unit.Name, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The declaration kind in lower-case words. A type declaration Roslyn adds
    /// later (C# 14's extension block is one) is named by its own keyword.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=BD1553
    // Hairs-Falsified-If: a record struct declaration is reported with the kind record
    // Hairs-Human:        PENDING
    private static string KindOf(MemberDeclarationSyntax declaration) => declaration switch
    {
        RecordDeclarationSyntax record =>
            record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record",
        TypeDeclarationSyntax type => type.Keyword.ValueText,
        EnumDeclarationSyntax => "enum",
        DelegateDeclarationSyntax => "delegate",
        MethodDeclarationSyntax => "method",
        ConstructorDeclarationSyntax => "constructor",
        DestructorDeclarationSyntax => "destructor",
        OperatorDeclarationSyntax => "operator",
        ConversionOperatorDeclarationSyntax => "conversion",
        PropertyDeclarationSyntax => "property",
        IndexerDeclarationSyntax => "indexer",
        EventDeclarationSyntax => "event",
        EventFieldDeclarationSyntax => "event field",
        FieldDeclarationSyntax => "field",
        EnumMemberDeclarationSyntax => "enum member",
        _ => "member",
    };

    /// <summary>
    /// The fingerprint of one declaration: SHA-256 over its token texts joined by
    /// a single space, uppercase hex, first six characters.
    ///
    /// <see cref="SyntaxToken.Text"/> and not <c>ToFullString</c>, which is the
    /// whole mechanism. Text is a token's own characters and never the trivia
    /// around it, so every comment is outside the value — the assurance
    /// annotation included. A fingerprint that covered its own annotation could
    /// never be written down, because writing it would change it.
    ///
    /// Nothing inside a token is canonicalized either, with one exception.
    /// <c>1_000</c> and <c>1000</c> are different fingerprints on purpose: how a
    /// literal is spelled is part of the source, and the conservative answer is
    /// the useful one for a question about whether code changed. The exception
    /// is a line break inside a verbatim or raw string, which is hashed as LF:
    /// that one is not spelled by the author but by the checkout, and a CRLF
    /// working tree of an LF blob would otherwise compute a different value
    /// from the same commit.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=F25577
    // Hairs-Falsified-If: changing any token of a declaration, or code under an inactive #if branch inside it, leaves its fingerprint unchanged
    // Hairs-Human:        PENDING
    public static string Fingerprint(SyntaxNode declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        return Hash(TokenStream(declaration));
    }

    /// <summary>
    /// The exact string a fingerprint is taken over.
    ///
    /// Exposed because a reviewer told only that six hex characters moved has
    /// been told nothing they can act on. What changed is answerable from this,
    /// and a pane that can show it is worth more than one that cannot.
    ///
    /// Two kinds of trivia are code and are in it: a preprocessor directive, and
    /// the disabled text a directive leaves behind, tokenized the same way, so
    /// rewrapping it moves nothing and rewriting it moves the value. Without
    /// them the <c>#else</c> branch of a method compiled under the other
    /// configuration could be rewritten under an approval. For a declaration,
    /// the trivia before its first token is not its own (a block or a region
    /// above it) and is left out; for a whole file nothing is.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=F05AC3
    // Hairs-Falsified-If: editing only a comment, whitespace or the annotation block above a declaration changes its token stream
    // Hairs-Human:        PENDING
    public static string TokenStream(SyntaxNode declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        return StreamOf(Tokens(declaration), includeFirstLeading: declaration is CompilationUnitSyntax);
    }

    /// <summary>
    /// A whole file's fingerprint, over the compilation unit.
    ///
    /// The root is not a type declaration, so it takes the whole-node branch —
    /// which includes the end-of-file token. That token's text is empty and its
    /// separator is not, so every file's stream ends with a space. It looks like
    /// a defect and is load-bearing: drop it and no file fingerprint matches.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=2EDB57
    // Hairs-Falsified-If: one file checked out with CRLF line endings and with LF line endings gets two different file fingerprints
    // Hairs-Human:        PENDING
    public static string FingerprintOfFile(string text, string path)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(path);

        return Fingerprint(CSharpSyntaxTree.ParseText(text, DefaultParseOptions, path: path).GetRoot());
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=992CC1
    // Hairs-Falsified-If: an edited declaration found by trying on average about 17 million token-level variants, such as a changed numeric literal, hashes to the same six characters as the original, so the decision recorded against that value still applies
    // Hairs-Human:        PENDING
    private static string Hash(string stream)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(stream));
        return Convert.ToHexString(digest)[..AssuranceVocabulary.FingerprintWidth];
    }

    /// <summary>
    /// The token texts joined by one space, each preceded by the code its
    /// leading trivia hides (directives and disabled text), except the first
    /// token's unless <paramref name="includeFirstLeading"/>.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=76B8D7
    // Hairs-Falsified-If: a directive or disabled text in the leading trivia of a declaration's second or later token is left out of its stream
    // Hairs-Human:        PENDING
    private static string StreamOf(IEnumerable<SyntaxToken> tokens, bool includeFirstLeading)
    {
        var parts = new List<string>();
        bool first = true;

        foreach (SyntaxToken token in tokens)
        {
            if (!first || includeFirstLeading)
                parts.AddRange(HiddenCode(token.LeadingTrivia));

            first = false;
            parts.Add(Normalized(token.Text));
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// The code a stretch of trivia carries: each directive's tokens, and the
    /// tokens of each run of disabled text. Comments, whitespace and a
    /// directive's message text are not code and are not here.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=3C5CFB
    // Hairs-Falsified-If: rewriting code inside an inactive #else branch within a method leaves the method's fingerprint unchanged
    // Hairs-Human:        PENDING
    private static IEnumerable<string> HiddenCode(SyntaxTriviaList trivia)
    {
        foreach (SyntaxTrivia item in trivia)
        {
            IEnumerable<SyntaxToken> tokens;
            if (item.IsKind(SyntaxKind.DisabledTextTrivia))
                tokens = SyntaxFactory.ParseTokens(item.ToFullString());
            else if (item.GetStructure() is DirectiveTriviaSyntax directive)
                tokens = directive.DescendantTokens();
            else
                continue;

            foreach (SyntaxToken token in tokens)
            {
                if (token.Text.Length > 0)
                    yield return Normalized(token.Text);
            }
        }
    }

    /// <summary>A token's text with every line break inside it as LF.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=B4B560
    // Hairs-Falsified-If: a verbatim or raw string that spans lines gets a different fingerprint in a CRLF checkout than in an LF checkout of the same commit
    // Hairs-Human:        PENDING
    private static string Normalized(string text) =>
        text.Contains('\r', StringComparison.Ordinal)
            ? text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            : text;

    /// <summary>
    /// Which tokens a declaration contributes.
    ///
    /// A type declaration contributes only its header, because its members are
    /// units with fingerprints of their own and counting them twice would make
    /// every edit anywhere in a class move the class's own value. An enum is not
    /// a type declaration in this sense and contributes everything: its members
    /// are its content, and the vocabulary is the reviewable thing.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=7FD968
    // Hairs-Falsified-If: adding, removing or renaming a member of an enum leaves the enum's fingerprint unchanged
    // Hairs-Human:        PENDING
    private static IEnumerable<SyntaxToken> Tokens(SyntaxNode declaration) => declaration switch
    {
        TypeDeclarationSyntax type => HeaderTokens(type),
        _ => declaration.DescendantTokens(),
    };

    /// <summary>
    /// Everything before the opening brace. A record declared without a body has
    /// no brace to stop at, so the whole declaration is its header — which is
    /// correct, and is why this takes while rather than searching for a brace it
    /// would then fail to find.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=B6F075
    // Hairs-Falsified-If: changing a type's attributes, base list or type-parameter constraints leaves the type's fingerprint unchanged
    // Hairs-Human:        PENDING
    private static IEnumerable<SyntaxToken> HeaderTokens(TypeDeclarationSyntax type) =>
        type.ChildNodesAndTokens()
            .TakeWhile(static child => !child.IsKind(SyntaxKind.OpenBraceToken))
            .SelectMany(static child => child.IsToken
                ? Enumerable.Repeat(child.AsToken(), 1)
                : child.AsNode()!.DescendantTokens());

    /// <summary>
    /// What counts as a code unit: a whitelist, not a pattern.
    ///
    /// A declaration this does not name is in no unit at all, so it has no
    /// fingerprint and no record of any kind — which is why the list reaches
    /// bodiless members, fields, enum members and type declarations rather than
    /// only the things with executable bodies. A local function is not here and
    /// needs no entry: it is a statement, and its tokens are already inside the
    /// member that declares it. Top-level statements are not here either; they
    /// form the one unit <see cref="TopLevel"/> makes of them.
    ///
    /// A namespace is deliberately absent. It declares nothing, and its leading
    /// trivia is where the generated file header lives.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=0; Fingerprint=33CB75
    // Hairs-Falsified-If: a declaration kind that carries executable code (a method, constructor, operator, conversion, indexer or event with accessors) is answered false and so belongs to no unit
    // Hairs-Human:        PENDING
    private static bool IsCodeUnit(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax or
        ConstructorDeclarationSyntax or
        DestructorDeclarationSyntax or
        OperatorDeclarationSyntax or
        ConversionOperatorDeclarationSyntax or
        PropertyDeclarationSyntax or
        IndexerDeclarationSyntax or
        EventDeclarationSyntax => true,
        BaseFieldDeclarationSyntax => true,
        EnumMemberDeclarationSyntax => true,
        DelegateDeclarationSyntax => true,
        BaseTypeDeclarationSyntax => true,
        _ => false,
    };

    /// <summary>
    /// The eight exemption cases, tried in the order the owning component writes
    /// them, and then this tool's named-value case.
    ///
    /// The order carries no policy — the cases are close to disjoint — but it is
    /// fixed, so the reason reported for a unit is the same one that component
    /// reports. A reviewer told "exempt: field declaring storage" when the other
    /// tool says "compiler-supplied record member" has been given two answers to
    /// one question.
    ///
    /// <paramref name="strict"/> narrows four cases where the owning
    /// component's predicate would exempt code that runs (see
    /// <see cref="AssuranceExemptionPredicate.Strict"/>). It never widens one,
    /// so a unit exempt under it is exempt under the owning component's too.
    ///
    /// <paramref name="watchNamedValues"/> adds the named-value case, and only
    /// that: it is tried after the other eight, so a unit one of them covers
    /// keeps its reason, and without it every answer is what it was.
    ///
    /// The source's own <c>EXEMPT=</c> escape hatch is not here. It lives on the
    /// annotation, which this scanner does not read: the model above it applies
    /// that reason after attaching the block, so there is one place that knows
    /// about annotations and one that knows about syntax.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=15A7B2
    // Hairs-Falsified-If: an expression-bodied method that calls another member of its type with a literal argument, such as Run(true), is reported exempt
    // Hairs-Human:        PENDING
    private static AssuranceExemption ExemptionFor(MemberDeclarationSyntax declaration, bool strict, bool watchNamedValues)
    {
        // Case 6 — inside a marker type. A property of where the member lives
        // rather than of what it says, so it is answered first. Strictly, where
        // a member lives says nothing about what its body does, and a nested
        // type of that name would carry any code at all past review.
        if (!strict &&
            ContainingTypes(declaration).Any(static type =>
                string.Equals(type.Identifier.ValueText, "AssemblyMarker", StringComparison.Ordinal)))
        {
            return AssuranceExemption.InsideAssemblyMarker;
        }

        // Case 8 — one member of an enum. Answered before case 4 because a
        // member of an enum is written by the source, and case 4 is about what
        // the compiler writes: reporting it as compiler-supplied would be a
        // false reason on a true answer.
        if (declaration is EnumMemberDeclarationSyntax)
            return AssuranceExemption.EnumMemberOfADeclaredVocabulary;

        // Case 7 — a field that is not a fixed value. It declares storage, and
        // the members that write it are reviewed. FieldDeclarationSyntax and not
        // the base type: an event field declares a broadcast point rather than
        // storage, and stays relevant. Strictly, an initializer that runs code
        // is code no member reviews, so only an inert one is storage.
        if (declaration is FieldDeclarationSyntax field && !IsFixedValue(field) &&
            (!strict || field.Declaration.Variables.All(static variable => IsInert(variable.Initializer?.Value))))
        {
            return AssuranceExemption.FieldDeclaringStorage;
        }

        // Case 4 — a member of a record or an enum that the compiler writes. A
        // type or a delegate declared inside a record is not one of those, and
        // without the first line a whole nested type header would be exempt.
        if (declaration is not BaseTypeDeclarationSyntax and not DelegateDeclarationSyntax &&
            ContainingTypes(declaration).FirstOrDefault() is RecordDeclarationSyntax or EnumDeclarationSyntax &&
            !SuppliesAnImplementation(declaration, strict))
        {
            return AssuranceExemption.CompilerSuppliedRecordOrEnumMember;
        }

        // Case 1 — an auto-property, or accessors that only return or assign the
        // corresponding member. Strictly, an auto-property's initializer is
        // held to the same rule as a field's.
        if (declaration is BasePropertyDeclarationSyntax property && IsTrivialProperty(property) &&
            (!strict || property is not PropertyDeclarationSyntax { Initializer: { } initializer } || IsInert(initializer.Value)))
        {
            return AssuranceExemption.TrivialPropertyOrAccessor;
        }

        // Case 2 — a constructor that only assigns its parameters.
        if (declaration is ConstructorDeclarationSyntax constructor && AssignsParametersOnly(constructor))
            return AssuranceExemption.ParameterAssigningConstructor;

        // Case 3 — an expression body that is the corresponding member, a
        // forwarding call, a constant, or a throw. Strictly, a throw whose
        // arguments compute something is a computation.
        if (ArrowBody(declaration) is { } arrow &&
            (IsCorrespondingMemberAccess(arrow, SimpleNameOf(declaration)) ||
             IsDelegationToOwnMember(arrow, declaration) ||
             IsConstant(arrow) ||
             (IsThrowNew(arrow) && (!strict || ThrowsInertly(arrow)))))
        {
            return AssuranceExemption.TrivialExpressionBodiedMember;
        }

        // Case 5 — ToString, GetHashCode, Equals or an operator that only hands
        // its question to another member. The qualifier is the case: an Equals
        // that compares fields itself is a decision about equality.
        if (IsOverrideOrOperator(declaration) && OnlyDelegates(declaration))
            return AssuranceExemption.DelegatingOverrideOrOperator;

        // The named-value case, this tool's own, where the component watches
        // named values: a value with a name and nothing else carries no
        // decision to assess, and its fingerprint still moves in the manifest.
        if (watchNamedValues && IsNamedValue(declaration))
            return AssuranceExemption.NamedValue;

        return AssuranceExemption.None;
    }

    /// <summary>
    /// A declaration that is a value with a name and nothing else: a const
    /// field, whose initializer the compiler holds to a constant; an enum
    /// declaration, whose members are already exempt one by one; or a static
    /// readonly <c>Guid</c>, <c>IntPtr</c>, <c>UIntPtr</c>, <c>nint</c> or
    /// <c>nuint</c> whose every declarator is stated by literals alone.
    ///
    /// Syntax, not semantics: the type is matched by its name, bare or under
    /// <c>System</c>. A type of the same name declared elsewhere is still built
    /// from literals here, and its constructor is a unit of its own.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=C5F526
    // Hairs-Falsified-If: a static readonly field of a type other than Guid, IntPtr, UIntPtr, nint or nuint, such as a Regex built from one string literal, is answered a named value and so needs no block
    // Hairs-Human:        PENDING
    private static bool IsNamedValue(MemberDeclarationSyntax declaration) => declaration switch
    {
        EnumDeclarationSyntax => true,
        FieldDeclarationSyntax field when field.Modifiers.Any(SyntaxKind.ConstKeyword) => true,
        FieldDeclarationSyntax field =>
            field.Modifiers.Any(SyntaxKind.StaticKeyword) &&
            field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) &&
            SystemTypeName(field.Declaration.Type) is "Guid" or "IntPtr" or "UIntPtr" or "nint" or "nuint" &&
            field.Declaration.Variables.All(static variable =>
                variable.Initializer is { } initializer && IsStatedByLiterals(initializer.Value)),
        _ => false,
    };

    /// <summary>
    /// An initializer stated by literals alone: a literal, a cast or a unary
    /// minus of one, <c>Guid.Empty</c>, the <c>Zero</c> of a handle type, or a
    /// <c>new(...)</c> or <c>new T(...)</c> with no object initializer whose
    /// arguments are each a literal, or a cast or a unary minus of one. Nothing
    /// that calls, reads another member, builds an array or computes.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=065A01
    // Hairs-Falsified-If: an initializer that calls something, such as Guid.NewGuid() or Guid.Parse of a literal, or passes an array to a constructor, is answered stated by literals
    // Hairs-Human:        PENDING
    private static bool IsStatedByLiterals(ExpressionSyntax value) => Unwrap(value) switch
    {
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.ValueText: "Empty" } } member =>
            member.IsKind(SyntaxKind.SimpleMemberAccessExpression) && SystemTypeName(member.Expression) is "Guid",
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.ValueText: "Zero" } } member =>
            member.IsKind(SyntaxKind.SimpleMemberAccessExpression) &&
            SystemTypeName(member.Expression) is "IntPtr" or "UIntPtr" or "nint" or "nuint",
        BaseObjectCreationExpressionSyntax creation =>
            creation.Initializer is null &&
            (creation.ArgumentList is null ||
             creation.ArgumentList.Arguments.All(static argument => IsLiteral(argument.Expression))),
        var other => IsLiteral(other),
    };

    /// <summary>
    /// A literal, through any number of parentheses, casts and unary minuses.
    /// A loop rather than a recursion, so a long chain of them is answered
    /// rather than ending the process.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=8CB2D6
    // Hairs-Falsified-If: a name or an arithmetic expression over literals, such as (IntPtr)(1 + 2), is answered a literal
    // Hairs-Human:        PENDING
    private static bool IsLiteral(ExpressionSyntax expression)
    {
        ExpressionSyntax current = expression;
        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized.Expression;
                    break;
                case CastExpressionSyntax cast:
                    current = cast.Expression;
                    break;
                case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryMinusExpression):
                    current = unary.Operand;
                    break;
                default:
                    return current is LiteralExpressionSyntax;
            }
        }
    }

    /// <summary>
    /// The name a type or a type used as an expression is written with, less a
    /// leading <c>System.</c> or <c>global::System.</c>: <c>Guid</c> for
    /// <c>Guid</c>, <c>System.Guid</c> and <c>global::System.Guid</c>. Null for
    /// anything that is not a dotted name.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=F5BE9D
    // Hairs-Falsified-If: a type of the same name under another namespace, such as Vendor.Interop.Guid, is returned as Guid
    // Hairs-Human:        PENDING
    private static string? SystemTypeName(ExpressionSyntax type) => DottedName(type) switch
    {
        { } name when name.StartsWith("global::System.", StringComparison.Ordinal) => name["global::System.".Length..],
        { } name when name.StartsWith("System.", StringComparison.Ordinal) => name["System.".Length..],
        var name => name,
    };

    /// <summary>
    /// <c>A.B.C</c> for a qualified name or a member access chain of simple
    /// names, with a leading <c>global::</c> kept; null for anything else. Walks
    /// the chain in a loop, so a long one is answered rather than ending the
    /// process.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=3; Fingerprint=0CF67D
    // Hairs-Falsified-If: a generic name or an invocation inside the chain, such as Factory().Guid, yields a dotted name instead of null
    // Hairs-Human:        PENDING
    private static string? DottedName(ExpressionSyntax expression)
    {
        var segments = new List<string>();
        ExpressionSyntax current = expression;
        while (true)
        {
            switch (current)
            {
                case QualifiedNameSyntax { Right: IdentifierNameSyntax right } qualified:
                    segments.Add(right.Identifier.ValueText);
                    current = qualified.Left;
                    continue;
                case MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } member
                    when member.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                    segments.Add(name.Identifier.ValueText);
                    current = member.Expression;
                    continue;
                case IdentifierNameSyntax identifier:
                    segments.Add(identifier.Identifier.ValueText);
                    break;
                case AliasQualifiedNameSyntax { Alias.Identifier.ValueText: "global" } alias:
                    segments.Add("global::" + alias.Name.Identifier.ValueText);
                    break;
                default:
                    return null;
            }

            segments.Reverse();
            return string.Join(".", segments);
        }
    }

    /// <summary>
    /// A field whose value is the reviewable thing: const, or static readonly,
    /// and stating a value.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=EA2CF4
    // Hairs-Falsified-If: a const or static readonly field with an initializer is answered not fixed and reported exempt as storage
    // Hairs-Human:        PENDING
    private static bool IsFixedValue(FieldDeclarationSyntax field)
    {
        bool isConstant = field.Modifiers.Any(SyntaxKind.ConstKeyword);
        bool isStaticReadOnly = field.Modifiers.Any(SyntaxKind.StaticKeyword) &&
            field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);

        return (isConstant || isStaticReadOnly) &&
            field.Declaration.Variables.Any(static variable => variable.Initializer is not null);
    }

    /// <summary>
    /// An expression that runs no code of its own: absent, a literal, a name or
    /// a dotted chain of names, <c>default</c>, <c>nameof</c>, <c>typeof</c>, a
    /// parameterless <c>new()</c> with no initializer, an array or collection
    /// of inert elements, an interpolated string of inert holes, and unary,
    /// binary, conditional and cast expressions over those.
    ///
    /// A name can read a property whose getter runs code, and an operator can
    /// be user-defined; both of those are units of their own and are reviewed
    /// there. What is not inert is code that belongs to no other unit: a call,
    /// a lambda, an assignment, an object built from arguments.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=224F23
    // Hairs-Falsified-If: under the strict predicate, an instance field whose initializer is a chain of about 20,000 binary operators ends the process with a stack overflow instead of being answered
    // Hairs-Human:        PENDING
    private static bool IsInert(ExpressionSyntax? expression) => expression switch
    {
        null => true,
        ParenthesizedExpressionSyntax parenthesized => IsInert(parenthesized.Expression),
        LiteralExpressionSyntax => true,
        IdentifierNameSyntax or GenericNameSyntax or QualifiedNameSyntax or PredefinedTypeSyntax => true,
        MemberAccessExpressionSyntax member =>
            IsSingleMemberAccess(member) || (IsInert(member.Expression) && member.Name is IdentifierNameSyntax),
        DefaultExpressionSyntax or TypeOfExpressionSyntax or SizeOfExpressionSyntax => true,
        InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } => true,
        PrefixUnaryExpressionSyntax unary => !unary.IsKind(SyntaxKind.PreIncrementExpression) &&
            !unary.IsKind(SyntaxKind.PreDecrementExpression) && IsInert(unary.Operand),
        PostfixUnaryExpressionSyntax suppressed when suppressed.IsKind(SyntaxKind.SuppressNullableWarningExpression) =>
            IsInert(suppressed.Operand),
        BinaryExpressionSyntax binary => IsInert(binary.Left) && IsInert(binary.Right),
        ConditionalExpressionSyntax conditional =>
            IsInert(conditional.Condition) && IsInert(conditional.WhenTrue) && IsInert(conditional.WhenFalse),
        CastExpressionSyntax cast => IsInert(cast.Expression),
        BaseObjectCreationExpressionSyntax creation =>
            creation.ArgumentList is null or { Arguments.Count: 0 } && creation.Initializer is null,
        ArrayCreationExpressionSyntax array =>
            array.Type.RankSpecifiers.SelectMany(static rank => rank.Sizes).All(IsInert) &&
            (array.Initializer is null || array.Initializer.Expressions.All(IsInert)),
        ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer.Expressions.All(IsInert),
        InitializerExpressionSyntax initializer when initializer.IsKind(SyntaxKind.ArrayInitializerExpression) =>
            initializer.Expressions.All(IsInert),
        CollectionExpressionSyntax collection => collection.Elements.All(static element =>
            element is ExpressionElementSyntax { Expression: var value } && IsInert(value)),
        InterpolatedStringExpressionSyntax interpolated => interpolated.Contents.All(static content =>
            content is not InterpolationSyntax hole || IsInert(hole.Expression)),
        OmittedArraySizeExpressionSyntax => true,
        _ => false,
    };

    /// <summary>A <c>throw new X(...)</c> whose arguments and initializer are inert.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=CFDF20
    // Hairs-Falsified-If: a throw expression whose exception is built from a call, such as throw new X(Describe()), is answered inert
    // Hairs-Human:        PENDING
    private static bool ThrowsInertly(ExpressionSyntax expression) =>
        Unwrap(expression) is ThrowExpressionSyntax { Expression: BaseObjectCreationExpressionSyntax creation } &&
        (creation.ArgumentList is null || creation.ArgumentList.Arguments.All(static argument => IsInert(argument.Expression))) &&
        creation.Initializer is null;

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=4CF9C4
    // Hairs-Falsified-If: a property whose accessors return or assign a field whose name does not correspond to the property, such as Count returning _total, is answered trivial
    // Hairs-Human:        PENDING
    private static bool IsTrivialProperty(BasePropertyDeclarationSyntax property)
    {
        string? name = SimpleNameOf(property);

        if (property is PropertyDeclarationSyntax { ExpressionBody: { } arrow })
            return IsCorrespondingMemberAccess(arrow.Expression, name);

        if (property.AccessorList is null)
            return false;

        return property.AccessorList.Accessors.All(accessor =>
        {
            // No body at all is compiler-supplied: an auto-property.
            if (accessor.Body is null && accessor.ExpressionBody is null)
                return true;

            if (accessor.ExpressionBody is { } body)
            {
                return IsCorrespondingMemberAccess(body.Expression, name) ||
                    IsFieldAssignmentFromValue(body.Expression, name);
            }

            return accessor.Body!.Statements is [var only] && only switch
            {
                ReturnStatementSyntax { Expression: { } returned } =>
                    IsCorrespondingMemberAccess(returned, name),
                ExpressionStatementSyntax statement =>
                    IsFieldAssignmentFromValue(statement.Expression, name),
                _ => false,
            };
        });
    }

    /// <summary>
    /// A member access naming the member that corresponds to
    /// <paramref name="name"/>.
    ///
    /// The correspondence is the case rather than a refinement of it. A property
    /// that returns <em>some</em> field is trivial only if it returns
    /// <em>its own</em>; re-pointing a property at a different field is a
    /// decision, and a decision is what this system exists to put in front of a
    /// person.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=3D126E
    // Hairs-Falsified-If: an expression that calls, indexes or applies an operator to the corresponding member, such as _count + 1 or _items[0], is answered a corresponding member access
    // Hairs-Human:        PENDING
    private static bool IsCorrespondingMemberAccess(ExpressionSyntax expression, string? name) =>
        name is not null &&
        IsSingleMemberAccess(expression) &&
        Corresponds(AssignedMemberName(expression), name);

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=F310E9
    // Hairs-Falsified-If: a setter that assigns anything other than the implicit value to the corresponding field, such as _count = value + 1, is answered a plain assignment
    // Hairs-Human:        PENDING
    private static bool IsFieldAssignmentFromValue(ExpressionSyntax expression, string? name) =>
        expression is AssignmentExpressionSyntax assignment &&
        assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
        IsCorrespondingMemberAccess(assignment.Left, name) &&
        assignment.Right is IdentifierNameSyntax { Identifier.ValueText: "value" };

    /// <summary>
    /// The simple name a declaration publishes, or null. An operator, a
    /// conversion, an indexer, a constructor and a destructor have none, so
    /// nothing corresponds to them and every borderline shape on them is
    /// answered relevant.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=0; Fingerprint=0537AB
    // Hairs-Falsified-If: a constructor, operator, indexer or destructor is given a simple name, so a body returning a same-named member is answered trivial
    // Hairs-Human:        PENDING
    private static string? SimpleNameOf(MemberDeclarationSyntax declaration) => declaration switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        EventDeclarationSyntax @event => @event.Identifier.ValueText,
        _ => null,
    };

    /// <summary>
    /// A constructor assigning each parameter, at most once, to the member that
    /// corresponds to it.
    ///
    /// Each parameter at most once, and each to its own member: a body that
    /// permutes two assignments is exactly the change this case would otherwise
    /// stop anyone from checking.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=F5A703
    // Hairs-Falsified-If: a constructor that assigns a parameter to a member whose name does not correspond to it, such as _count = total, is answered parameter-assigning
    // Hairs-Human:        PENDING
    private static bool AssignsParametersOnly(ConstructorDeclarationSyntax constructor)
    {
        // A chained constructor runs code this predicate is not looking at. An
        // argument-free hop runs nothing worth reviewing.
        if (constructor.Initializer is { ArgumentList.Arguments.Count: > 0 })
            return false;

        IReadOnlyList<ExpressionSyntax>? assignments;
        if (constructor.ExpressionBody is { } arrow)
        {
            assignments = [arrow.Expression];
        }
        else if (constructor.Body is null)
        {
            return false;
        }
        else
        {
            assignments = OnlyExpressions(constructor.Body);
        }

        if (assignments is null)
            return false;

        var assigned = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExpressionSyntax expression in assignments)
        {
            if (expression is not AssignmentExpressionSyntax assignment ||
                !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ||
                !IsSingleMemberAccess(assignment.Left) ||
                assignment.Right is not IdentifierNameSyntax right)
            {
                return false;
            }

            ParameterSyntax? parameter = constructor.ParameterList.Parameters.FirstOrDefault(parameter =>
                string.Equals(
                    parameter.Identifier.ValueText, right.Identifier.ValueText, StringComparison.Ordinal));

            if (parameter is null ||
                !Corresponds(AssignedMemberName(assignment.Left), parameter.Identifier.ValueText) ||
                !assigned.Add(parameter.Identifier.ValueText))
            {
                return false;
            }
        }

        return true;

        static IReadOnlyList<ExpressionSyntax>? OnlyExpressions(BlockSyntax body)
        {
            var expressions = new List<ExpressionSyntax>();
            foreach (StatementSyntax statement in body.Statements)
            {
                if (statement is not ExpressionStatementSyntax expression)
                    return null;

                expressions.Add(expression.Expression);
            }

            return expressions;
        }
    }

    /// <summary>The last identifier of <c>X</c>, <c>this.X</c> or <c>A.B.X</c>.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=40A850
    // Hairs-Falsified-If: for x, this.x or A.B.x it yields something other than the last identifier x
    // Hairs-Human:        PENDING
    private static string AssignedMemberName(ExpressionSyntax left) => Unwrap(left) switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>
    /// Whether a member name and a parameter name are the same name under the
    /// convention: a leading underscore is decoration and the leading capital is
    /// a casing rule. Anything the convention does not cover is answered
    /// relevant.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=2203E1
    // Hairs-Falsified-If: a property FooBar returning _foobar is answered corresponding although only a leading underscore and the first letter's case are the convention, so a property re-pointed at a field that differs only in inner letter case stays exempt
    // Hairs-Human:        PENDING
    private static bool Corresponds(string member, string parameter) =>
        member.Length > 0 &&
        parameter.Length > 0 &&
        string.Equals(
            member.TrimStart('_'),
            parameter.TrimStart('_'),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>A name, a <c>this</c>, or a dotted chain of them. No call, no operator, no argument.</summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=7FC203
    // Hairs-Falsified-If: an expression-bodied property returning a member access chain of about 20,000 segments ends the process with a stack overflow instead of being answered
    // Hairs-Human:        PENDING
    private static bool IsSingleMemberAccess(ExpressionSyntax expression) => Unwrap(expression) switch
    {
        IdentifierNameSyntax => true,
        ThisExpressionSyntax => true,
        MemberAccessExpressionSyntax member =>
            IsSingleMemberAccess(member.Expression) && member.Name is IdentifierNameSyntax,
        _ => false,
    };

    /// <summary>
    /// A call to another member of the same type, forwarding only arguments the
    /// caller chose.
    ///
    /// The argument rule is the case. Anything a member <em>supplies</em> — a
    /// literal, an enum member, a field — is a decision that member is making,
    /// and a delegation that supplies a value is reviewed like any other. Only a
    /// member that hands its own arguments on unchanged decides nothing.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=ABA696
    // Hairs-Falsified-If: an expression body forwarding its parameters to a method imported with using static, such as Kill(string path) calling Delete(path) from System.IO.File, is answered a delegation to its own type and reported exempt
    // Hairs-Human:        PENDING
    private static bool IsDelegationToOwnMember(ExpressionSyntax expression, SyntaxNode declaration)
    {
        if (Unwrap(expression) is not InvocationExpressionSyntax invocation)
            return false;

        if (!invocation.ArgumentList.Arguments.All(argument =>
                IsForwardedParameter(argument.Expression, declaration)))
        {
            return false;
        }

        string? ownType = ContainingTypes(declaration).FirstOrDefault()?.Identifier.ValueText;

        return invocation.Expression switch
        {
            IdentifierNameSyntax or GenericNameSyntax => true,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } => true,
            MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax qualifier } =>
                ownType is not null &&
                string.Equals(qualifier.Identifier.ValueText, ownType, StringComparison.Ordinal),
            _ => false,
        };
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=6BF7D2
    // Hairs-Falsified-If: a literal argument, or an expression over a parameter such as path + suffix, is answered a forwarded parameter
    // Hairs-Human:        PENDING
    private static bool IsForwardedParameter(ExpressionSyntax expression, SyntaxNode declaration)
    {
        ExpressionSyntax unwrapped = Unwrap(expression);

        if (unwrapped is ThisExpressionSyntax)
            return true;

        if (unwrapped is not IdentifierNameSyntax identifier)
            return false;

        SeparatedSyntaxList<ParameterSyntax> parameters = declaration switch
        {
            BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
            IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters,

            // A property or event accessor has one implicit parameter, called value.
            _ => default,
        };

        if (parameters.Count == 0)
        {
            return declaration is BasePropertyDeclarationSyntax &&
                string.Equals(identifier.Identifier.ValueText, "value", StringComparison.Ordinal);
        }

        return parameters.Any(parameter => string.Equals(
            parameter.Identifier.ValueText, identifier.Identifier.ValueText, StringComparison.Ordinal));
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=CAE7B9
    // Hairs-Falsified-If: an expression body returning a field or a computed value, such as _limit * 2, is answered constant
    // Hairs-Human:        PENDING
    private static bool IsConstant(ExpressionSyntax expression) => Unwrap(expression) switch
    {
        LiteralExpressionSyntax => true,
        PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryMinusExpression) ||
            unary.IsKind(SyntaxKind.UnaryPlusExpression) => IsConstant(unary.Operand),
        InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } => true,
        DefaultExpressionSyntax => true,
        _ => false,
    };

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=08E5C9
    // Hairs-Falsified-If: an expression body that throws an existing exception object, such as throw _error, is answered a throw of a new exception
    // Hairs-Human:        PENDING
    private static bool IsThrowNew(ExpressionSyntax expression) =>
        Unwrap(expression) is ThrowExpressionSyntax { Expression: BaseObjectCreationExpressionSyntax };

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=0; Fingerprint=271DCC
    // Hairs-Falsified-If: a method named other than ToString, GetHashCode or Equals, and not an operator or conversion, is answered true
    // Hairs-Human:        PENDING
    private static bool IsOverrideOrOperator(MemberDeclarationSyntax declaration) => declaration switch
    {
        MethodDeclarationSyntax method =>
            method.Identifier.ValueText is "ToString" or "GetHashCode" or "Equals",
        OperatorDeclarationSyntax => true,
        ConversionOperatorDeclarationSyntax => true,
        _ => false,
    };

    /// <summary>
    /// One expression built only from calls, names, literals, type tests and the
    /// two short-circuit operators, containing at least one call.
    ///
    /// A whitelist, because the question is what the expression may <em>not</em>
    /// contain. Logical negation is deliberately absent: <c>!Equals(other)</c> is
    /// not a delegation to Equals, it is the opposite decision, and which way
    /// round it is is the whole content of an inequality operator.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=4206CB
    // Hairs-Falsified-If: a ToString or GetHashCode whose expression body calls an outside API with a literal argument, such as Process.Start of a literal command followed by ToString(), is answered only-delegating and reported exempt
    // Hairs-Human:        PENDING
    private static bool OnlyDelegates(MemberDeclarationSyntax declaration)
    {
        ExpressionSyntax? expression = ArrowBody(declaration) ?? SingleReturnedExpression(declaration);
        if (expression is null)
            return false;

        bool permitted = expression.DescendantNodesAndSelf().All(static node => node switch
        {
            InvocationExpressionSyntax => true,
            MemberAccessExpressionSyntax => true,
            ArgumentListSyntax or ArgumentSyntax => true,
            ParenthesizedExpressionSyntax => true,
            ThisExpressionSyntax or BaseExpressionSyntax => true,
            LiteralExpressionSyntax => true,
            IsPatternExpressionSyntax => true,
            DeclarationPatternSyntax or SingleVariableDesignationSyntax => true,
            BinaryExpressionSyntax binary =>
                binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression),
            TypeSyntax => true,
            TypeArgumentListSyntax => true,
            _ => false,
        });

        return permitted && expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any();
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=0; Fingerprint=19406A
    // Hairs-Falsified-If: a block-bodied member is given an arrow body, so a body of several statements is judged as a single expression
    // Hairs-Human:        PENDING
    private static ExpressionSyntax? ArrowBody(MemberDeclarationSyntax declaration) => declaration switch
    {
        MethodDeclarationSyntax method => method.ExpressionBody?.Expression,
        ConstructorDeclarationSyntax constructor => constructor.ExpressionBody?.Expression,
        DestructorDeclarationSyntax destructor => destructor.ExpressionBody?.Expression,
        OperatorDeclarationSyntax @operator => @operator.ExpressionBody?.Expression,
        ConversionOperatorDeclarationSyntax conversion => conversion.ExpressionBody?.Expression,
        PropertyDeclarationSyntax property => property.ExpressionBody?.Expression,
        IndexerDeclarationSyntax indexer => indexer.ExpressionBody?.Expression,
        _ => null,
    };

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=0; Fingerprint=7B4228
    // Hairs-Falsified-If: a block body with any statement before its return is answered as that return's expression
    // Hairs-Human:        PENDING
    private static ExpressionSyntax? SingleReturnedExpression(MemberDeclarationSyntax declaration) =>
        declaration switch
        {
            MethodDeclarationSyntax { Body.Statements: [ReturnStatementSyntax { Expression: { } only }] } => only,
            OperatorDeclarationSyntax { Body.Statements: [ReturnStatementSyntax { Expression: { } only }] } => only,
            ConversionOperatorDeclarationSyntax
            { Body.Statements: [ReturnStatementSyntax { Expression: { } only }] } => only,
            _ => null,
        };

    /// <summary>
    /// True when the source, rather than the compiler, says what this member
    /// does. Strictly, a property initializer that runs code is also the
    /// source's, as a field's initializer always is.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=6; Fingerprint=3436F1
    // Hairs-Falsified-If: a record property with a block-bodied or expression-bodied accessor is answered compiler-supplied and reported exempt
    // Hairs-Human:        PENDING
    private static bool SuppliesAnImplementation(MemberDeclarationSyntax declaration, bool strict)
    {
        if (ArrowBody(declaration) is not null)
            return true;

        // An initializer is an implementation the source supplies, so a constant
        // inside a record is not one of the members the compiler writes.
        if (declaration is FieldDeclarationSyntax field)
            return field.Declaration.Variables.Any(static variable => variable.Initializer is not null);

        // The compiler writes no event into a record, so an event field inside
        // one is the source's own, initializer or not.
        if (declaration is EventFieldDeclarationSyntax)
            return true;

        if (declaration is BaseMethodDeclarationSyntax { Body: not null })
            return true;

        if (strict && declaration is PropertyDeclarationSyntax { Initializer: { } initializer } && !IsInert(initializer.Value))
            return true;

        return declaration is BasePropertyDeclarationSyntax { AccessorList: { } accessors } &&
            accessors.Accessors.Any(static accessor =>
                accessor.Body is not null || accessor.ExpressionBody is not null);
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=5; Fingerprint=941528
    // Hairs-Falsified-If: an expression other than a parenthesized one, such as a cast or a checked expression, is unwrapped to its operand
    // Hairs-Human:        PENDING
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized
            ? Unwrap(parenthesized.Expression)
            : expression;

    // Hairs-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=4ECA50
    // Hairs-Falsified-If: a declaration's containing types are yielded outermost first, so the record-member case judges a member by its outermost type
    // Hairs-Human:        PENDING
    private static IEnumerable<BaseTypeDeclarationSyntax> ContainingTypes(SyntaxNode node) =>
        node.Ancestors().OfType<BaseTypeDeclarationSyntax>();

    /// <summary>
    /// The qualified, signature-shaped name: namespace, containing types
    /// outermost first, then the member and its parameters.
    ///
    /// The qualifier joins only the parts that exist. A top-level type has no
    /// containing type, and joining an empty one in produces a name with a hole
    /// in it that nothing can be matched against by eye.
    ///
    /// <paramref name="detailed"/> adds what the plain name leaves out and
    /// only a repeated name needs: the containing types' type parameters, and
    /// an indexer's parameter types.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=84E669
    // Hairs-Falsified-If: a member of a nested type is named without one of its enclosing types, or with them innermost first
    // Hairs-Human:        PENDING
    private static string NameOf(MemberDeclarationSyntax declaration, bool detailed)
    {
        string owner = string.Join(
            ".",
            ContainingTypes(declaration).Reverse().Select(type => detailed ? TypeNameOf(type) : type.Identifier.ValueText));

        string? space = declaration.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(static @namespace => @namespace.Name.ToString())
            .FirstOrDefault();

        string qualified = string.Join(
            ".",
            new[] { space, owner }.Where(static part => !string.IsNullOrEmpty(part)));

        return OneLine($"{qualified}.{MemberNameOf(declaration, detailed)}{ParametersOf(declaration)}");
    }

    /// <summary>
    /// A name on one line: every run of whitespace that holds a line break
    /// becomes one space. A parameter type written across lines (a long tuple,
    /// say) is rendered from its source text, line break and indentation
    /// included, which would put the checkout's line ending into the name and
    /// make a CRLF tree and an LF tree name the unit differently. The owning
    /// component has no name that spans lines, so its names are unchanged.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=E87E2F
    // Hairs-Falsified-If: a name whose parameter type spans lines keeps a line break or the indentation that followed it
    // Hairs-Human:        PENDING
    private static string OneLine(string name)
    {
        if (name.AsSpan().IndexOfAny('\r', '\n') < 0)
            return name;

        var line = new StringBuilder(name.Length);
        int index = 0;
        while (index < name.Length)
        {
            if (!char.IsWhiteSpace(name[index]))
            {
                line.Append(name[index++]);
                continue;
            }

            int end = index;
            bool breaks = false;
            while (end < name.Length && char.IsWhiteSpace(name[end]))
                breaks |= name[end++] is '\r' or '\n';

            line.Append(breaks ? " " : name[index..end]);
            index = end;
        }

        return line.ToString();
    }

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=334656
    // Hairs-Falsified-If: a generic containing type is named without its type parameters when the detailed name is asked for
    // Hairs-Human:        PENDING
    private static string TypeNameOf(BaseTypeDeclarationSyntax type) => type is TypeDeclarationSyntax generic
        ? generic.Identifier.ValueText + Generics(generic.TypeParameterList)
        : type.Identifier.ValueText;

    /// <summary>
    /// The member part of the name, without the qualifier.
    ///
    /// An explicit interface implementation carries its specifier, because it is
    /// a different member: two declarations in one type whose names differed only
    /// by the specifier would otherwise be indistinguishable in a report and
    /// unaddressable in a manifest.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=513EA2
    // Hairs-Falsified-If: two members of one type differing only by an explicit interface specifier get the same member name
    // Hairs-Human:        PENDING
    private static string MemberNameOf(MemberDeclarationSyntax declaration, bool detailed) => declaration switch
    {
        MethodDeclarationSyntax method =>
            Explicit(method.ExplicitInterfaceSpecifier) +
            method.Identifier.ValueText + Generics(method.TypeParameterList),
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        DestructorDeclarationSyntax destructor => "~" + destructor.Identifier.ValueText,
        OperatorDeclarationSyntax @operator => "operator " + @operator.OperatorToken.ValueText,
        ConversionOperatorDeclarationSyntax conversion => "operator " + conversion.Type,
        PropertyDeclarationSyntax property =>
            Explicit(property.ExplicitInterfaceSpecifier) + property.Identifier.ValueText,
        IndexerDeclarationSyntax indexer => Explicit(indexer.ExplicitInterfaceSpecifier) +
            (detailed ? "this[" + ParameterTypes(indexer.ParameterList.Parameters) + "]" : "this[]"),
        EventDeclarationSyntax @event =>
            Explicit(@event.ExplicitInterfaceSpecifier) + @event.Identifier.ValueText,
        BaseFieldDeclarationSyntax field => string.Join(
            ", ",
            field.Declaration.Variables.Select(static variable => variable.Identifier.ValueText)),
        EnumMemberDeclarationSyntax entry => entry.Identifier.ValueText,
        DelegateDeclarationSyntax @delegate =>
            @delegate.Identifier.ValueText + Generics(@delegate.TypeParameterList),
        TypeDeclarationSyntax type => type.Identifier.ValueText + Generics(type.TypeParameterList),
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        _ => "<member>",
    };

    /// <summary>
    /// The parameter list, rendered only for a method-shaped declaration.
    ///
    /// Modifiers are part of it because they are part of the signature: two
    /// overloads differing only by <c>out</c> are two units, and a name that
    /// dropped the keyword would make one of them unreachable.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=6A1E59
    // Hairs-Falsified-If: a property, field or type declaration is given a parameter list in its name
    // Hairs-Human:        PENDING
    private static string ParametersOf(MemberDeclarationSyntax declaration) =>
        declaration is BaseMethodDeclarationSyntax method
            ? "(" + ParameterTypes(method.ParameterList.Parameters) + ")"
            : string.Empty;

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=B9F38A
    // Hairs-Falsified-If: a parameter's ref, out, in or params modifier is dropped from its rendered type, so two overloads differing only by it share a name
    // Hairs-Human:        PENDING
    private static string ParameterTypes(SeparatedSyntaxList<ParameterSyntax> parameters) =>
        string.Join(
            ", ",
            parameters.Select(static parameter => string.Concat(
                parameter.Modifiers.Select(static modifier => modifier.ValueText + " ")) +
                (parameter.Type?.ToString() ?? "?")));

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=9A8C64
    // Hairs-Falsified-If: type parameters are rendered with a separator other than a bare comma, so a generic name differs from the owning component's
    // Hairs-Human:        PENDING
    private static string Generics(TypeParameterListSyntax? parameters) => parameters is null
        ? string.Empty
        : "<" + string.Join(",", parameters.Parameters.Select(static p => p.Identifier.ValueText)) + ">";

    // Hairs-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=6CF1F5
    // Hairs-Falsified-If: an explicit interface implementation is named without its interface qualifier and the dot after it
    // Hairs-Human:        PENDING
    private static string Explicit(ExplicitInterfaceSpecifierSyntax? specifier) =>
        specifier is null ? string.Empty : specifier.Name.ToString() + ".";

    /// <summary>
    /// Which of the cases, if any, covers a declaration. The first eight names
    /// are the owning component's, so a reader can hold the two against each
    /// other one line at a time; <c>NamedValue</c> is this tool's, last, and
    /// answered only where a component watches named values.
    /// </summary>
    // Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=59DF31
    // Hairs-Falsified-If: a member name differs from the owning component's exemption reason identifier, so the reason shown for an exempt unit matches neither tool
    // Hairs-Human:        PENDING
    private enum AssuranceExemption
    {
        None = 0,
        TrivialPropertyOrAccessor,
        ParameterAssigningConstructor,
        TrivialExpressionBodiedMember,
        CompilerSuppliedRecordOrEnumMember,
        DelegatingOverrideOrOperator,
        InsideAssemblyMarker,
        FieldDeclaringStorage,
        EnumMemberOfADeclaredVocabulary,
        NamedValue,
    }
}

/// <summary>
/// One unit and the syntax it was read from: its declaration, or for the
/// top-level unit the first statement, whose leading trivia holds its block.
/// </summary>
// Hairs-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=E9431D
// Hairs-Human:        PENDING
internal sealed record ScannedDeclaration(MemberDeclarationSyntax Declaration, AssuranceScannedUnit Unit, bool IsTopLevel);
