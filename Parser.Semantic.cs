using Accessibility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Trans.Parser;


namespace Trans;


public static partial class Parser
{
    private static void CheckSemantic(Node root)
    {
        CheckLabelsSemantic(root);

        CheckVariableUsageSemantic(root);

    }


    private static void CheckLabelsSemantic(Node root)
    {
        List<Token> usedLabels = [];
        List<Token> declaredLabels = [];

        GetLabelsInfo(root, declaredLabels, usedLabels);


        var repetitiveDeclaredLabels = declaredLabels
            .GroupBy(l => l.Id)
            .Where(g => g.Count() > 1)
            .Select(y => y.First());

        if (repetitiveDeclaredLabels.Any())
            throw new SemanticException($"The same label is declared more than once: {repetitiveDeclaredLabels.First().Value}");

        var usedUndeclaredLabels = usedLabels.Except(declaredLabels);

        if (usedUndeclaredLabels.Any())
            throw new SemanticException($"Used undeclared label: {usedUndeclaredLabels.First().Value}");


        static void GetLabelsInfo(Node root, List<Token> declaredLabels, List<Token> usedLabels)
        {
            if (root is OperatorNode operatorNode)
            {
                if (operatorNode.Operator == "Label")
                {
                    var declaredLabel = ((ValueNode)operatorNode.Children.First()).Token;

                    declaredLabels.Add(declaredLabel);
                }
                else if (operatorNode.Operator == "Go to")
                {
                    var usedLabel = ((ValueNode)operatorNode.Children.First()).Token;

                    usedLabels.Add(usedLabel);
                }
                else
                {
                    foreach (var childNode in operatorNode.Children)
                    {
                        GetLabelsInfo(childNode, declaredLabels, usedLabels);
                    }
                }
            }
            else
            {
                return;
            }
        }
    }


