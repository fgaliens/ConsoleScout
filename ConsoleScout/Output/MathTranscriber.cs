using System.Text;

namespace ConsoleScout.Output;

/// <summary>Rewrites the LaTeX models keep falling back to into plain Unicode, so that a formula
/// reads as a formula in a terminal. Only delimited spans are converted and code is copied
/// verbatim: inside it LaTeX is usually the subject of the answer rather than a way of writing
/// maths.</summary>
internal static class MathTranscriber
{
    private const string ScriptSource = "0123456789+-=()ni";
    private const string Superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁼⁽⁾ⁿⁱ";
    private const string SubscriptSource = "0123456789+-=()aeoxhklmnpstijruv";
    private const string Subscripts = "₀₁₂₃₄₅₆₇₈₉₊₋₌₍₎ₐₑₒₓₕₖₗₘₙₚₛₜᵢⱼᵣᵤᵥ";

    public static string Transcribe(string text)
    {
        if (!text.Contains('\\') && !text.Contains("$$", StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var converted = false;
        var i = 0;

        while (i < text.Length)
        {
            if (text[i] == '`')
            {
                var code = EndOfCode(text, i);
                builder.Append(text, i, code - i);
                i = code;
                continue;
            }

            if (TryReadMath(text, i, out var math, out var next))
            {
                AppendMath(builder, math);
                converted = true;
                i = next;
                continue;
            }

            builder.Append(text[i++]);
        }

        return converted ? builder.ToString() : text;
    }

    /// <summary>Spans the backtick run at <paramref name="start"/> and everything up to the run that
    /// closes it, which covers both inline spans and fenced blocks.</summary>
    private static int EndOfCode(string text, int start)
    {
        var ticks = 0;
        while (start + ticks < text.Length && text[start + ticks] == '`')
        {
            ticks++;
        }

        var i = start + ticks;
        while (i < text.Length)
        {
            if (text[i] != '`')
            {
                i++;
                continue;
            }

            var run = 0;
            while (i + run < text.Length && text[i + run] == '`')
            {
                run++;
            }

            if (run >= ticks)
            {
                return i + run;
            }

            i += run;
        }

        return text.Length;
    }

    private static bool TryReadMath(string text, int start, out ReadOnlySpan<char> math, out int next)
    {
        math = default;
        next = start;

        var (open, close) = Delimiters(text, start);
        if (open == 0)
        {
            return false;
        }

        var end = text.IndexOf(close, start + open, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        math = text.AsSpan(start + open, end - start - open);
        next = end + close.Length;
        return true;
    }

    private static (int Open, string Close) Delimiters(string text, int i)
    {
        if (i + 1 >= text.Length)
        {
            return (0, "");
        }

        return (text[i], text[i + 1]) switch
        {
            ('\\', '[') => (2, "\\]"),
            ('\\', '(') => (2, "\\)"),
            ('$', '$') => (2, "$$"),
            _ => (0, ""),
        };
    }

    private static void AppendMath(StringBuilder builder, ReadOnlySpan<char> math)
    {
        var start = builder.Length;
        var i = 0;

        while (i < math.Length)
        {
            switch (math[i])
            {
                case '\\':
                    AppendCommand(builder, math, ref i);
                    break;

                case '^':
                case '_':
                    AppendScript(builder, math, ref i);
                    break;

                case '{':
                case '}':
                case '&':
                    i++;
                    break;

                default:
                    builder.Append(math[i++]);
                    break;
            }
        }

        Tidy(builder, start);
    }

    private static void AppendCommand(StringBuilder builder, ReadOnlySpan<char> math, ref int i)
    {
        i++;

        if (i >= math.Length)
        {
            return;
        }

        if (!char.IsLetter(math[i]))
        {
            AppendEscape(builder, math[i]);
            i++;
            return;
        }

        var start = i;
        while (i < math.Length && char.IsLetter(math[i]))
        {
            i++;
        }

        var name = math[start..i];

        switch (name)
        {
            case "frac" or "dfrac" or "tfrac":
                AppendFraction(builder, math, ref i);
                return;

            case "sqrt":
                AppendRoot(builder, math, ref i);
                return;

            case "text" or "textrm" or "mathrm" or "mathbf" or "mathit" or "operatorname":
                var group = ReadGroup(math, ref i);
                AppendMath(builder, group);
                return;

            case "begin" or "end":
                ReadGroup(math, ref i);
                builder.Append('\n');
                return;

            case "quad" or "qquad":
                builder.Append(' ');
                return;

            case "left" or "right" or "big" or "Big" or "bigl" or "bigr" or "displaystyle" or "limits":
                return;

            default:
                var symbol = Symbol(name) ?? name.ToString();
                builder.Append(symbol);
                return;
        }
    }

    private static void AppendEscape(StringBuilder builder, char escaped)
    {
        switch (escaped)
        {
            case ',' or ';' or ':' or ' ':
                builder.Append(' ');
                break;

            case '!':
                break;

            case '\\':
                builder.Append('\n');
                break;

            default:
                builder.Append(escaped);
                break;
        }
    }

    private static void AppendFraction(StringBuilder builder, ReadOnlySpan<char> math, ref int i)
    {
        var numerator = ReadGroup(math, ref i);
        var denominator = ReadGroup(math, ref i);

        AppendOperand(builder, numerator);
        builder.Append('/');
        AppendOperand(builder, denominator);
    }

    private static void AppendRoot(StringBuilder builder, ReadOnlySpan<char> math, ref int i)
    {
        var degree = ReadDegree(math, ref i);

        switch (degree)
        {
            case null:
                builder.Append('√');
                break;

            case "3":
                builder.Append('∛');
                break;

            case "4":
                builder.Append('∜');
                break;

            default:
                var index = Script(degree, superscript: true) ?? degree;
                builder.Append(index).Append('√');
                break;
        }

        var operand = ReadGroup(math, ref i);
        AppendOperand(builder, operand);
    }

    private static void AppendOperand(StringBuilder builder, ReadOnlySpan<char> operand)
    {
        var bracketed = NeedsBrackets(operand);

        if (bracketed)
        {
            builder.Append('(');
        }

        AppendMath(builder, operand);

        if (bracketed)
        {
            builder.Append(')');
        }
    }

    /// <summary>True when the operand would bind wrong once the fraction bar or the root sign is
    /// gone. Anything already inside brackets binds on its own.</summary>
    private static bool NeedsBrackets(ReadOnlySpan<char> operand)
    {
        var depth = 0;

        foreach (var c in operand.Trim())
        {
            switch (c)
            {
                case '(' or '{' or '[':
                    depth++;
                    break;

                case ')' or '}' or ']':
                    depth--;
                    break;

                case '+' or '-' or ' ' when depth == 0:
                    return true;
            }
        }

        return false;
    }

    private static void AppendScript(StringBuilder builder, ReadOnlySpan<char> math, ref int i)
    {
        var superscript = math[i] == '^';
        i++;

        var content = new StringBuilder();
        var group = ReadGroup(math, ref i);
        AppendMath(content, group);
        var text = content.ToString();

        var mapped = Script(text, superscript);
        if (mapped is not null)
        {
            builder.Append(mapped);
            return;
        }

        builder.Append(superscript ? '^' : '_');

        if (text.Length > 1)
        {
            builder.Append('(').Append(text).Append(')');
        }
        else
        {
            builder.Append(text);
        }
    }

    private static string? Script(string text, bool superscript)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var source = superscript ? ScriptSource : SubscriptSource;
        var target = superscript ? Superscripts : Subscripts;
        var builder = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            var index = source.IndexOf(c);
            if (index < 0)
            {
                return null;
            }

            builder.Append(target[index]);
        }

        return builder.ToString();
    }

    /// <summary>Reads the argument at <paramref name="i"/>: a braced group, or the single token that
    /// stands in for one in x^2 and \sqrt2.</summary>
    private static ReadOnlySpan<char> ReadGroup(ReadOnlySpan<char> math, ref int i)
    {
        while (i < math.Length && math[i] == ' ')
        {
            i++;
        }

        if (i >= math.Length)
        {
            return default;
        }

        if (math[i] != '{')
        {
            var token = i++;

            if (math[token] == '\\')
            {
                while (i < math.Length && char.IsLetter(math[i]))
                {
                    i++;
                }
            }

            return math[token..i];
        }

        var start = i;
        var depth = 0;

        while (i < math.Length)
        {
            if (math[i] == '{')
            {
                depth++;
            }
            else if (math[i] == '}' && --depth == 0)
            {
                i++;
                return math[(start + 1)..(i - 1)];
            }

            i++;
        }

        return math[(start + 1)..i];
    }

    private static string? ReadDegree(ReadOnlySpan<char> math, ref int i)
    {
        if (i >= math.Length || math[i] != '[')
        {
            return null;
        }

        var end = math[i..].IndexOf(']');
        if (end < 0)
        {
            return null;
        }

        var degree = math.Slice(i + 1, end - 1).ToString();
        i += end + 1;
        return degree;
    }

    /// <summary>Collapses the runs of spaces the dropped commands leave behind, within the region
    /// this conversion appended.</summary>
    private static void Tidy(StringBuilder builder, int start)
    {
        var text = builder.ToString(start, builder.Length - start);
        builder.Length = start;

        var pending = false;

        foreach (var c in text)
        {
            if (c == ' ')
            {
                pending = true;
                continue;
            }

            if (pending && builder.Length > start && builder[builder.Length - 1] != '\n')
            {
                builder.Append(' ');
            }

            pending = false;
            builder.Append(c);
        }
    }

    private static string? Symbol(ReadOnlySpan<char> name) => name switch
    {
        "approx" => "≈",
        "neq" or "ne" => "≠",
        "leq" or "le" => "≤",
        "geq" or "ge" => "≥",
        "equiv" => "≡",
        "sim" => "∼",
        "propto" => "∝",
        "times" => "×",
        "div" => "÷",
        "cdot" => "·",
        "pm" => "±",
        "mp" => "∓",
        "ast" => "*",
        "infty" => "∞",
        "sum" => "∑",
        "prod" => "∏",
        "int" => "∫",
        "partial" => "∂",
        "nabla" => "∇",
        "degree" => "°",
        "angle" => "∠",
        "perp" => "⊥",
        "parallel" => "∥",
        "in" => "∈",
        "notin" => "∉",
        "subset" => "⊂",
        "subseteq" => "⊆",
        "supset" => "⊃",
        "cup" => "∪",
        "cap" => "∩",
        "emptyset" or "varnothing" => "∅",
        "forall" => "∀",
        "exists" => "∃",
        "neg" or "lnot" => "¬",
        "land" or "wedge" => "∧",
        "lor" or "vee" => "∨",
        "to" or "rightarrow" => "→",
        "leftarrow" or "gets" => "←",
        "leftrightarrow" => "↔",
        "Rightarrow" or "implies" => "⇒",
        "Leftarrow" => "⇐",
        "Leftrightarrow" or "iff" => "⇔",
        "mapsto" => "↦",
        "ldots" or "dots" => "…",
        "cdots" => "⋯",
        "vdots" => "⋮",
        "prime" => "′",
        "circ" => "∘",
        "bullet" => "•",
        "alpha" => "α",
        "beta" => "β",
        "gamma" => "γ",
        "delta" => "δ",
        "epsilon" or "varepsilon" => "ε",
        "zeta" => "ζ",
        "eta" => "η",
        "theta" => "θ",
        "iota" => "ι",
        "kappa" => "κ",
        "lambda" => "λ",
        "mu" => "μ",
        "nu" => "ν",
        "xi" => "ξ",
        "pi" => "π",
        "rho" => "ρ",
        "sigma" => "σ",
        "tau" => "τ",
        "upsilon" => "υ",
        "phi" or "varphi" => "φ",
        "chi" => "χ",
        "psi" => "ψ",
        "omega" => "ω",
        "Gamma" => "Γ",
        "Delta" => "Δ",
        "Theta" => "Θ",
        "Lambda" => "Λ",
        "Xi" => "Ξ",
        "Pi" => "Π",
        "Sigma" => "Σ",
        "Phi" => "Φ",
        "Psi" => "Ψ",
        "Omega" => "Ω",
        _ => null,
    };
}
