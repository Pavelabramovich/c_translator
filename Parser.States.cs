using System.Text;


namespace Trans;


public static partial class Parser
{
    private abstract class State
    {
        protected StringBuilder _currentLexeme;


        public State()
        {
            _currentLexeme = new StringBuilder();
        }

        public State(string lexemePart)
        {
            _currentLexeme = new StringBuilder(lexemePart);
        }

        public abstract void Process(string text, ref int i);
    }


    private class DefaultState : State
    {
        public override void Process(string text, ref int i)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (char.IsNumber(c))
            {
                Parser._state = new NumericLiteralState();
            }
            else if (c == '"' || (i + 1 < text.Length && text[i..(i + 2)] is ['L', '"']))
            {
                Parser._state = new StringLiteralState();
            }
            else if (char.IsLetter(c) || c == '_')
            {
                Parser._state = new IdentifierState();
            }
            else if (c == '.')
            {
                Parser._state = new PointState();
            }
            else if (c == '#')
            {
                Parser._state = new PreprocessorDirectiveState();
            }
            else if ("()[]{};,".Contains(c))
            {
                Parser._tokens.Add(new(0, TokenType.Punctuator, c.ToString()));
                i++;
            }
            else if (c == '\'')
            {
                Parser._state = new CharLiteralState();
            }
            else if (c == '/')
            {
                Parser._state = new SlashState();
            }
            else if ("+-*/%<>~|&^=!?:".Contains(c))
            {
                Parser._state = new OperatorState();
            }
            else
            {
                throw new LexicalException($"Unknown symbol: {c}");
            }
        }
    }


    private class NumericLiteralState : State
    {
        public NumericLiteralState()
            : base()
        { }

        public NumericLiteralState(string lexemePart)
            : base(lexemePart)
        { }

        public override void Process(string text, ref int i)
        {
            char c = text[i];

            if (c == '0')
            {
                if (_currentLexeme.Length == 0 && i + 1 < text.Length)
                {
                    if (char.ToLower(text[i + 1]) == 'b')
                    {
                        i += 2;
                        Parser._state = new BinaryLiteralState();
                    }
                    else if (char.ToLower(text[i + 1]) == 'x')
                    {
                        i += 2;
                        Parser._state = new HexLiteralState();
                    }
                    else
                    {
                        _currentLexeme.Append(c);
                        i++;
                    }
                }
                else
                {
                    _currentLexeme.Append(c);
                    i++;
                }
            }
            else if (char.IsNumber(c))
            {
                _currentLexeme.Append(c);
                i++;
            }
            else if (char.ToLower(c) is '.' or 'e')
            {
                Parser._state = new FloatingPointLiteralState(_currentLexeme.ToString());
            }
            else if (char.ToLower(c) == 'f')
            {
                throw new LexicalException("Can't define floating point literal without point."); 
            }
            else if (char.ToLower(c) is 'u' or 'l')
            {
                Parser._state = new UllLiteralState(_currentLexeme.ToString());
            }
            else if (char.IsLetter(c))
            {
                throw new LexicalException("Invalid numeric literal.");
            }
            else
            {
                if (_currentLexeme.ToString().All(c => c == '0'))
                {
                    Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned: true, isLong: false, "int", 10), "0"));
                }
                else if (_currentLexeme.ToString().ToLower() is ['0', not 'b' and not 'x', ..])
                {
                    foreach (char n in _currentLexeme.ToString())
                    {
                        if (n >= '8')                        
                            throw new LexicalException($"Invalid octal literal with nonoctal symbol: {n}.");
                    }

                    ClearLeadingZeros();
                    _currentLexeme.Insert(0, '0');

                    Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned: true, isLong: false, "int", 8), _currentLexeme.ToString()));
                }
                else
                {
                    ClearLeadingZeros();
                    Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned: true, isLong: false, "int", 10), _currentLexeme.ToString()));
                }

                Parser._state = new DefaultState();
            }
        }


        protected void ClearLeadingZeros()
        {
            while (_currentLexeme.Length != 0 && _currentLexeme[0] == '0')
            {
                _currentLexeme.Remove(0, 1);
            }

            if (_currentLexeme.Length == 0 || !char.IsNumber(_currentLexeme[0]))
                _currentLexeme.Insert(0, '0');
        }
    }

    private class BinaryLiteralState : NumericLiteralState
    {
        public BinaryLiteralState()
            : base()
        { }

        public override void Process(string text, ref int i)
        {
            char c = text[i];

            if (c is '0' or '1')
            {
                _currentLexeme.Append(c);
                i++;
            }
            else if (char.ToLower(c) is 'u' or 'l')
            {
                Parser._state = new UllLiteralState($"0b{_currentLexeme}");
            }
            else if (".Ee".Contains(c))
            {
                throw new LexicalException("Binary literal can't has point.");
            }
            else if (char.IsNumber(c) || char.IsLetter(c) || _currentLexeme.Length == 0)
            {
                throw new LexicalException("Invalid binary literal.");
            }
            else
            {
                ClearLeadingZeros();
                _currentLexeme.Insert(0, "0b");

                Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned: true, isLong: false, "int", 2), _currentLexeme.ToString()));

                i++;
                Parser._state = new DefaultState();
            }
        }
    }

    private class HexLiteralState : NumericLiteralState
    {
        public HexLiteralState()
            : base()
        { }

        public override void Process(string text, ref int i)
        {
            char c = text[i];

            if (char.IsNumber(c) || "ABCDEF".Contains(char.ToUpper(c)))
            {
                _currentLexeme.Append(char.ToLower(c));
                i++;
            }
            else if (char.ToUpper(c) is 'U' or 'L' && _currentLexeme.Length != 0)
            {
                Parser._state = new UllLiteralState($"0x{_currentLexeme}");
            }
            else if (".Ee".Contains(c))
            {
                throw new LexicalException("Hex literal can't be floating point.");
            }
            else if (char.IsLetter(c) || _currentLexeme.Length == 0)
            {
                throw new LexicalException("Invalid hex literal.");
            }
            else
            {
                ClearLeadingZeros();
                _currentLexeme.Insert(0, "0x");

                Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned: true, isLong: false, "int", 16), _currentLexeme.ToString()));
                Parser._state = new DefaultState();
            }
        }
    }

    private class UllLiteralState : NumericLiteralState
    {
        public UllLiteralState(string lexemePart)
            : base(lexemePart)
        { }

        public override void Process(string text, ref int i)
        {
            char c = text[i];

            if (char.ToUpper(c) is 'U' or 'L')
            {
                List<char> suffixList = [];

                for (int j = i; j < text.Length && j < i + 4; j++)
                {
                    suffixList.Add(char.ToLower(text[j]));
                }

                var rest = suffixList switch
                {
                    ['l', 'l', .. var rest1] => rest1,
                    ['l', .. var rest1] => rest1,
                    ['u', 'l', 'l', .. var rest1] => rest1,
                    ['u', 'l', .. var rest1] => rest1,
                    ['u', .. var rest1] => rest1,

                    _ => throw new InvalidOperationException(),
                };

                if (!char.IsLetter(rest[0]) && !char.IsNumber(rest[0]) && rest[0] != '.')
                {
                    if (_currentLexeme is ['0', not 'b' and not 'x' and not 'B' and not 'X'])
                    {
                        foreach (char n in _currentLexeme.ToString())
                        {
                            if (n > '8')
                                throw new LexicalException($"Invalid octal literal with nonoctal symbol: {n}.");
                        }
                    }

                    int suffixLength = suffixList.Count - rest.Count;

                    string suffix = new string(suffixList[..suffixLength].ToArray());

                    int tokenSystem = 10;

                    Dictionary<string, int> prefixes = new() { ["0b"] = 2, ["0x"] = 16, ["0"] = 8 };

                    string literal = _currentLexeme.ToString() + suffix;

                    if (_currentLexeme.ToString().All(c => c == '0'))
                    {
                        _currentLexeme.Clear();
                        _currentLexeme.Append($"0{suffix}");
                    }
                    else
                    {
                        _currentLexeme.Append(suffix);

                        foreach (var (prefix, system) in prefixes)
                        {
                            if (literal.StartsWith(prefix))
                            {
                                _currentLexeme = new StringBuilder(literal[prefix.Length..]);

                                ClearLeadingZeros();
                                _currentLexeme.Insert(0, prefix);

                                tokenSystem = system;
                                break;
                            }
                        }
                    }

                    bool isSigned = !suffix.StartsWith('u');
                    bool isLong = suffix.Count(c => c == 'l') == 2;
                    string type = suffix.Count(c => c == 'l') != 0 ? "long" : "int";

                    Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned, isLong, type, tokenSystem), _currentLexeme.ToString()));

                    i += suffixLength;
                    Parser._state = new DefaultState();
                }
                else
                {
                    int suffixLength = suffixList.Count - rest.Count;
                    string invalidSuffix = new string(suffixList[..suffixLength].ToArray());

                    throw new LexicalException($"Invalid literal: {_currentLexeme}{invalidSuffix}.");
                }
            }
            else
            {
                throw new Exception("???");
            }
        }
    }

    private class FloatingPointLiteralState : NumericLiteralState
    {
        private readonly List<char> _specialsSymbols;

        public FloatingPointLiteralState()
            : base()
        {
            _specialsSymbols = [];
        }

        public FloatingPointLiteralState(string lexemePart)
            : base(lexemePart)
        {
            _specialsSymbols = [];
        }

        public override void Process(string text, ref int i)
        {
            char c = text[i];

            if (char.IsNumber(c))
            {
                _currentLexeme.Append(c);
                i++;
            }
            else if (c == '.')
            {
                if (_specialsSymbols.Count > 0)
                {
                    throw new LexicalException("Invalid point position in number");
                }
                else
                {
                    _currentLexeme.Append(c);
                    _specialsSymbols.Add(c);
                    i++;
                }
            }
            else if (char.ToLower(c) == 'e')
            {
                if (_specialsSymbols is ['.', 'e' or 'E', ..] or ['e' or 'E', ..])
                {
                    throw new LexicalException("Invalid exponent position in number");
                }
                else
                {
                    ClearTrailingZeros();

                    _currentLexeme.Append(char.ToLower(c));
                    i++;

                    if (i < text.Length && text[i] is '+' or '-')
                    {
                        if (text[i] == '-')
                            _currentLexeme.Append(text[i]);

                        i++;
                    }
                }
            }
            else if (char.ToLower(c) == 'f')
            {
                if ((_currentLexeme[^1] != '.') 
                    && (i == text.Length - 1 
                        || (i + 1 < text.Length 
                            && !char.IsLetter(text[i + 1]) 
                            && !char.IsNumber(text[i + 1]) 
                            && text[i + 1] != '.')))
                {
                    if (!_currentLexeme.ToString().Contains('e'))
                        ClearTrailingZeros();

                    _currentLexeme.Append(char.ToLower(c));

                    ClearLeadingZeros();

                    Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned: false, isLong: false, "float"), _currentLexeme.ToString()));

                    i++;
                    Parser._state = new DefaultState();
                }
                else
                {
                    throw new LexicalException("Invalid float literal.");
                }
            }
            else if (char.IsLetter(c))
            {
                throw new LexicalException("Invalid float literal");
            }
            else
            {
                char prev = _currentLexeme[^1];

                if (prev is  '.' or 'e' )                
                    throw new LexicalException($"Float literal can't end with {prev} .");
                
                ClearLeadingZeros();
                ClearTrailingZeros();

                Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned: false, isLong: false, "double"), _currentLexeme.ToString()));
                Parser._state = new DefaultState();
            }
        }

        private void ClearTrailingZeros()
        {
            while (_currentLexeme.Length != 0 && _currentLexeme[^1] == '0')
            {
                _currentLexeme.Remove(_currentLexeme.Length - 1, 1);
            }

            if (_currentLexeme.Length == 0 || !char.IsNumber(_currentLexeme[^1]))
                _currentLexeme.Insert(_currentLexeme.Length - 1, '0');
        }
    }


    private class PointState : State
    {
        public override void Process(string text, ref int i)
        {
            char c = text[i];

            if (c == '.')
            {
                if (i + 1 < text.Length)
                {
                    if (char.IsLetter(text[i + 1]))
                    {
                        Parser._tokens.Add(new(0, TokenType.Punctuator, "."));
                        i++;
                        Parser._state = new IdentifierState();
                    }
                    else if (char.IsNumber(text[i + 1]))
                    {
                        Parser._state = new FloatingPointLiteralState();
                    }
                    else
                    {
                        throw new LexicalException("Invalid point position.");
                    }
                }
                else
                {
                    throw new LexicalException("Invalid point position");
                }
            }
            else
            {
                throw new Exception("???");
            }
        }
    }


    private abstract class CharacterState : State
    {
        protected static void ProcessEscapeSequence(string text, ref int i)
        {
            if (text[i] == '\\')
            {
                if (i + 1 < text.Length)
                {
                    i++;

                    if ("befnrtv\\?\"a\'".Contains(text[i]))
                    {
                        Parser._tokens.Add(new(0, TokenType.EscapeSequence, $"\\{text[i]}"));
                        i++;
                    }
                    else if (i + 3 < text.Length && text[i..(i + 3)] is [>= '0' and <= '8', >= '0' and <= '8', >= '0' and <= '8'])
                    {
                        Parser._tokens.Add(new(0, TokenType.EscapeSequence, $"\\{text[i..(i + 3)]}"));
                        i += 3;
                    }
                    else if (i + 5 < text.Length && text[i..(i + 5)] is
                    [
                        'x' or 'X',
                        >= '0' and <= '9' or >= 'A' and <= 'F',
                        >= '0' and <= '9' or >= 'A' and <= 'F',
                        >= '0' and <= '9' or >= 'A' and <= 'F',
                        >= '0' and <= '9' or >= 'A' and <= 'F'
                    ])
                    {
                        Parser._tokens.Add(new(0, TokenType.EscapeSequence, $"\\{text[i..(i + 5)]}"));
                        i += 5;
                    }
                    else if (i + 3 < text.Length && text[i..(i + 3)] is
                    [
                        'x' or 'X',
                        >= '0' and <= '9' or >= 'A' and <= 'F',
                        >= '0' and <= '9' or >= 'A' and <= 'F'
                    ])
                    {
                        Parser._tokens.Add(new(0, TokenType.EscapeSequence, $"\\{text[i..(i + 3)]}"));
                        i += 3;
                    }
                    else if (text[i] == '0')
                    {
                        Parser._tokens.Add(new(0, TokenType.EscapeSequence, $"\\0"));
                        i++;
                    }
                    else
                    {
                        throw new LexicalException("Invalid escape sequence");
                    }
                }
                else
                {
                    throw new LexicalException("Invalid escape sequence");
                }
            }
            else
            {
                throw new Exception("???");
            }
        }
    }

    private class StringLiteralState : CharacterState
    {
        public override void Process(string text, ref int i)
        {
            if (text[i] == '"' || (i + 1 < text.Length && text[i..(i + 2)] is ['L', '"']))
            {
                if (i + 1 < text.Length)
                {
                    if (text[i] == '"')
                    {
                        i++;
                        Parser._tokens.Add(new(0, TokenType.DoubleQuotes, "\""));
                    }
                    else
                    {
                        i += 2;
                        Parser._tokens.Add(new(0, TokenType.UnicodeDoubleQuotes, "L\""));
                    }

                    do
                    {
                        if (text[i] == '\\')
                        {
                            if (_currentLexeme.Length > 0)
                            {
                                Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned: true, isLong: false, "string"), _currentLexeme.ToString()));
                                _currentLexeme.Clear();
                            }

                            ProcessEscapeSequence(text, ref i);
                        }
                        else if (text[i] == '"')
                        {
                            break;
                        }
                        else if (text[i] == '\n')
                        {
                            throw new LexicalException("Unclosed quote.");
                        }
                        else
                        {
                            _currentLexeme.Append(text[i]);
                            i++;
                        }

                        if (i >= text.Length)
                        {
                            throw new LexicalException("Unclosed quote.");
                        }
                    }
                    while (text[i] != '"');

                    if (_currentLexeme.Length > 0)
                        Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned: true, isLong: false, "string"), _currentLexeme.ToString()));

                    List<Token> subTokens = [];

                    do
                    {
                        subTokens.Add(Parser._tokens[^1]);
                        Parser._tokens.RemoveAt(_tokens.Count - 1);
                    }
                    while (subTokens[^1].TokenType != TokenType.DoubleQuotes && subTokens[^1].TokenType != TokenType.UnicodeDoubleQuotes);


                    subTokens.Insert(0, new(0, TokenType.DoubleQuotes, "\""));

                    Parser._tokens.Add(new MultiToken(0, TokenType.FromLiteralType(isSigned: true, isLong: false, "string"), subTokens));

                    i++;

                    Parser._state = new DefaultState();
                }
                else
                {
                    throw new LexicalException("Unclosed double quote.");
                }
            }
            else
            {
                throw new Exception("???");
            }
        }
    }

    private class CharLiteralState : CharacterState
    {
        public override void Process(string text, ref int i)
        {
            if (text[i] == '\'')
            {
                Parser._tokens.Add(new(0, TokenType.Quotes, "'"));

                if (i + 1 < text.Length)
                {
                    if (text[i + 1] == '\\')
                    {
                        i++;
                        ProcessEscapeSequence(text, ref i);
                    }
                    else if (text[i + 1] == '\'')
                    {
                        throw new LexicalException("Empty char literal");
                    }
                    else
                    {
                        Parser._tokens.Add(new(0, TokenType.FromLiteralType(isSigned: true, isLong: false, "char"), text[i + 1].ToString()));
                        i += 2;

                        if (i >= text.Length)
                            throw new LexicalException("Unclosed quote.");
                    }

                    if (text[i] == '\'')
                    {
                        List<Token> subTokens = [];

                        do
                        {
                            subTokens.Add(Parser._tokens[^1]);
                            Parser._tokens.RemoveAt(_tokens.Count - 1);
                        }
                        while (subTokens[^1].TokenType != TokenType.Quotes);

                        subTokens.Insert(0, new(0, TokenType.Quotes, "'"));

                        Parser._tokens.Add(new MultiToken(0, TokenType.FromLiteralType(isSigned: true, isLong: false, "char"), subTokens));




                        //Parser._tokens.Add(new(0, TokenType.Quotes, "'"));

                        i++;
                        Parser._state = new DefaultState();
                    }
                    else
                    {
                        throw new LexicalException("Invalid char literal");
                    }
                }
                else
                {
                    throw new LexicalException("Unclosed quote.");
                }
            }
            else
            {
                throw new Exception("???");
            }
        }
    }


    private class SlashState : State
    {
        public override void Process(string text, ref int i)
        {
            if (text[i] == '/')
            {
                if (i + 1 < text.Length)
                {
                    Parser._state = text[i + 1] switch
                    {
                        '/' => new LineCommentState(),
                        '*' => new BlockCommentState(),
                        _ => new OperatorState(),
                    };
                }
                else
                {
                    Parser._state = new OperatorState();
                }
            }
            else
            {
                throw new Exception("???");
            }
        }
    }

    private class BlockCommentState : State
    {
        public override void Process(string text, ref int i)
        {
            for (; i + 2 < text.Length && text[i..(i + 2)] is not "*/"; i++)
            { }

            if (i + 2 >= text.Length)
                throw new LexicalException("Unclosed block comment.");

            Parser._state = new DefaultState();

            i += 2;
        }
    }

    private class LineCommentState : State
    {
        public override void Process(string text, ref int i)
        {
            for (; i < text.Length && text[i] != '\n'; i++)
            { }

            Parser._state = new DefaultState();
        }
    }

    private class OperatorState : State
    {
        public override void Process(string text, ref int i)
        {
            if ("+-*/%<>~|&^=!?:".Contains(text[i]))
            {
                List<char> operatorList = [];

                for (int j = i; j < text.Length && j < i + 3; j++)
                {
                    operatorList.Add(char.ToUpper(text[j]));
                }

                var rest = operatorList switch
                {
                    ['+', '+', .. var rest1] => rest1,
                    ['+', '=', .. var rest1] => rest1,
                    ['+', .. var rest1] => rest1,

                    ['-', '-', .. var rest1] => rest1,
                    ['-', '=', .. var rest1] => rest1,
                    ['-', '>', .. var rest1] => rest1,
                    ['-', .. var rest1] => rest1,

                    ['*', '=', .. var rest1] => rest1,
                    ['*', .. var rest1] => rest1,

                    ['/', '=', .. var rest1] => rest1,
                    ['/', .. var rest1] => rest1,

                    ['%', '=', .. var rest1] => rest1,
                    ['%', .. var rest1] => rest1,

                    ['<', '<', '=', .. var rest1] => rest1,
                    ['<', '<', .. var rest1] => rest1,
                    ['<', '=', .. var rest1] => rest1,
                    ['<', .. var rest1] => rest1,

                    ['>', '>', '=', .. var rest1] => rest1,
                    ['>', '>', .. var rest1] => rest1,
                    ['>', '=', .. var rest1] => rest1,
                    ['>', .. var rest1] => rest1,

                    ['~', '=', .. var rest1] => rest1,
                    ['~', .. var rest1] => rest1,

                    ['|', '|', .. var rest1] => rest1,
                    ['|', '=', .. var rest1] => rest1,
                    ['|', .. var rest1] => rest1,

                    ['&', '&', .. var rest1] => rest1,
                    ['&', '=', .. var rest1] => rest1,
                    ['&', .. var rest1] => rest1,

                    ['^', '=', .. var rest1] => rest1,
                    ['^', .. var rest1] => rest1,

                    ['=', '=', .. var rest1] => rest1,
                    ['=', .. var rest1] => rest1,

                    ['!', '=', .. var rest1] => rest1,
                    ['!', .. var rest1] => rest1,

                    ['?' or ':', .. var rest1] => rest1,

                    _ => [],
                };

                string @operator = new string(operatorList[..(operatorList.Count - rest.Count)].ToArray());

                Parser._tokens.Add(new(0, TokenType.Punctuator, @operator));

                i += @operator.Length;
                Parser._state = new DefaultState();
            }
            else
            {
                throw new Exception("???");
            }
        }
    }


    private class PreprocessorDirectiveState : State
    {
        public override void Process(string text, ref int i)
        {
            while (text[i] != '\n' && i < text.Length)
            {
                _currentLexeme.Append(text[i]);
                i++;
            }

            string directive = _currentLexeme.ToString().TrimEnd();

            Parser._tokens.Add(new(0, TokenType.PreprocessorDirective, directive));
            Parser._state = new DefaultState();
        }
    }


    private class IdentifierState : State
    {
        public override void Process(string text, ref int i)
        {
            char c = text[i];

            if (_currentLexeme.Length == 0 && char.IsNumber(c))
            {
                throw new LexicalException("Identifier can't start with number.");
            }
            else if (char.IsNumber(c) || char.IsLetter(c) || c == '_')
            {
                _currentLexeme.Append(c);
                i++;
            }
            else
            {
                Parser._tokens.Add(new(0, TokenType.Identifier, _currentLexeme.ToString()));
                Parser._state = new DefaultState();
            }
        }
    }
}