    private static void CheckVariableUsageSemantic(Node root)
    {
        CheckVariableUsageSemantic(root, [], []);


        static void CheckVariableUsageSemantic(Node root, Dictionary<int, IdentifierInfo> identifiersTypes, List<StructInfo> structs)
        {
            if (root is OperatorNode operatorNode)
            {
                string @operator = operatorNode.Operator;

                if (@operator == "Function declaration")
                {
                    Node type = operatorNode.Children.ToArray()[0];
                    Token funcName = ((ValueNode)operatorNode.Children.ToArray()[1]).Token;

                    Node @params = operatorNode.Children.ToArray()[2];

                    IEnumerable<IdentifierInfo> paramsInfo = ((OperatorNode)@params).Children.Select(p =>
                    {
                        var paramsDict = new Dictionary<int, IdentifierInfo>();

                        CheckVariableUsageSemantic(p, paramsDict, structs);

                        return paramsDict.Values.First();
                    });

                    identifiersTypes[funcName.Id] = new FuncInfo { Token = funcName, Type = new TypeInfo(type, structs), argsInfo = paramsInfo.ToList() };

                    var identifiersTypesWithParams = new Dictionary<int, IdentifierInfo>(identifiersTypes);
                    CheckVariableUsageSemantic(@params, identifiersTypesWithParams, structs);

                    Node body = operatorNode.Children.ToArray()[3];

                    CheckVariableUsageSemantic(body, identifiersTypesWithParams, structs);
                }
                else if (@operator == "Function prototype")
                {
                    Node type = operatorNode.Children.ToArray()[0];
                    Token funcName = ((ValueNode)operatorNode.Children.ToArray()[1]).Token;

                    identifiersTypes[funcName.Id] = new IdentifierInfo { Token = funcName, Type = new TypeInfo(type, structs) };

                    Node @params = operatorNode.Children.ToArray()[2];

                    IEnumerable<IdentifierInfo> paramsInfo = ((OperatorNode)@params).Children.Select(p =>
                    {
                        var paramsDict = new Dictionary<int, IdentifierInfo>();

                        CheckVariableUsageSemantic(p, paramsDict, structs);

                        return paramsDict.Values.First();
                    });

                    identifiersTypes[funcName.Id] = new FuncInfo { Token = funcName, Type = new TypeInfo(type, structs), argsInfo = paramsInfo.ToList() };
                }
                else if (@operator == "Struct declaration")
                {
                    Token token = ((ValueNode)operatorNode.Children.ToArray()[0]).Token;

                    try
                    {
                        var structFields = ((OperatorNode)operatorNode.Children.ToArray()[1]).Children.Select(d => ((OperatorNode)d).Children);

                        var fieldTypes = structFields
                            .Select(sf =>
                            {
                                var typeNode = sf.ToArray()[0];
                                var nameNode = sf.ToArray()[1];

                                var arrayDeclarations = sf.Count() - 2;

                                for (int i = 0; i < arrayDeclarations; i++)
                                {
                                    typeNode = typeNode switch
                                    {
                                        ValueNode valueNode => new TypesNode([valueNode.Token, new Token(-2, TokenType.Punctuator, "*")]),
                                        TypesNode typesNode => new TypesNode([.. typesNode.Types, new Token(-2, TokenType.Punctuator, "*")]),

                                        _ => throw new Exception("???")
                                    };
                                }

                                string name = ((ValueNode)nameNode).Token.Value;
                                TypeInfo typeInfo = new TypeInfo(typeNode, structs);

                                return KeyValuePair.Create(name, typeInfo);
                            })
                            .ToDictionary();

                        var structInfo = new StructInfo() { Pointed = fieldTypes, Name = token.Value };
                        structs.Add(structInfo);
                    }
                    catch (Exception e)
                    {
                        throw new SemanticException("Unknown struct.", e);
                    }
                }
                else if (@operator == "Variable initialization")
                {
                    var initValue = operatorNode.Children.ToArray()[1];
                    //CheckVariableUsageSemantic(initValue, identifiersTypes, structs); 

                    var initValueType = GetRValueType(initValue, identifiersTypes, structs);

                    var declarationNode = (OperatorNode)operatorNode.Children.ToArray()[0];

                    Node type = declarationNode.Children.ToArray()[0];
                    Token token = ((ValueNode)declarationNode.Children.ToArray()[1]).Token;

                    var varType = new TypeInfo(type, structs);

                    TypeInfo.CanImplicitCasted(initValueType, varType);

                    identifiersTypes[token.Id] = new IdentifierInfo { Token = token, Type = varType, Value = initValue };
                }
                else if (@operator == "Variable declaration")
                {
                    Node type = operatorNode.Children.ToArray()[0];
                    Token token = ((ValueNode)operatorNode.Children.ToArray()[1]).Token;

                    if (type is ValueNode { Token.Value: "const" or "auto" } typeValueNode)
                    {
                        throw new SemanticException($"Variable of {typeValueNode.Token.Value} not initialized.");
                    }
                    else if (type is TypesNode tokenNode)
                    {
                        var typeInfo = new TypeInfo(type, structs);

                        if (typeInfo.TypesParts.Contains("const") || typeInfo.TypesParts.Contains("*const"))
                            throw new SemanticException($"Const variable not initialized.");
                    }

                    identifiersTypes[token.Id] = new IdentifierInfo { Token = token, Type = new TypeInfo(type, structs) };
                }
                else if (@operator is "If else if statements" or "If else if else statements")
                {
                    foreach (Node childNode in operatorNode.Children)
                    {
                        CheckVariableUsageSemantic(childNode, identifiersTypes, structs);
                    }
                }
                else if (@operator is "If statement" or "Else if statement")
                {
                    CheckVariableUsageSemantic(operatorNode.Children.ToArray()[0], new Dictionary<int, IdentifierInfo>(identifiersTypes), structs);
                    CheckVariableUsageSemantic(operatorNode.Children.ToArray()[1], new Dictionary<int, IdentifierInfo>(identifiersTypes), structs);
                }
                else if (@operator == "Else statement")
                {
                    CheckVariableUsageSemantic(operatorNode.Children.ToArray()[0], new Dictionary<int, IdentifierInfo>(identifiersTypes), structs);
                }
                else if (@operator == "For loop")
                {
                    Node iteratorInitializationNode = operatorNode.Children.ToArray()[0];
                    Node conditionNode = operatorNode.Children.ToArray()[1];
                    Node iteratorIncrementNode = operatorNode.Children.ToArray()[2];
                    Node blockNode = operatorNode.Children.ToArray()[3];

                    var identifiersTypesWithIterator = new Dictionary<int, IdentifierInfo>(identifiersTypes);
                    CheckVariableUsageSemantic(iteratorInitializationNode, identifiersTypesWithIterator, structs);
                    CheckVariableUsageSemantic(conditionNode, identifiersTypesWithIterator, structs);
                    CheckVariableUsageSemantic(iteratorIncrementNode, identifiersTypesWithIterator, structs);
                    CheckVariableUsageSemantic(blockNode, identifiersTypesWithIterator, structs);
                }
                else if (@operator is "While loop" or "Do while loop")
                {
                    CheckVariableUsageSemantic(operatorNode.Children.ToArray()[0], new Dictionary<int, IdentifierInfo>(identifiersTypes), structs);
                    CheckVariableUsageSemantic(operatorNode.Children.ToArray()[1], new Dictionary<int, IdentifierInfo>(identifiersTypes), structs);
                }
                else if (new[] { "=", "+=", "-=", "*=", "/=", "%=", ">>=", "<<=", "|=", "&=", "^=" }.Contains(@operator))
                {
                    Node var = operatorNode.Children.ToArray()[0];
                    Node expr = operatorNode.Children.ToArray()[1];

                    var token = ((ValueNode)var).Token;

                    if (!identifiersTypes.TryGetValue(token.Id, out IdentifierInfo? info))
                    {
                        throw new SemanticException($"Variable {token.Value} is no defined.");
                    }

                    var type1 = GetRValueType(var, identifiersTypes, structs);
                    var type2 = GetRValueType(expr, identifiersTypes, structs);

                    TypeInfo.CanImplicitCasted(type2, type1);

                    var identifiersTypesWithIterator = new Dictionary<int, IdentifierInfo>(identifiersTypes);
                    CheckVariableUsageSemantic(var, identifiersTypesWithIterator, structs);
                    CheckVariableUsageSemantic(expr, identifiersTypesWithIterator, structs);
                }
                else
                {
                    try
                    {
                        _ = GetRValueType(operatorNode, identifiersTypes, structs);
                    }
                    catch (SemanticException ex) when (ex.Message == "Invalid semantic")
                    { }

                    foreach (var childNode in operatorNode.Children)
                    {
                        CheckVariableUsageSemantic(childNode, identifiersTypes, structs);
                    }
                }
            }
            else if (root is ValueNode valueNode)
            {
                if (valueNode.Token.TokenType == TokenType.Identifier)
                {
                    int id = valueNode.Token.Id;

                    if (!identifiersTypes.ContainsKey(id))
                        throw new SemanticException($"Indentifier {valueNode.Token.Value} used without declaration.");
                }
            }
            else
            {
                return;
            }
        }

        static TypeInfo GetLValueType(Node root, Dictionary<int, IdentifierInfo> identifierInfo, List<StructInfo> structs)
        {
            //if (root is OperatorNode { Operator: "(..)" } brecketsNode)
            //{
            //    var inner = brecketsNode.Children.ToArray()[0];
            //    TypeInfo innerTypeInfo = GetRValueType(inner, identifierInfo, structs);

            //    return innerTypeInfo;
            //}
            //else 
            if (root is OperatorNode { Operator: "Indexer [..]" } indexNode)
            { 
                var left = indexNode.Children.ToArray()[0];
                var right = indexNode.Children.ToArray()[1];

                TypeInfo leftTypeInfo = GetLValueType(left, identifierInfo, structs);
                TypeInfo rightTypeInfo = GetRValueType(right, identifierInfo, structs);

                if (leftTypeInfo.IfIndexed is null)
                    throw new SemanticException($"Not indexed type {string.Join(' ', leftTypeInfo.TypesParts)}.");

                if (!rightTypeInfo.IsInteger)
                    throw new SemanticException("Not integer indexer argument.");

                return leftTypeInfo.IfIndexed;
            }
            else if (root is OperatorNode { Operator: "Member access ." } pointOperator)
            {
                var left = pointOperator.Children.ToArray()[0];
                var right = pointOperator.Children.ToArray()[1];

                var pointArg = ((ValueNode)right).Token.Value;

                TypeInfo leftTypeInfo = GetRValueType(left, identifierInfo, structs);

                if (leftTypeInfo.IfPointed is null)
                    throw new SemanticException($"Not pointable type: {string.Join(' ', leftTypeInfo.TypesParts)}.");

                if (leftTypeInfo.IfPointed.TryGetValue(pointArg, out var pointed))
                    return pointed;

                throw new SemanticException($"Invalid point member accessing: {pointArg} is infalid field .");
            }
            else if (root is OperatorNode { Operator: "Member access ->" } arrowOperator)
            {
                var left = arrowOperator.Children.ToArray()[0];
                var right = arrowOperator.Children.ToArray()[1];

                var arrowArg = ((ValueNode)right).Token.Value;

                TypeInfo leftTypeInfo = GetRValueType(left, identifierInfo, structs);

                if (leftTypeInfo.IfArrowed is null)
                    throw new SemanticException($"Not arrowble type {string.Join(' ', leftTypeInfo.TypesParts)}.");

                if (leftTypeInfo.IfArrowed.TryGetValue(arrowArg, out var arrowed))
                    return arrowed;

                throw new SemanticException($"Invalid point member accessing: {arrowArg} is infalid field .");
            }
            else if (root is ValueNode valueNode && valueNode.Token.TokenType == TokenType.Identifier)
            {
                if (identifierInfo.TryGetValue(valueNode.Token.Id, out IdentifierInfo? value))
                {
                    return new TypeInfo(value.Type);
                }
                else
                {
                    throw new SemanticException($"Undefined variable {valueNode.Token.Value}");
                }
            }
            else if (root is OperatorNode { Operator: "Address-of &" } addressOfOperator)
            {
                var operand = addressOfOperator.Children.ToArray()[0];

                var typeInfo = GetLValueType(operand, identifierInfo, structs);

                return new TypeInfo([.. typeInfo.TypesParts, "*"], structs);
            }
            else
            {
                throw new SemanticException("Invalid semantic");
            }
        }

        static TypeInfo GetRValueType(Node root, Dictionary<int, IdentifierInfo> identifierInfo, List<StructInfo> structs)
        {
            if (root is ValueNode valueNode && valueNode.Token.TokenType is LiteralType literal)
            {
                if (literal.ToString().ToLower().Contains("string"))
                    return new TypeInfo(["*", "char"], structs);

                List<string> list = ["*", "const", "char", "long", "int", "short", "double", "float", "auto"];

                return new TypeInfo(literal.ToString().ToLower().Split().Where(l => list.Contains(l)).ToList(), structs);
            }
            else if (root is OperatorNode { Operator: "Function calling" } funcOperator)
            {
                var funcId = ((ValueNode)funcOperator.Children.ToArray()[0]).Token.Id;

                if (!identifierInfo.TryGetValue(funcId, out IdentifierInfo? info))
                    throw new SemanticException("Func is not declared.");

                if (info is not FuncInfo funcInfo)
                    throw new SemanticException("Function replaced by variable.");

                IdentifierInfo[] declaredArgsInfo = funcInfo.argsInfo.ToArray();


                IEnumerable<Node> funcArgs = funcOperator.Children.ToArray()[1..];

                TypeInfo[] funcArgsTypes = funcArgs.Select(fa =>
                {
                    return GetRValueType(fa, new(identifierInfo), structs);
                })
                .ToArray();

                if (declaredArgsInfo.Length < funcArgsTypes.Length)
                    throw new SemanticException("Given extra function args.");

                for (int i = 0; i < declaredArgsInfo.Length; i++)
                {
                    if (declaredArgsInfo[i].Value is not null
                        && (funcArgsTypes.Length >= i
                            || TypeInfo.IsImplicitCasted(funcArgsTypes[i], declaredArgsInfo[i].Type)))
                    {
                        throw new SemanticException($"Invalid function argument: {declaredArgsInfo[i].Token.Value}");
                    }
                }

                return funcInfo.Type;
            }
            else if (root is UnaryOperatorNode { Operator: "Indirection *" } starNode)
            {
                var operand = starNode.Children.ToArray()[0];

                var typeInfo = GetRValueType(operand, identifierInfo, structs);

                if (typeInfo.IfIndexed is null)
                    throw new SemanticException($"Invalid type of star operator: {string.Join(' ', typeInfo.TypesParts)}.");

                if (!typeInfo.TypesParts.Remove("*"))
                    typeInfo.TypesParts.Remove("*const");

                return typeInfo;
            }
            else if (root is OperatorNode operatorNode && new string[] { "+", "-", "*", "/", "%" }.Contains(operatorNode.Operator))
            {
                var left = operatorNode.Children.ToArray()[0];
                var right = operatorNode.Children.ToArray()[1];

                var leftInfo = GetRValueType(left, identifierInfo, structs);
                var rightInfo = GetRValueType(right, identifierInfo, structs);

                if (leftInfo.IfPointed is not null || rightInfo.IfPointed is not null)
                {
                    throw new SemanticException($"Struct operand of {operatorNode.Operator}.");
                }
                if (leftInfo.IfIndexed is not null && rightInfo.IfIndexed is not null)
                {
                    if (operatorNode.Operator == "+")
                    {
                        if (TypeInfo.IsImplicitCasted(leftInfo, rightInfo))
                        {
                            return rightInfo;
                        }
                        else if (TypeInfo.IsImplicitCasted(rightInfo, leftInfo))
                        {
                            return leftInfo;
                        }
                    }
                    
                    throw new SemanticException($"Invalid type of {operatorNode.Operator} operand.");    
                }
                else if (TypeInfo.IsImplicitCasted(leftInfo, rightInfo))
                {
                    return rightInfo;
                }
                else if (TypeInfo.IsImplicitCasted(rightInfo, leftInfo))
                {
                    return leftInfo;
                }
                else if (operatorNode.Operator is "+" or "-"
                        && (leftInfo.IfIndexed is not null && rightInfo.IsInteger 
                            || rightInfo.IfIndexed is not null && leftInfo.IsInteger))
                {
                    return leftInfo.IfIndexed is not null
                        ? leftInfo
                        : rightInfo;
                }
                else
                {
                    throw new SemanticException($"Invalid type of {operatorNode.Operator} operand.");
                }
            }
            else if (root is OperatorNode oNode && new string[] { "==", "!=", ">", "<", ">=", "<=" }
                .Contains(oNode.Operator))
            {
                var left = oNode.Children.ToArray()[0];
                var right = oNode.Children.ToArray()[1];

                var leftInfo = GetRValueType(left, identifierInfo, structs);
                var rightInfo = GetRValueType(right, identifierInfo, structs);

                if (leftInfo.IfPointed is not null || rightInfo.IfPointed is not null)
                {
                    throw new SemanticException($"Struct operand of {oNode.Operator}.");
                }
                else if (!TypeInfo.IsImplicitCasted(leftInfo, rightInfo) && !TypeInfo.IsImplicitCasted(rightInfo, leftInfo))
                {
                    throw new SemanticException($"Invalid operator args types.");
                }
                else
                {
                    return new TypeInfo(["char"], structs);
                }
            }
            else if (root is OperatorNode binNode && new string[] { "<<", ">>", "|", "||", "&&", "&", "^" }
                .Contains(binNode.Operator))
            {
                var left = binNode.Children.ToArray()[0];
                var right = binNode.Children.ToArray()[1];

                var leftInfo = GetRValueType(left, identifierInfo, structs);
                var rightInfo = GetRValueType(right, identifierInfo, structs);

                if (leftInfo.IfPointed is not null || rightInfo.IfPointed is not null)
                {
                    throw new SemanticException($"Struct operand of {binNode.Operator}.");
                }
                else if (!leftInfo.IsInteger || !rightInfo.IsInteger)
                {
                    throw new SemanticException($"Not integer argument of binary operator.");
                }
                else if (TypeInfo.IsImplicitCasted(leftInfo, rightInfo))
                {
                    return rightInfo;
                }
                else if (TypeInfo.IsImplicitCasted(rightInfo, leftInfo))
                {
                    return leftInfo;
                }
                else
                {
                    throw new SemanticException($"Invalid type of {binNode.Operator} operand.");
                }
            }
            else if (root is OperatorNode unaryNode && new string[] { "Preincrement ++", "Postincrement ++", "Predecrement --", "Postdecrement --" }
                .Contains(unaryNode.Operator))
            {
                var operand = unaryNode.Children.ToArray()[0];

                var typeInfo = GetRValueType(operand, identifierInfo, structs);

                if (typeInfo.TypesParts.Contains("struct"))
                    throw new SemanticException($"Struct with {unaryNode.Operator.ToLower()}.");

                return typeInfo;
            }
            else if (root is OperatorNode { Operator: "(..)" } bracketsNode)
            {
                var operand = bracketsNode.Children.ToArray()[0];

                return GetRValueType(operand, identifierInfo, structs);
            }
            else if (root is OperatorNode { Operator: "Type cast" } typeCastNode)
            {
                var type = typeCastNode.Children.ToArray()[0];
                var expr = typeCastNode.Children.ToArray()[1];

                var typeInfo1 = new TypeInfo(type, structs);
                var typeInfo2 = GetRValueType(expr, identifierInfo, structs);

                if (typeInfo2.TypesParts.Contains("string"))
                    throw new SemanticException("Invalid cast to string.");

                return typeInfo1;
            }


            //else if (root is OperatorNode operatorNode)
            //{
            //    var left = operatorNode.Children.ToArray()[0];

            //    return identifierInfo[((ValueNode)left).Token.Id].Type;
            //}




            else
            {
                return GetLValueType(root, identifierInfo, structs);
            }
        }
    }

