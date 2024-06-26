﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Trans;

public static partial class Parser
{
    private abstract partial class Grammar
    {
        public abstract Node? Parse(List<Token> tokens, ref int i);

        public static Grammar operator | (Grammar left, Grammar right)
        {
            ArgumentNullException
                .ThrowIfNull(left, nameof(left));

            ArgumentNullException 
                .ThrowIfNull(right, nameof(right));

            return new DelegateGrammar(ParseOr);


            Node? ParseOr(List<Token> tokens, ref int i)
            {
                int initial = i;
                if (left.Parse(tokens, ref i) is Node leftNode)
                    return leftNode;

                i = initial;
                if (right.Parse(tokens, ref i) is Node rightNode)
                    return rightNode;

                i = initial;
                return null;
            }
        }


        public GrammarRow Then(Grammar other, int count = 1)
        {
            ArgumentOutOfRangeException
                .ThrowIfLessThanOrEqual(count, 0, nameof(count));

            return new GrammarRow() { (this, count: 1, null), (other, count, null) };
        }

        public class GrammarRow 
            : List<(Grammar, int count, string? errorMessage)> 
        {
            public GrammarRow Then(Grammar grammar, int count = 1)
            {
                ArgumentOutOfRangeException
                    .ThrowIfLessThanOrEqual(count, 0, nameof(count));

                Add((grammar, count, null));
                return this;
            }

            public GrammarRow WithError(string errorMessage)
            {
                this[^1] = this[^1] with { errorMessage = errorMessage };

                return this;
            }

            public Grammar AsNode(Func<List<Node>, Node> merge)
            {
                return new DelegateGrammar(ParseAll);


                Node? ParseAll(List<Token> tokens, ref int i)
                {
                    int next = i;

                    List<Node> nodes = [];

                    foreach (var (grammar, count, errorMessage) in this)
                    {
                        for (int j = 0; j < count; j++)
                        {
                            if (grammar.Parse(tokens, ref next) is not Node node)
                            {
                                if (errorMessage is not null)
                                {
                                    throw new SyntaxException(errorMessage);
                                }
                                else
                                {
                                    return null;
                                }
                            }
                            else
                            {
                                nodes.Add(node);
                            }
                        }        
                    }

                    i = next;
                    return merge(nodes);
                }
            }
        }


        public Grammar ThenNested(Grammar other, Func<Node, Node, Node> merge, bool canEmpty = false)
        {
            return new DelegateGrammar(ParseNested);


            Node? ParseNested(List<Token> tokens, ref int i)
            {
                int initial = i;
                LinkedList<Node> nodes = [];

                if (this.Parse(tokens, ref i) is not Node startNode)
                {
                    i = initial;
                    return null;
                }
                else
                {
                    nodes.AddLast(startNode);
                }

                int next = i;
                

                while (true)
                {
                    if (other.Parse(tokens, ref next) is Node nextNode)
                    {
                        nodes.AddLast(nextNode);
                        i = next;
                    }
                    else
                    {
                        break;
                    }
                }

                int minNodesCount = canEmpty ? 1 : 2;

                if (nodes.Count < minNodesCount)
                {
                    i = initial;
                    return null;
                }

                if (nodes.Count == 1 && canEmpty)
                    return nodes.First!.Value;

                return nodes.Aggregate(merge);
            }
        }
    }

    private class LazyGrammar : Grammar
    {
        private readonly Lazy<Grammar> _lazy;


        public LazyGrammar(Func<Grammar> factory)
        {
            _lazy = new(factory);
        }

        public override Node? Parse(List<Token> tokens, ref int i)
        {
            return _lazy.Value.Parse(tokens, ref i);
        }
    }


    private class DelegateGrammar : Grammar
    {
        public delegate Node? ParseDelegate(List<Token> tokens, ref int i);

        private ParseDelegate _parse;


        public DelegateGrammar(ParseDelegate parse)
        {
            _parse = parse;
        }

        public override Node? Parse(List<Token> tokens, ref int i)
        {
            return _parse(tokens, ref i);
        }
    }

    private class PredicateGrammar : Grammar
    {
        private readonly Func<Token, bool> _predicate;


        public PredicateGrammar(Func<Token, bool> predicate)
        {
            _predicate = predicate;
        }

        public override Node? Parse(List<Token> tokens, ref int i)
        {
            if (i >= tokens.Count || !_predicate(tokens[i]))
                return null;

            return new ValueNode(tokens[i++]);
        }
    }

    private class EmptyGrammar : Grammar
    {
        public override Node? Parse(List<Token> tokens, ref int i)
        {
            return new EmptyNode();
        }
    }

