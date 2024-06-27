
using System.CodeDom;
using System.Data;
using System.Net.Http.Headers;
using System.Numerics;
using static System.Windows.Forms.AxHost;
using static Trans.Parser;

namespace Trans;


public static partial class Parser
{
    private static State _state = new DefaultState();
    private static readonly List<Token> _tokens = [];


    public static IEnumerable<Token> LexicalAnalysis(string text)
    {
        _state = new DefaultState();
        _tokens.Clear();

        text += ' ';

        for (int i = 0; i < text.Length;)
        {
            _state.Process(text, ref i);
        }

        int j = 0;

        Dictionary<string, int> dct = [];

        for (int i = 0; i < _tokens.Count; i++)
        {
            if (_types.Contains(_tokens[i].Value))
            {
                _tokens[i] = _tokens[i] with { TokenType = TokenType.Type };
            }
            else if (_keywords.Contains(_tokens[i].Value))
            {
                _tokens[i] = _tokens[i] with { TokenType = TokenType.KeyWord };
            }

            if (dct.TryGetValue(_tokens[i].Value, out int value))
            {
                _tokens[i] = _tokens[i] with { Id = value };
            }
            else
            {
                _tokens[i] = _tokens[i] with { Id = ++j };
                dct[_tokens[i].Value] = j;
            }
        }

        return _tokens.ToArray();
    }

    public static Node SyntaxAnalysis(List<Token> tokens)
    {
        int i = 0;

        var res = Grammar.ProgramGrammar.Parse(tokens, ref i);

        if (i < tokens.Count || res is null)
            throw new SyntaxException("Invalid syntax");
    
        return res;
    }

    public static void SemanticAnalysis(Node root)
    {
        CheckSemantic(root);
    }
}