    public class IdentifierInfo
    {
        public Token Token { get; set; }
        public TypeInfo Type { get; set; }
        public Node? Value { get; set; }
    }

    public class FuncInfo : IdentifierInfo
    {
        public List<IdentifierInfo> argsInfo { get; set; }
    }

    public class TypeInfo
    {
        public List<string> TypesParts;

        public bool IsReadOnly
        { 
            get
            {
                return TypesParts[0] is "const" or "*const";
            }
        }

        public bool IsInteger
        {
            get
            {
                List<string> intTypes = ["int", "short", "char"];

                for (int i = 0; i < intTypes.Count; i++)
                {
                    if (TypesParts.Contains(intTypes[i]))                   
                        return true;             
                }

                if (TypesParts.Contains("long") && !TypesParts.Contains("double"))
                    return true;

                return false;
            }
        }



        private Dictionary<string, TypeInfo>? _ifPointed;
        public Dictionary<string, TypeInfo>? IfPointed
        {
            get
            {
                if (IfIndexed is null) 
                    return _ifPointed;

                return null;
            }
        }

        public Dictionary<string, TypeInfo>? IfArrowed
        { 
            get
            {
                return IfIndexed?.IfPointed;
            }
        }

        public TypeInfo? IfIndexed
        {
            get
            {
                if (TypesParts.Contains("*") || TypesParts.Contains("*const"))
                {
                    List<string> parts = TypesParts.ToList();

                    if (!parts.Remove("*"))
                        parts.Remove("*const");

                    return new TypeInfo(parts, _structs);
                }
                else
                {
                    return null;
                }
            }
        }