    private class BinaryOperatorGrammar : Grammar
    {
        private readonly Grammar _grammar;


        public BinaryOperatorGrammar(Grammar leftGrammar, Grammar rightGrammar, Grammar operatorGrammar, string? operatorName = null)
        {
            _grammar =
                leftGrammar
                    .ThenNested(operatorGrammar
                        .Then(rightGrammar)
                        .AsNode(nodes => new ParentNode(nodes)),
                    merge: (node1, node2) =>
                    {
                        return new BinaryOperatorNode(node1, ((ParentNode)node2).Children.ToArray()[1], ((ValueNode)((ParentNode)node2).Children.ToArray()[0]).Token);
                    });
        }

        public override Node? Parse(List<Token> tokens, ref int i)
        {
            return _grammar.Parse(tokens, ref i);
        }
    }

    private class PreUnaryOperatorGrammar : Grammar
    {
        private readonly Grammar _operandGrammar;

        private readonly string _operator;
        private readonly string? _operatorName;


        public PreUnaryOperatorGrammar(Grammar operandGrammar, string @operator, string? operatorName = null)
        {
            _operandGrammar = operandGrammar;

            _operator = @operator;
            _operatorName = operatorName;
        }

        public override Node? Parse(List<Token> tokens, ref int i)
        {
            int next = i;

            if (next >= tokens.Count || tokens[next].Value != _operator)
                return null;

            next++;

            if (_operandGrammar.Parse(tokens, ref next) is not Node operandNode)
                return null;

            i = next;
            return new UnaryOperatorNode(operandNode, _operatorName ?? _operator);
        }
    }

    private class PostUnaryOperatorGrammar : Grammar
    {
        private readonly Grammar _operandGrammar;

        private readonly string _operator;
        private readonly string? _operatorName;


        public PostUnaryOperatorGrammar(Grammar operandGrammar, string @operator, string? operatorName = null)
        {
            _operandGrammar = operandGrammar;

            _operator = @operator;
            _operatorName = operatorName;
        }

        public override Node? Parse(List<Token> tokens, ref int i)
        {
            int next = i;

            if (_operandGrammar.Parse(tokens, ref next) is not Node operandNode)
                return null;


            if (next >= tokens.Count || tokens[next].Value != _operator)
                return null;

            i = next + 1;
            return new UnaryOperatorNode(operandNode, _operatorName ?? _operator);
        }
    }

    private class TokenGrammar : PredicateGrammar
    {
        public TokenGrammar(TokenType tokenType, string? token = null)
            : base (t => t.TokenType == tokenType && (token is null || t.Value == token))
        { }

        public static Grammar Any(params (TokenType tokenType, string token)[] tokens)
        {
            return tokens[1..].Aggregate(new TokenGrammar(tokens[0].tokenType, tokens[0].token) as Grammar, (sum, arg) => sum | new TokenGrammar(arg.tokenType, arg.token));
        }
        public static Grammar Any(TokenType tokenType, params string[] tokens)
        {
            return tokens[1..].Aggregate(new TokenGrammar(tokenType, tokens[0]) as Grammar, (sum, token) => sum | new TokenGrammar(tokenType, token));
        }
    }

    private class BlockGrammar : Grammar
    {
        private readonly string _left;
        private readonly string _right;
        private readonly string? _operatorName;

        private readonly Grammar _innerGrammar;


        public BlockGrammar(string left, string right, Grammar innerGrammar, string? operatorName = null)
        {
            _left = left;
            _right = right;

            _innerGrammar = innerGrammar;
            _operatorName = operatorName;
        }

        public override Node? Parse(List<Token> tokens, ref int i)
        {
            int next = i;

            if (next >= tokens.Count || tokens[next].Value != _left)
                return null;

            next++;

            if (_innerGrammar.Parse(tokens, ref next) is not Node innerNode)
                return null;

            if (next >= tokens.Count || tokens[next].Value != _right)
                return null;

            i = next + 1;
            return new UnaryOperatorNode(innerNode, _operatorName ?? $"{_left}..{_right}");
        }
    }

    private class ListGrammar : Grammar
    {
        private readonly Grammar _elemGrammar;
        private readonly string _operator;
        private readonly string? _separator;

        private readonly bool _canEmpty;


        public ListGrammar(Grammar elemGrammar, string @operator, string? separator = ",", bool canEmpty = false)
        {
            _elemGrammar = elemGrammar;
            _operator = @operator;
            _separator = separator;
            _canEmpty = canEmpty;
        }

