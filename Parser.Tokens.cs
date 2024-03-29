using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Trans;


public partial class Parser
{
    public record Token(int Id, TokenType TokenType, string Value);

    public record MultiToken(int Id, TokenType TokenType, IEnumerable<Token> Tokens)
        : Token(Id, TokenType, string.Join("", Tokens.Reverse().Select(t => t.Value)))
    { }

    private static readonly HashSet<string> _types =
    [
        "auto", "char", "const", "double", "float", "int", "long", "short", "struct", "signed", "unsigned", "void"
    ];



    private static readonly HashSet<string> _keywords =
    [
        "auto", "break", "case", "char", "const", "continue",
        "default", "do", "double", "else", "enum", "extern",
        "float", "for", "goto", "if", "inline", "int", "long",
        "register", "restrict", "return", "short", "signed",
        "sizeof", "static", "struct", "switch", "typedef", "union",
        "unsigned", "void", "volatile", "while"
    ];


    public class TokenType
    {
        private string _type;

        static TokenType()
        { }

        protected TokenType(string type)
        {
            _type = type;
        }

        public override string ToString()
        {
            return _type;
        }

        public static readonly TokenType Punctuator = new("Punctuator");
        public static readonly TokenType Identifier = new("Identifier");
        public static readonly TokenType Type = new("Type");
        public static readonly TokenType KeyWord = new("Keyword");
        public static readonly TokenType EscapeSequence = new("Escape sequence");
        public static readonly TokenType DoubleQuotes = new("Double quotes");
        public static readonly TokenType UnicodeDoubleQuotes = new("Unicode double quotes");
        public static readonly TokenType Quotes = new("Quotes");
        public static readonly TokenType PreprocessorDirective = new("Preprocessor directive");

        public static TokenType FromLiteralType(bool isSigned, bool isLong, string type, int? system = null)
            => new LiteralType(isSigned, isLong, type, system);
    }

    private class LiteralType : TokenType
    {
        public LiteralType(bool isSigned, bool isLong, string type, int? system = null)
            : base(CreateType(isSigned, isLong, type, system))
        { }


        private static string CreateType(bool isSigned, bool isLong, string type, int? system = null)
        {
            List<string> modifiersList = [];

            if (!isSigned)
                modifiersList.Add("unsigned");

            if (isLong)
                modifiersList.Add("long");

            modifiersList.Add(type);

            if (system is not null)
            {
                modifiersList.Add(system switch
                {
                    16 => "hex",
                    10 => "decimal",
                    8 => "octal",
                    2 => "binary",

                    _ => throw new ArgumentException("Unknown system.")
                });
            }


            modifiersList[0] = CapFirst(modifiersList[0]);

            return $"{string.Join(" ", modifiersList)} literal";


            static string CapFirst(string str)
            {
                int firstLetterIndex = str
                    .Select((c, i) => (c, i))
                    .Where(item => char.IsLetter(item.c))
                    .FirstOrDefault(('\0', -1))
                    .Item2;

                if (firstLetterIndex != -1)
                {
                    var array = str.ToArray();
                    array[firstLetterIndex] = char.ToUpper(array[firstLetterIndex]);
                    return new string(array);
                }
                else
                {
                    return str;
                }
            }
        }
    }
}