        private List<StructInfo> _structs;



        public static void CanImplicitCasted(TypeInfo type1, TypeInfo type2)
        {
            int starsCount1 = type1.TypesParts.Count(t => t is "*" or "*const");
            int starsCount2 = type2.TypesParts.Count(t => t is "*" or "*count");

            if (starsCount1 != 0 || starsCount2 != 0)
            {
                if (starsCount1 != starsCount2)
                    throw new SemanticException("Invalid pointer casting.");
            }
            else if (type1.TypesParts.Contains("struct"))
            {
                string struct1Name = type1.TypesParts[type1.TypesParts.IndexOf("struct") + 1];

                if (!type2.TypesParts.Contains("struct"))
                    throw new SemanticException("Invalid struct casting.");

                string struct2Name = type2.TypesParts[type2.TypesParts.IndexOf("struct") + 1];

                if (struct1Name != struct2Name)
                    throw new SemanticException("Invalid struct casting.");

            }
            else if (type1.TypesParts.Contains("float"))
            {
                if (!type2.TypesParts.Contains("float") && !type2.TypesParts.Contains("double"))
                    throw new SemanticException("Invalid float casting.");
            }
            else if (type1.TypesParts.Contains("double"))
            {
                if (type1.TypesParts.Contains("long"))
                {
                    if (!type2.TypesParts.Contains("double") || !type2.TypesParts.Contains("long"))
                        throw new SemanticException("Invalid double casting.");
                }
                else
                {
                    if (!type2.TypesParts.Contains("double"))
                        throw new SemanticException("Invalid double casting.");
                }
            }
            else
            {
                var weights = new List<TypeInfo>() { type1, type2 }.Select(typeInfo =>
                {
                    int res = 0;

                    var typeParts = typeInfo.TypesParts;

                    Dictionary<string, int> weightsDict = new()
                    {
                        ["char"] = 1,
                        ["short"] = 2,
                        ["long"] = 8,
                        ["float"] = 2,
                        ["int"] = 4,
                        ["double"] = 4,
                    };

                    foreach (var (type, weight) in weightsDict)
                    {
                        if (typeParts.Contains(type))
                        {
                            res = weight;
                            break;
                        }
                    }

                    if (typeInfo.TypesParts.Contains("unsigned"))
                        res *= 2;

                    return res;
                })
                .ToArray();

                int weight1 = weights[0];
                int weight2 = weights[1];

                if (weight1 > weight2)
                    throw new SemanticException("Invalid numeric cast.");
            }
        }