        public override Node? Parse(List<Token> tokens, ref int i)
        {
            int initial = i;

            LinkedList<Node> nodes = [];

            while (true)
            {
                if (_elemGrammar.Parse(tokens, ref i) is not Node elemNode)
                {
                    if (nodes.Count == 0 && _canEmpty || _separator is null)
                    {
                        break;
                    }
                    else
                    {
                        return null;
                    }
                }

                nodes.AddLast(elemNode);

                if (_separator is not null)
                    if (new TokenGrammar(TokenType.Punctuator, _separator).Parse(tokens, ref i) is null)
                        break;
            }

            return new OperatorNode(nodes, _operator);
        }
    }

    private partial class Grammar 
    {
        public static LazyGrammar ProgramGrammar => new(() =>
        {
            return
                new ListGrammar(StructDeclarationGrammar
                    | FunctionDeclarationGrammar
                    | FunctionPrototypeGrammar
                    | VariableCreatingGrammar
                        .Then(new TokenGrammar(TokenType.Punctuator, ";")).WithError("; missed after variable creating.")
                        .AsNode(nodes => nodes[0])
                    | new TokenGrammar(TokenType.PreprocessorDirective),
                    "Program",
                    separator: null,
                    canEmpty: true);
        });



        public static LazyGrammar StructDeclarationGrammar => new(() =>
        {
            return
                new TokenGrammar(TokenType.Type, "struct")
                .Then(IdentifierGrammar)
                .Then(new BlockGrammar("{", "}", new ListGrammar(VariableDeclarationGrammar
                    .Then(new TokenGrammar(TokenType.Punctuator, ";")).WithError("Invalid struct field declaration: ';' is missed.")
                    .AsNode(nodes => nodes[0]), "Struct fields", separator: null, canEmpty: true), "Struct fields"))
                .Then(new TokenGrammar(TokenType.Punctuator, ";")).WithError("Invalid structure declaration: ';' is missed.")
                .AsNode(nodes => new BinaryOperatorNode(nodes[1], ((OperatorNode)nodes[2]).Children.ToArray()[0], "Struct declaration"));
        });

        public static LazyGrammar FunctionDeclarationGrammar => new(() =>
        {
            return
                TypeGrammar
                .Then(IdentifierGrammar)
                .Then(new TokenGrammar(TokenType.Punctuator, "("))
                .Then(new ListGrammar(VariableCreatingGrammar, "Function parameters", canEmpty: true))
                .Then(new TokenGrammar(TokenType.Punctuator, ")")).WithError("Invalid function declaration: ')' is missed.")
                .Then(ScopeGrammar)
                .AsNode(nodes => new OperatorNode([nodes[0], nodes[1], nodes[3], nodes[5]], "Function declaration"));
        });

        public static LazyGrammar FunctionPrototypeGrammar => new(() =>
        {
            return
               TypeGrammar
               .Then(IdentifierGrammar)
               .Then(new TokenGrammar(TokenType.Punctuator, "("))
               .Then(new ListGrammar(VariableCreatingGrammar, "Function parameters", canEmpty: true))
               .Then(new TokenGrammar(TokenType.Punctuator, ")")).WithError("Invalid function declaration: ')' is missed.")
               .Then(new TokenGrammar(TokenType.Punctuator, ";"))
               .AsNode(nodes => new OperatorNode([nodes[0], nodes[1], nodes[3]], "Function prototype"));
        });