        public static bool IsImplicitCasted(TypeInfo type1, TypeInfo type2)
        {
            try
            {
                CanImplicitCasted(type1, type2);
                return true;
            }
            catch (SemanticException)
            {
                return false;
            }
        }


        public TypeInfo(Node typeNode, List<StructInfo> structs)
        {
            this.TypesParts = [];
            _structs = structs;

            if (typeNode is ValueNode valueNode)
            {
                AnaliseType([valueNode.Token.Value], structs);
            }
            else if (typeNode is TypesNode typesPartsNode)
            {
                List<string> typeParts = typesPartsNode.Types.Select(token => token.Value).ToList();

                for (int i = 0; i + 1 < typeParts.Count; i++)
                {
                    if (typeParts[i] == "*" && typeParts[i + 1] == "const")
                    {
                        typeParts.RemoveAt(i);
                        typeParts[i] = "*const";
                    }
                }

                typeParts.Sort((t1, t2) =>
                {
                    Func<string, int> getIndex = (str) => str switch
                    {
                        "*" => 3,
                        "*const" => 3,
                        "const" => 2,
                        "signed" => 1,
                        "unsigned" => 1,
                        _ => 0
                    };

                    return Comparer<int>.Default.Compare(getIndex(t1), getIndex(t2));
                });

                AnaliseType(typeParts, structs);
            }
            else
            {
                throw new Exception("???");
            }
        }

        public TypeInfo(List<string> typesParts, List<StructInfo> structs)
        {
            this.TypesParts = [];
            _structs = structs;

            AnaliseType(typesParts, structs);
        }

        public TypeInfo(TypeInfo other)
        {
            this.TypesParts = other.TypesParts.ToList();
            _structs = other._structs;

            if (other.IfPointed is not null)
                _ifPointed = new(other.IfPointed);
        }



        private void AnaliseType(List<string> typeParts, List<StructInfo> structs)
        {
            if (typeParts.Contains("auto"))
            {
                throw new NotImplementedException("Auto is not implemented");

                if (typeParts.Count > 1)
                    throw new SemanticException($"Auto with invalid type modifier: {typeParts.First(t => t != "auto")}.");
            }
            else if (typeParts.Contains("*") || typeParts.Contains("*const"))
            { 
                var newTypesParts = new List<string>(typeParts);

                newTypesParts.RemoveAll(s => s == "*");
                newTypesParts.RemoveAll(s => s == "*const");

                AnaliseType(newTypesParts, structs);

                //_ifPointed = null;
            }
            else if (typeParts.Contains("struct"))
            {
                if (typeParts.Count(t => t == "struct") > 1)
                    throw new SemanticException("Duplicate struct keyword.");

                int index = typeParts.IndexOf("struct");

                if (index + 1 >= typeParts.Count)
                    throw new SemanticException("Invalis semantic.");

                string structName = typeParts[index + 1];

                foreach (string invalidType in new string[] { "char", "double", "float", "int", "long", "short", "signed", "unsigned", "void" })
                    if (typeParts.Contains(invalidType))
                        throw new SemanticException("Invalid struct type modifier.");

                ///
                if (!structs.Select(s => s.Name).Contains(structName))
                    throw new SemanticException("Unknown struct.");

                _ifPointed = structs.First(s => s.Name == structName).Pointed;
            }
            else if (typeParts.Contains("void"))
            {
                if (typeParts.Count(t => t == "void") > 1)
                    throw new SemanticException("Duplicated void");

                foreach (string invalidType in new string[] { "char", "double", "float", "int", "long", "short", "signed", "unsigned", "const" })
                    if (typeParts.Contains(invalidType))
                        throw new SemanticException("Invalid void type modifier.");
            }
            else if (typeParts.Contains("float"))
            {
                if (typeParts.Count(t => t == "float") > 1)
                    throw new SemanticException("Duplicated float");

                foreach (string invalidType in new string[] { "char", "double", "int", "long", "short", "signed", "unsigned" })
                    if (typeParts.Contains(invalidType))
                        throw new SemanticException("Invalid float type modifier.");
            }
            else if (typeParts.Contains("double"))
            {
                if (typeParts.Count(t => t == "double") > 1)
                    throw new SemanticException("Duplicated float");

                foreach (string invalidType in new string[] { "char", "int", "short", "signed", "unsigned" })
                    if (typeParts.Contains(invalidType))
                        throw new SemanticException("Invalid double type modifier.");

                if (typeParts.Count(t => t == "long") > 1)
                    throw new SemanticException("Very long double.");
            }
            else if (typeParts.Contains("char"))
            {
                if (typeParts.Count(t => t == "char") > 1)
                    throw new SemanticException("Duplicated char");

                foreach (string invalidType in new string[] { "int", "short", "long" })
                    if (typeParts.Contains(invalidType))
                        throw new SemanticException("Invalid char type modifier.");

                if (typeParts.Contains("signed"))
                {
                    if (typeParts.Count(t => t == "signed") > 1)
                        throw new SemanticException("Duplicated signed.");

                    if (typeParts.Count(t => t == "unsigned") > 0)
                        throw new SemanticException("Signed or unsigned?");
                }

                if (typeParts.Count(t => t == "unsigned") > 1)
                    throw new SemanticException("Duplicated unsigned.");

                if (typeParts.Count(t => t == "unsigned") == 1)
                    typeParts.Remove("unsigned");
            }
            else if (typeParts.Contains("short"))
            {
                if (typeParts.Count(t => t == "short") > 1)
                    throw new SemanticException("Duplicated short.");

                if (typeParts.Contains("signed"))
                {
                    if (typeParts.Count(t => t == "signed") > 1)
                        throw new SemanticException("Duplicated signed.");

                    if (typeParts.Count(t => t == "unsigned") > 0)
                        throw new SemanticException("Signed or unsigned?");

                    typeParts.Remove("signed");
                }

                if (typeParts.Count(t => t == "unsigned") > 1)
                    throw new SemanticException("Duplicated unsigned.");

                if (typeParts.Count(t => t == "int") > 1)
                    throw new SemanticException("Duplicated int.");
            }
            else if (typeParts.Contains("long"))
            {
                if (typeParts.Count(t => t == "long") > 2)
                    throw new SemanticException("So long.");

                if (typeParts.Contains("signed"))
                {
                    if (typeParts.Count(t => t == "signed") > 1)
                        throw new SemanticException("Duplicated signed.");

                    if (typeParts.Count(t => t == "unsigned") > 0)
                        throw new SemanticException("Signed or unsigned?");

                    typeParts.Remove("signed");
                }

                if (typeParts.Count(t => t == "unsigned") > 1)
                    throw new SemanticException("Duplicated unsigned.");

                if (typeParts.Count(t => t == "int") == 0)
                {
                    typeParts[typeParts.IndexOf("long")] = "int";
                }
                else if (typeParts.Count(t => t == "int") == 1)
                {
                    if (typeParts.Count(t => t == "long") == 2)
                        typeParts.Remove("long");
                }
                else
                {
                    throw new SemanticException("So many ints.");
                }
            }
            else if (typeParts.Contains("int"))
            {
                if (typeParts.Count(t => t == "int") > 1)
                    throw new SemanticException("So many ints.");

                if (typeParts.Contains("signed"))
                {
                    if (typeParts.Count(t => t == "signed") > 1)
                        throw new SemanticException("Duplicated signed.");

                    if (typeParts.Count(t => t == "unsigned") > 0)
                        throw new SemanticException("Signed or unsigned?");

                    typeParts.Remove("signed");
                }

                if (typeParts.Count(t => t == "unsigned") > 1)
                    throw new SemanticException("Duplicated unsigned.");
            }
            else if (typeParts.Contains("const"))
            {
                int constCount = typeParts.Count(t => t == "const");

                for (int i = 0; i < constCount - 1; i++)
                {
                    typeParts.Remove("const");
                }

                bool constOnly = true;

                foreach (var t in new List<string> { "char", "double", "int", "long", "short", "signed", "unsigned" })
                {
                    if (typeParts.Contains(t))
                    {
                        constOnly = false;
                        break;
                    }
                }

                if (constOnly)
                {
                    typeParts.Add("int");
                }
                else
                {
                    throw new NotImplementedException("Const not implemented");
                }
            }
            else
            {
                throw new SemanticException("Invalid type.");
            }

            TypesParts = typeParts;
        }
    }