        public static LazyGrammar IfElseIfElseGrammar => new(() =>
        {
            return
                new TokenGrammar(TokenType.KeyWord, "if")
                    .Then(new TokenGrammar(TokenType.Punctuator, "(")).WithError("Invalid if declaration: '(' is missed.")
                    .Then(ExpressionGrammar).WithError("Invalid if declaration: predicate is missed.")
                    .Then(new TokenGrammar(TokenType.Punctuator, ")")).WithError("Invalid if declaration: ')' is missed.")
                    .Then(ScopeGrammar | LineGrammar).WithError("Invalid if declaration: body is missed.")
                    .AsNode(nodes => new BinaryOperatorNode(nodes[2], nodes[4], "If statement"))
                .Then(new ListGrammar(
                    new TokenGrammar(TokenType.KeyWord, "else")
                    .Then(new TokenGrammar(TokenType.KeyWord, "if"))
                    .Then(new TokenGrammar(TokenType.Punctuator, "(")).WithError("Invalid else if declaration: '(' is missed.")
                    .Then(ExpressionGrammar).WithError("Invalid else if declaration: predicate is missed.")
                    .Then(new TokenGrammar(TokenType.Punctuator, ")")).WithError("Invalid if declaration: ')' is missed.")
                    .Then(ScopeGrammar | LineGrammar).WithError("Invalid else if declaration: body is missed.")
                    .AsNode(nodes => new BinaryOperatorNode(nodes[3], nodes[5], "Else if statement")),
                "Else if operators",
                separator: null,
                canEmpty: true))
                .AsNode(nodes =>
                {
                    var ifNode = nodes[0];
                    var elseIfNodes = ((OperatorNode)nodes[1]).Children;

                    List<Node> ifElseIfNodes = [ifNode, .. elseIfNodes];

                    return new OperatorNode(ifElseIfNodes, "If else if statements");
                })
                .Then(new TokenGrammar(TokenType.KeyWord, "else")
                    .Then(ScopeGrammar | LineGrammar).WithError("Invalid else declaration: body is missed.")
                    .AsNode(nodes => new UnaryOperatorNode(nodes[1], "Else statement"))
                    | EmptyGrammar)
                .AsNode(nodes =>
                {
                    var ifElseIfNode = nodes[0];
                    var elseNode = nodes[1];

                    if (elseNode is EmptyNode)
                        return ifElseIfNode;

                    var ifElseIfNodes = ((OperatorNode)ifElseIfNode).Children;

                    List<Node> ifElseIfElseNodes = [..ifElseIfNodes, elseNode];

                    return new OperatorNode(ifElseIfElseNodes, "If else if else statements");
                });
        });

        public static LazyGrammar SwitchCaseGrammar => new(() =>
        {
            return
                new TokenGrammar(TokenType.KeyWord, "switch")
                .Then(new TokenGrammar(TokenType.Punctuator, "("))
                .Then(ExpressionGrammar)
                .Then(new TokenGrammar(TokenType.Punctuator, ")"))
                .Then(new TokenGrammar(TokenType.Punctuator, "{"))
                .Then(new ListGrammar(CaseGrammar, "Cases", separator: null, canEmpty: true))
                .Then(new TokenGrammar(TokenType.Punctuator, "}"))
                .AsNode(nodes => 
                {
                    var defaultCount = ((OperatorNode)nodes[5]).Children
                        .Count(@case =>
                        {
                            var caseValue = ((OperatorNode)@case).Children.First();

                            if (caseValue is ValueNode { Token.Value: "default" })
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        });

                    if (defaultCount > 1)
                        throw new SyntaxException("Switch case has more than one default.");

                    return new BinaryOperatorNode(nodes[2], nodes[5], "Switch case"); 
                });
        });

        public static LazyGrammar CaseGrammar => new(() =>
        {
            return
                (new TokenGrammar(TokenType.KeyWord, "case")
                    .Then(ExpressionGrammar)
                    .AsNode(nodes => nodes[1])
                    | new TokenGrammar(TokenType.KeyWord, "default"))
                .Then(new TokenGrammar(TokenType.Punctuator, ":"))
                .Then(ScopeGrammar | LinesGrammar)
                    .AsNode(nodes => new BinaryOperatorNode(nodes[0], nodes[2], "Case"));
        });

        public static LazyGrammar ForGrammar => new(() =>
        {
            return
                new TokenGrammar(TokenType.KeyWord, "for")
                .Then(new TokenGrammar(TokenType.Punctuator, "(")).WithError("Invalid for declaration: '(' is missed.")
                .Then(InstructionGrammar | EmptyGrammar)
                .Then(new TokenGrammar(TokenType.Punctuator, ";")).WithError("Invalid for declaration: ';' is missed.")
                .Then(InstructionGrammar | EmptyGrammar)
                .Then(new TokenGrammar(TokenType.Punctuator, ";")).WithError("Invalid for declaration: ';' is missed.")
                .Then(InstructionGrammar | EmptyGrammar)
                .Then(new TokenGrammar(TokenType.Punctuator, ")")).WithError("Invalid for declaration: ')' is missed.")
                .Then(ScopeGrammar | LineGrammar).WithError("Invalid for declaration: body is missed.")
                .AsNode(nodes => new OperatorNode([nodes[2], nodes[4], nodes[6], nodes[8]], "For loop"));
        });


        public static LazyGrammar DoWhileGrammar => new(() =>
        {
            return
                new TokenGrammar(TokenType.KeyWord, "do")
                .Then(ScopeGrammar | LineGrammar)
                .Then(new TokenGrammar(TokenType.KeyWord, "while")).WithError("Invalid do while declaration: 'while' is missed.")
                .Then(new TokenGrammar(TokenType.Punctuator, "(")).WithError("Invalid do while declaration: '(' is missed.")
                .Then(ExpressionGrammar)
                .Then(new TokenGrammar(TokenType.Punctuator, ")")).WithError("Invalid do while declaration: ')' is missed.")
                .Then(new TokenGrammar(TokenType.Punctuator, ";")).WithError("Invalid do while declaration: ';' is missed.")
                .AsNode(nodes => new BinaryOperatorNode(nodes[4], nodes[1], "Do while loop"));
        });

        public static LazyGrammar WhileGrammar => new(() =>
        {
            return
                new TokenGrammar(TokenType.KeyWord, "while")
                .Then(new TokenGrammar(TokenType.Punctuator, "(")).WithError("Invalid while declaration: '(' is missed.")
                .Then(ExpressionGrammar)
                .Then(new TokenGrammar(TokenType.Punctuator, ")")).WithError("Invalid while declaration: ')' is missed.")
                .Then(ScopeGrammar | LineGrammar)
                .AsNode(nodes => new BinaryOperatorNode(nodes[2], nodes[4], "While loop"));
        });

        public static LazyGrammar ScopeGrammar => new(() =>
        {
            return 
                new TokenGrammar(TokenType.Punctuator, "{")
                .Then(LinesGrammar)
                .Then(new TokenGrammar(TokenType.Punctuator, "}"))
                .AsNode(nodes => nodes[1]);
        });

        public static LazyGrammar LinesGrammar => new(() =>
        {
            return
                new ListGrammar(
                    LineGrammar 
                    | LabelGrammar 
                    | WhileGrammar 
                    | DoWhileGrammar 
                    | ForGrammar 
                    | IfElseIfElseGrammar 
                    | SwitchCaseGrammar, 
                "Block of code", separator: null, canEmpty: true);
        });

        public static LazyGrammar LineGrammar => new(() =>
        {
            return
                new ListGrammar(InstructionGrammar, "Instruction list", canEmpty: true)
                .Then(new TokenGrammar(TokenType.Punctuator, ";"))
                .AsNode(nodes =>
                {
                    if (nodes[0] is OperatorNode instructionsNode)
                    {
                        var instructions = instructionsNode.Children.ToArray();

                        string lineNodeValue = instructions.Length switch
                        {
                            0 => "Empty line ;",
                            1 => "Line ;",
                            _ => "Lines ;"
                        };

                        return new OperatorNode(instructions, lineNodeValue);
                    }
                    else
                    {
                        throw new Exception("???");
                    }
                });
        });

        public static LazyGrammar InstructionGrammar => new(() =>
        {
            return
                VariableCreatingGrammar
                | VariableDeclarationGrammar
                | ExpressionGrammar
                | KeyWordGrammar;
        });

        public static LazyGrammar LabelGrammar => new(() =>
        {
            return
                IdentifierGrammar
                .Then(new TokenGrammar(TokenType.Punctuator, ":"))
                .AsNode(nodes => new UnaryOperatorNode(nodes[0], "Label"));
        });

        public static LazyGrammar KeyWordGrammar => new(() =>
        {
            return
                new TokenGrammar(TokenType.KeyWord, "break")
                | new TokenGrammar(TokenType.KeyWord, "continue")
                | new TokenGrammar(TokenType.KeyWord, "return")
                  .Then(ExpressionGrammar | EmptyGrammar)
                  .AsNode(nodes => new UnaryOperatorNode(nodes[1], "Return"))
                | new TokenGrammar(TokenType.KeyWord, "goto")
                  .Then(IdentifierGrammar).WithError("Goto invalid declaration: label is missed.")
                  .AsNode(nodes => new UnaryOperatorNode(nodes[1], "Go to"));
        });