    public class StructInfo
    {
        public string Name { get; set; }
        public Dictionary<string, TypeInfo>? Pointed { get; set; }
    }

    public class ScopeInfo
    {
        public ScopeInfo? ParentScope { get; set; } = null;

        public List<StructInfo> Structs { get; set; } = [];

        public Dictionary<int, IdentifierInfo> Identifiers { get; set; } = [];


        public List<StructInfo> AncestorsStructs
        {
            get
            {
                List<StructInfo> ans = [];

                for (var scope = this; scope.ParentScope is not null; scope = scope.ParentScope)
                {
                    ans.AddRange(scope.Structs);
                }

                return ans;
            }
        }

        public Dictionary<int, IdentifierInfo> AncestorsIdentifiers
        {
            get
            {
                Dictionary<int, IdentifierInfo> res = [];

                for (var scope = this; scope.ParentScope is not null; scope = scope.ParentScope)
                {
                    scope.Identifiers.ToList().ForEach(x =>
                    {
                        try
                        {
                            res.Add(x.Key, x.Value);
                        }
                        catch (ArgumentException)
                        { }
                    });
                }

                return res;
            }
        }
    }


    private static void Alert(object message)
    {
        MessageBox.Show(
            message.ToString(),
            "Сообщение",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.DefaultDesktopOnly);
    }

    private static bool IsNotThrow<TException>(Action action) where TException : Exception
    {
        try
        {
            action.Invoke();
            return true;
        }
        catch (TException)
        {
            return false;
        }
    }

    private static bool IsNotThrow(Action action)
    {
        return IsNotThrow<Exception>(action);
    }
}