        public static LazyGrammar VariableCreatingGrammar => new(() =>  
        {
            return
                VariableDeclarationGrammar
                    .Then(new TokenGrammar(TokenType.Punctuator, "=")
                        .Then(ExpressionGrammar)
                        .AsNode(nodes => nodes[1]) 
                        | EmptyGrammar)
                    .AsNode(nodes => 
                    {
                        if (nodes[1] is EmptyNode)
                            return nodes[0];

                        return new BinaryOperatorNode(nodes[0], nodes[1], "Variable initialization");
                    })
                        .ThenNested(new TokenGrammar(TokenType.Punctuator, ",")
                            .ThenNested(new TokenGrammar(TokenType.Punctuator, "*") | new TokenGrammar(TokenType.Type, "const"),
                            merge: (ptrNode1, ptrNode2) =>
                            {
                                return (ptrNode1, ptrNode2) switch
                                {
                                    (ValueNode { Token.Value: "," }, ValueNode valueNode) => new TypesNode(valueNode.Token),
                                    (ValueNode valueNode1, ValueNode valueNode2) => new TypesNode(valueNode1.Token, valueNode1.Token),
                                    (TypesNode typesNode, ValueNode valueNode) => new TypesNode() { Types = [..typesNode.Types, valueNode.Token] },

                                    _ => throw new Exception("???")
                                };
                            },
                            canEmpty: true)
                            .Then(IdentifierGrammar)
                            .Then(new TokenGrammar(TokenType.Punctuator, "=")
                                .Then(ExpressionGrammar)
                                .AsNode(nodes => nodes[1])
                                | Grammar.EmptyGrammar)
                            .AsNode(nodes =>
                            {
                                Node? typesAddition;

                                if (nodes[0] is ValueNode { Token.Value: "," })
                                {
                                    typesAddition = new TypesNode();
                                }
                                else
                                {
                                    typesAddition = nodes[0];
                                }

                                var identifier = nodes[1];

                                return nodes[2] is not EmptyNode
                                    ? new ParentNode([typesAddition, identifier, nodes[2]])
                                    : new ParentNode([typesAddition, identifier]);
                            }),
                            merge: (prevVarsNode, currentVarNode) =>
                            {
                                var nodes = (prevVarsNode switch
                                { 
                                    BinaryOperatorNode firstVarNode => [firstVarNode],
                                    OperatorNode prevVarsOperatorNode => prevVarsOperatorNode.Children,
                                    
                                    _ => throw new Exception("???")
                                })
                                .ToArray();

                                if (currentVarNode is ParentNode parentNode)
                                {
                                    var children = parentNode.Children.ToArray();

                                    var firstVarTypeOrDeclarationNode = ((OperatorNode)nodes[0]).Children.First();

                                    List<Token> type = firstVarTypeOrDeclarationNode switch
                                    { 
                                        ValueNode typeValueNode => [typeValueNode.Token],
                                        TypesNode typeTypesNode => typeTypesNode.Types,

                                        BinaryOperatorNode declarationNode => declarationNode.Children.ToArray()[0] switch
                                        {
                                            ValueNode typeValueNode => [typeValueNode.Token],
                                            TypesNode typeTypesNode => typeTypesNode.Types,

                                            _ => throw new Exception("???")
                                        },

                                        _ => throw new Exception("???")
                                    };

                                    var typesAddition = ((TypesNode)children[0]).Types;

                                    var newType = new TypesNode([.. type, .. typesAddition]);

                                    var varDeclaration = new BinaryOperatorNode(newType, children[1], "Variable declaration");

                                    if (children.Length == 3)
                                    {
                                        varDeclaration = new BinaryOperatorNode(varDeclaration, children[2], "Variable initialization");
                                    }

                                    currentVarNode = varDeclaration;
                                }
                                else
                                {
                                    throw new Exception("???");
                                }

                                nodes = [.. nodes, currentVarNode];

                                return new OperatorNode(nodes, "Variables declaration");
                            },
                            canEmpty: true)
                | VariableDeclarationGrammar;
        });

        public static LazyGrammar VariableDeclarationGrammar => new(() =>
        {
            return
                TypeGrammar
                    .Then(IdentifierGrammar)
                    .AsNode(nodes => new BinaryOperatorNode(nodes[0], nodes[1], "Variable declaration"))
                    .ThenNested(new TokenGrammar(TokenType.Punctuator, "[")
                        .Then(ExpressionGrammar | EmptyGrammar)
                        .Then(new TokenGrammar(TokenType.Punctuator, "]"))
                        .AsNode(nodes => nodes[1]),
                        merge: (varDecNode, bracketsNode) =>
                        {
                            var nodes = ((OperatorNode)varDecNode).Children.ToList();

                            nodes.Add(new UnaryOperatorNode(bracketsNode, $"Array declaration [{(bracketsNode is EmptyNode ? "" : "..")}]"));
                            return new OperatorNode(nodes, "Variable declaration");
                        },
                        canEmpty: true);
        });

        public static LazyGrammar ExpressionGrammar => new(() =>  
        {
            return 
                new BinaryOperatorGrammar(LValueGrammar, GGrammar, TokenGrammar.Any(TokenType.Punctuator, "=", "+=", "-=", "*=", "/=", "%=", "&=", "^=", "|="))
                | GGrammar
                | new BlockGrammar("{", "}", new ListGrammar(ExpressionGrammar, "Init values", canEmpty: false));
        });

        public static LazyGrammar GGrammar => new(() =>
        {
            return
                FGrammar
                    .Then(new TokenGrammar(TokenType.Punctuator, "?"))
                    .Then(GGrammar)
                    .Then(new TokenGrammar(TokenType.Punctuator, ":")).WithError("Invalid ternary operator: ':' is missed")
                    .Then(GGrammar)
                    .AsNode(nodes => new TernaryOperatorNode(nodes[0], nodes[2], nodes[4], "Ternary operator ?:"))
                | FGrammar;
        });

        public static LazyGrammar FGrammar => new(() =>
        {
            return
                new BinaryOperatorGrammar(EGrammar, EGrammar, TokenGrammar.Any(TokenType.Punctuator, "==", "!=",">", "<", ">=", "<=",    "&", "^", "|", "&&", "||"))
                | EGrammar;
        });

        public static LazyGrammar EGrammar => new(() =>
        {
            return
                new BinaryOperatorGrammar(DGrammar, DGrammar, TokenGrammar.Any(TokenType.Punctuator, "<<", ">>"))
                | DGrammar;
        });

        public static LazyGrammar DGrammar => new(() =>
        {
            return 
                new BinaryOperatorGrammar(CGrammar, CGrammar, TokenGrammar.Any(TokenType.Punctuator, "+", "-"))
                | CGrammar;
        });

        public static LazyGrammar CGrammar => new(() =>
        {
            return 
                new BinaryOperatorGrammar(BGrammar, BGrammar, TokenGrammar.Any(TokenType.Punctuator, "*", "/", "%")) 
                | BGrammar;
        });

        public static LazyGrammar BGrammar => new(() =>
        {
            return 
                new PreUnaryOperatorGrammar(LValueGrammar, "++", "Preincrement ++")
                | new PreUnaryOperatorGrammar(LValueGrammar, "--", "Predecrement --")
                | new PreUnaryOperatorGrammar(BGrammar, "-", "Negation -")
                | new PreUnaryOperatorGrammar(BGrammar, "+", "Plus +")
                | new PreUnaryOperatorGrammar(ExpressionGrammar, "*", "Indirection *")
                | new PreUnaryOperatorGrammar(BGrammar, "!", "Logical not !")
                | new PreUnaryOperatorGrammar(BGrammar, "~", "Bitwise complement ~")              
                | new PreUnaryOperatorGrammar(BGrammar, "&", "Address-of &")
                | new TokenGrammar(TokenType.KeyWord, "sizeof")
                    .Then(new TokenGrammar(TokenType.Punctuator, "(")).WithError("Invalid sizeof operator: '(' is missed.")
                    .Then(TypeGrammar).WithError("Invalid sizeof operator: type is missed.")
                    .Then(new TokenGrammar(TokenType.Punctuator, ")")).WithError("Invalid sizeof operator: ')' is missed.")
                    .AsNode(nodes => new OperatorNode([nodes[2]] , "Sizeof"))
                | new TokenGrammar(TokenType.Punctuator, "(")
                    .Then(TypeGrammar)
                    .Then(new TokenGrammar(TokenType.Punctuator, ")"))
                    .Then(RValueGrammar)
                    .AsNode(nodes => new OperatorNode([nodes[1], nodes[3]], "Type cast"))
                | AGrammar;
        });


        public static LazyGrammar AGrammar => new(() =>
        {
            return 
                new PostUnaryOperatorGrammar(LValueGrammar, "++", "Postincrement ++")
                | new PostUnaryOperatorGrammar(LValueGrammar, "--", "Postdecrement --")
                | RValueGrammar
                    .Then(new TokenGrammar(TokenType.Punctuator, "("))
                    .Then(new ListGrammar(ExpressionGrammar, "Function call args", canEmpty: true))
                    .Then(new TokenGrammar(TokenType.Punctuator, ")")).WithError("Invalid function calling: ')' is missed.")
                    .AsNode(nodes => new OperatorNode((new[] { nodes[0] }).Concat((nodes[2] is OperatorNode args) ? args.Children : Array.Empty<Node>()) , "Function calling"))
                | RValueGrammar;
        });

        public static LazyGrammar RValueGrammar => new(() =>
        {
            return
                LValueGrammar 
                | new BlockGrammar("(", ")", ExpressionGrammar) 
                | LiteralGrammar;
        });

        public static LazyGrammar LValueGrammar => new(() =>
        {
            return 
                MemberedGrammar
                    .ThenNested(
                        new TokenGrammar(TokenType.Punctuator, "[")
                            .Then(DGrammar)
                            .Then(new TokenGrammar(TokenType.Punctuator, "]"))
                            .AsNode(nodes => new UnaryOperatorNode(nodes[1], "Indexer [..]"))
                        | new TokenGrammar(TokenType.Punctuator, ".")
                            .Then(IdentifierGrammar)
                            .AsNode(nodes => new UnaryOperatorNode(nodes[1], "Member access ."))
                        | new TokenGrammar(TokenType.Punctuator, "->")
                            .Then(IdentifierGrammar).WithError("Invalid -> operator: identifier is missed.")
                            .AsNode(nodes => new UnaryOperatorNode(nodes[1], "Member access ->")),
                         merge: (l, r) => new BinaryOperatorNode(l, ((UnaryOperatorNode)r).Child, ((UnaryOperatorNode)r).Operator))
                | IdentifierGrammar;
        }); 


        public static LazyGrammar MemberedGrammar => new(() =>
        {
            return IdentifierGrammar | new BlockGrammar("(", ")", ExpressionGrammar);
        });


        public static LazyGrammar TypeGrammar => new(() =>
        {
            return
                (StructType | new TokenGrammar(TokenType.Type))
                    .ThenNested(StructType | new TokenGrammar(TokenType.Type) | new TokenGrammar(TokenType.Punctuator, "*"),
                    merge: (node1, node2) =>
                    {
                        List<Token> tokens2 = node2 switch
                        {
                            ValueNode valueNode2 => [valueNode2.Token],  
                            TypesNode typesNode => [.. typesNode.Types],

                            _ => throw new Exception("???")
                        };

                        return node1 switch
                        {
                            ValueNode valueNode1 => new TypesNode([valueNode1.Token, .. tokens2]),
                            TypesNode typesNode1 => new TypesNode([..typesNode1.Types, .. tokens2]),

                            _ => throw new Exception("???")
                        };
                    },
                    canEmpty: true);
        });

        public static LazyGrammar StructType => new(() =>
        {
            return
                new TokenGrammar(TokenType.Type, "struct")
                .Then(IdentifierGrammar)
                .AsNode(nodes => new TypesNode([((ValueNode)nodes[0]).Token, ((ValueNode)nodes[1]).Token]));
        });

        public static LazyGrammar IdentifierGrammar => new(() =>
        {
            return new PredicateGrammar(t => (t.TokenType == TokenType.Identifier));
        });

        public static LazyGrammar LiteralGrammar => new(() =>
        {
            return new PredicateGrammar(t => (t.TokenType is LiteralType));
        });


        public static Grammar EmptyGrammar => new EmptyGrammar();
    }





    public abstract class Node
    { }

    public class ValueNode : Node
    {
        public Token Token { get; init; }
        public TypeInfo? TypeInfo { get; set; }

        public ValueNode(Token token)
        {
            Token = token;
        }
    }

    public class TypesNode : Node
    {
        public List<Token> Types { get; init; }

        public TypesNode(params Token[] types)
        {
            Types = new(types);
        }

        public TypeInfo TypeInfo { get; set; }
    }

    public sealed class EmptyNode : Node
    { }

    public class ParentNode : Node
    {
        public virtual IEnumerable<Node> Children { get; init; }


        public ParentNode(IEnumerable<Node> children)
        {
            Children = children;
        }
    }


    public class OperatorNode : ParentNode
    {
        public string Operator { get; init; }


        public OperatorNode(IEnumerable<Node> children, string @operator)
            : base(children)
        {
            Operator = @operator;
        }

        public OperatorNode(List<Node> children, Token token)
            : base(children)
        {
            Operator = token.Value;
        }
    }

    public class UnaryOperatorNode : OperatorNode
    {
        public Node Child => Children.First();

        public UnaryOperatorNode(Node child, Token token)
            : base([child], token)
        { }

        public UnaryOperatorNode(Node child, string @operator)
            : base([child], @operator)
        { }
    }

    public class BinaryOperatorNode : OperatorNode
    {
        public BinaryOperatorNode(Node child1, Node child2, Token token)
            : base([child1, child2], token)
        { }

        public BinaryOperatorNode(Node child1, Node child2, string @operator)
            : base([child1, child2], @operator)
        { }
    }

    public class TernaryOperatorNode : OperatorNode
    {
        public TernaryOperatorNode(Node child1, Node child2, Node child3, string @operator)
            : base([child1, child2, child3], @operator)
        { }
    }
}
