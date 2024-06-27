using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Collections;
using static Trans.Parser;
using System.Drawing.Drawing2D;

namespace Trans;


public static partial class Parser
{
    static List<dynamic> objects = []; 


    public unsafe static void Interpret(Node root, StringWriter console)
    {
        GCLatencyMode oldMode = GCSettings.LatencyMode;

        RuntimeHelpers.PrepareConstrainedRegions();

        dynamic? main = null;

        try
        {
            GCSettings.LatencyMode = GCLatencyMode.LowLatency;

            Scope mainScope = new Scope();
            object @return = new object();
            
            Interpret(root, console, mainScope, ref @return);

            if (main is null)
            {
                throw new InterpretationException("No main.");
            }
            else
            {
                main(Array.Empty<object>());
            }
        }
        catch (Exception e)
        {
            throw new InterpretationException(e.Message, e);
        }
        finally
        {
            GCSettings.LatencyMode = oldMode;
        }


        void Interpret(Node root, StringWriter console, Scope scope, ref object? @return)
        {
            if (root is OperatorNode operatorNode)
            {
                string @operator = operatorNode.Operator;


                if (false)
                {

                }
                if (@operator == "Function declaration")
                {
                    Node type = operatorNode.Children.ToArray()[0];
                    Token funcToken = ((ValueNode)operatorNode.Children.ToArray()[1]).Token;

                    IEnumerable<Node> @params = ((OperatorNode)operatorNode.Children.ToArray()[2]).Children;

                    Dictionary<int, int> paramsIndexes = @params.Select((p, index) =>
                    {
                        int varId = ((ValueNode)((OperatorNode)p).Children.ToArray()[1]).Token.Id;

                        return KeyValuePair.Create(index, varId);
                    })
                    .ToDictionary();


                    Node body = operatorNode.Children.ToArray()[3];


                    var allParamsScope = new Scope(parentScope: scope);


                    foreach (Node param in @params)
                    {
                        Interpret(param, console, allParamsScope, ref @return);
                    }

                    Delegate func = (params dynamic[] values) =>
                    {
                        object? @return = new object();

                        for (int i = 0; i < values.Length; i++)
                        {
                            scope.Variables[paramsIndexes[i]] = values[i];
                        }

                        Interpret(body, console, scope, ref @return);

                        return @return;
                    };

                    dynamic funcValue = new FunctionValue(func);

                    scope.Variables[funcToken.Id] = funcValue;

                    if (funcToken.Value == "main")
                        main = funcValue;
                }
                else if (@operator == "Function prototype")
                { }
                else if (@operator == "Struct declaration")
                { }
                else if (@operator == "Variable declaration")
                {
                    Node typeNode = operatorNode.Children.ToArray()[0];
                    Token token = ((ValueNode)operatorNode.Children.ToArray()[1]).Token;

                    TypeInfo typeInfo;

                    if (typeNode is ValueNode valueNode)
                    {
                        typeInfo = new TypeInfo(typesParts: [valueNode.Token.Value], structs: []);
                    }
                    else if (typeNode is TypesNode complexTypeNode)
                    {
                        typeInfo = complexTypeNode.TypeInfo;
                    }
                    else
                    {
                        throw new UnexpectedException();
                    }

                    List<int> arrayDeclarations = operatorNode.Children.ToArray()[2..].Select(a =>
                    {
                        if (a is OperatorNode o && o.Operator.StartsWith("Array"))
                        {
                            return o.Children.Any()
                                ? int.Parse(((ValueNode)o.Children.First()).Token.Value)
                                : 5;
                        }
                        else
                        {
                            throw new UnexpectedException();
                        }
                    })
                    .ToList();

                    dynamic variable = CreateVariable(typeInfo, arrays: arrayDeclarations);
                    int varialeId = token.Id;

                    scope.Variables[varialeId] = variable;
                }
                else if (@operator == "Variable initialization")
                {
                    var initValueNode = operatorNode.Children.ToArray()[1];
                    var declarationNode = (OperatorNode)operatorNode.Children.ToArray()[0];
                    Token token = ((ValueNode)declarationNode.Children.ToArray()[1]).Token;

                    dynamic initValue;

                    Interpret(declarationNode, console, scope, ref @return);

                    if ((initValueNode as OperatorNode)?.Operator == "{..}")
                    {
                        var children = ((OperatorNode)((OperatorNode)initValueNode).Children.ToArray()[0]).Children.ToArray();

                        int x = children.Length;
                        var y = ((OperatorNode)((OperatorNode)children.First()).Children.ToArray()[0]).Children.Count();

                        dynamic[][] ar = new dynamic[x][];

                        for (int i = 0; i < x; i++)
                        {
                            ar[i] = new dynamic[y];
                        }

                        for (int i = 0; i < x; i++)
                        {
                            var c = ((OperatorNode)((OperatorNode)children.ToArray()[i]).Children.ToArray()[0]).Children.ToArray();

                            for (int j = 0; j < y; j++)
                            {
                                var s = (c[j] as ValueNode)?.Token?.Value ?? "0";

                                ar[i][j] = GetRValue(c[j], scope)._value;
                            }
                        }

                        initValue = CreateArray(ar);
                    }
                    else
                    {
                        initValue = GetRValue(initValueNode, scope);
                    }
              
                    scope.Variables[token.Id] = initValue;
                }
                else if (@operator is "If else if statements" or "If else if else statements")
                {
                    var statements = operatorNode.Children.ToArray();

                    List<(Node?, Node)> conditionsBlocks = statements.Select(s =>
                    {
                        Node[] conditionWithBlock = ((OperatorNode)s).Children.ToArray();

                        if (conditionWithBlock.Length == 1) // else case
                        {
                            return (null, conditionWithBlock[0])!;
                        }
                        else
                        {
                            return (conditionWithBlock[0], conditionWithBlock[1]);
                        }
                        
                    })
                    .ToList()!;

                    foreach (var (condition, block) in conditionsBlocks)
                    {
                        if (condition is null || GetRValue(condition, scope))
                        {
                            Interpret(block, console, scope, ref @return);
                            break;
                        }
                    }
                }
                else if (@operator == "Switch case")
                {
                    var cases = ((OperatorNode)operatorNode.Children.ToArray().Last()).Children.ToArray();
                    var variable = GetRValue(operatorNode.Children.ToArray()[0], scope);

                    foreach (var @case in cases)
                    {
                        var conditionNode = ((OperatorNode)@case).Children.First();

                        dynamic conditionValue;

                        if (conditionNode is ValueNode { Token.Value: "default" })
                        {
                            conditionValue = variable;
                        }
                        else
                        {
                            conditionValue = GetRValue(((OperatorNode)@case).Children.First(), scope);
                        }

                        var blockNode = ((OperatorNode)@case).Children.Last();

                        if (conditionValue._value == variable._value)
                        {
                            Interpret(blockNode, console, scope, ref @return);
                            break;
                        }
                    }
                }
                else if (@operator == "For loop")
                {
                    Node iteratorInitializationNode = operatorNode.Children.ToArray()[0];
                    Node conditionNode = operatorNode.Children.ToArray()[1];
                    Node iteratorIncrementNode = operatorNode.Children.ToArray()[2];
                    Node blockNode = operatorNode.Children.ToArray()[3];

                    var forScope = new Scope(scope);
                    
                    for (
                        Interpret(iteratorInitializationNode, console, forScope, ref @return); 
                        GetRValue(conditionNode, forScope) == true; 
                        Interpret(iteratorIncrementNode, console, forScope, ref @return))
                    {
                        Interpret(blockNode, console, forScope, ref @return);
                    }
                }
                else if (@operator is "While loop")
                {
                    var cond = operatorNode.Children.ToArray()[0];
                    var block = operatorNode.Children.ToArray()[1];

                    while (GetRValue(cond, scope) == true)
                    {
                        Interpret(block, console, scope, ref @return);
                    }
                }
                else if (@operator == "Return")
                {
                    if (operatorNode.Children.Count() == 0)
                    {
                        @return = null;
                    }
                    else
                    {
                        Node returnValueNode = operatorNode.Children.First();
                        @return = GetRValue(returnValueNode, scope);
                        return;
                    }
                }
                else
                {
                    try
                    {
                        _ = GetRValue(root, scope);
                    }
                    catch
                    {
                        foreach (var childNode in operatorNode.Children)
                        {
                            Interpret(childNode, console, scope, ref @return);
                        }
                    }
                }
            }
        }

    

        dynamic GetLValue(Node root, Scope scope, out Action<dynamic> setter)
        {
            if (root is OperatorNode { Operator: "Indexer [..]" } indexNode)
            {
                var leftNode = indexNode.Children.ToArray()[0];
                var rightNode = indexNode.Children.ToArray()[1];

                dynamic left = GetLValue(leftNode, scope, out var vsetter);
                dynamic right = GetRValue(rightNode, scope);

                setter = value => left[right._value] = value;

                return left[right._value];
            }
            else if (root is OperatorNode { Operator: "Member access ." } pointOperator)
            {
                var left = pointOperator.Children.ToArray()[0];
                var right = pointOperator.Children.ToArray()[1];

                var pointArg = ((ValueNode)right).Token.Value;

                var s = GetLValue(left, scope, out setter);

                setter = value => s.Fields[pointArg] = value;

                return s.Fields[pointArg];
            }
            else if (root is OperatorNode { Operator: "Member access ->" } arrowOperator)
            {
                throw new NotImplementedException();
            }
            else if (root is OperatorNode { Operator: "Address-of &" } addressOfOperator)
            {
                var operand = addressOfOperator.Children.ToArray()[0];
                var var = GetLValue(operand, scope, out setter);

                return var.Addres;
            }
            else if (root is OperatorNode { Operator: "Indirection *" } starNode)
            {
                var operandNode = starNode.Children.ToArray()[0];
                dynamic operand = GetRValue(operandNode, scope);

                setter = value => operand.Dereference = value;

                return operand.Dereference;
            }
            else if (root is ValueNode valueNode && valueNode.Token.TokenType == TokenType.Identifier)
            {
                int varId = valueNode.Token.Id;
                dynamic var = scope.Variables[varId];

                setter = newValue => scope.Variables[varId]._value = newValue;

                return var;
            }
            else
            {
                throw new UnexpectedException();
            }
        }

        dynamic GetRValue(Node root, Scope scope)
        {
            if (root is ValueNode valueNode && valueNode.Token.TokenType is LiteralType)
            {
                if (valueNode.TypeInfo is TypeInfo literalType)
                {
                    string literalValue = valueNode.Token.Value;

                    return CreateVariable(literalType, literalValue, []);
                }
                else
                {
                    return CreateVariable(new TypeInfo(["int"], []), valueNode.Token.Value, []);
                }
            }
            else if (root is OperatorNode { Operator: "Function calling" } funcOperator)
            {
                var funcToken = ((ValueNode)funcOperator.Children.ToArray()[0]).Token;

                if (funcToken.Value == "printf")
                {
                    IEnumerable<dynamic> funcArgs = funcOperator.Children.ToArray()[1..].Select(arg => GetRValue(arg, scope));


                    var d = funcArgs.First();

                    string format = funcArgs.First().ToString();

                    console.WriteLine(format);
                    return null;
                }


                var func = scope.Variables[funcToken.Id];


                Node[] funcArgsNodes = funcOperator.Children.ToArray()[1..];

                dynamic[] args = funcArgsNodes.Select(n => GetRValue(n, scope)).ToArray();

                return func(args);
            }
            else if (root is OperatorNode assignmentNode
                    && new[] { "=", "+=", "-=", "*=", "/=", "%=", ">>=", "<<=", "|=", "&=", "^=" }.Contains(assignmentNode.Operator))
            {
                string @operator = assignmentNode.Operator;

                Node var = assignmentNode.Children.ToArray()[0];
                Node expr = assignmentNode.Children.ToArray()[1];

                var var1 = GetLValue(var, scope, out var varSetter);
                var value = GetRValue(expr, scope);

                dynamic newVal = assignmentNode.Operator switch
                {
                    "=" => value,
                    "+=" => var1 + value,
                    "-=" => var1 - value,
                    "*=" => var1 * value,
                    "/=" => var1 / value,
                    "&=" => var1 % value,

                    _ => throw new NotImplementedException()
                };

                varSetter(newVal);

                if (var is ValueNode v)
                {
                    scope.Variables[v.Token.Id] = newVal;
                }

                return newVal;
            }
            else if (root is OperatorNode opNode && new string[] { "+", "-", "*", "/", "%" }.Contains(opNode.Operator))
            {
                string @operator = opNode.Operator;

                var leftNode = opNode.Children.ToArray()[0];
                var rightNode = opNode.Children.ToArray()[1];

                dynamic left = GetRValue(leftNode, scope);
                dynamic right = GetRValue(rightNode, scope);

                return @operator switch
                {
                    "+" => right + left,
                    "-" => left - right,
                    "*" => left * right,
                    "/" => left / right,
                    "%" => left % right,

                    _ => throw new UnexpectedException()
                };
            }
            else if (root is OperatorNode oNode && new string[] { "==", "!=", ">", "<", ">=", "<=" }
                .Contains(oNode.Operator))
            {
                string @operator = oNode.Operator;

                var leftNode = oNode.Children.ToArray()[0];
                var rightNode = oNode.Children.ToArray()[1];

                dynamic left = GetRValue(leftNode, scope);
                dynamic right = GetRValue(rightNode, scope);

                left = left is Value v ? left._value : left;
                right = right is Value ? right._value : right;

                return @operator switch
                {
                    "==" => left == right,
                    "!=" => left != right,
                    "<" => left < right,
                    ">" => left > right,
                    "<=" => left <= right,
                    ">=" => left >= right,

                    _ => throw new UnexpectedException()
                };
            }
            else if (root is OperatorNode unaryNode && new string[] { "Preincrement ++", "Postincrement ++", "Predecrement --", "Postdecrement --" }
               .Contains(unaryNode.Operator))
            {
                string @operator = unaryNode.Operator;
                var leftNode = unaryNode.Children.ToArray()[0];

                dynamic left = GetLValue(leftNode, scope, out var set);
                left = left is Value v ? left._value : left;

                var e = @operator switch
                {
                    "Preincrement ++" => left++,
                    "Postincrement ++" => left++,
                    "Predecrement --" => left--,
                    "Postdecrement --" => left--,

                    _ => throw new UnexpectedException()
                };

                set(left);


                return e;
            }


            else
            {
                return GetLValue(root, scope, out var setter);
            }
            

            //return 0;
        }

        static unsafe dynamic CreateVariable(TypeInfo typeInfo, string? value = null, List<int>? arrays = null)
        {
            List<string> typeParts = typeInfo.TypesParts;

            if (typeInfo.IfIndexed is not null)
            {
                return new PointerValue(default);
            }
            else if (typeParts.Contains("struct"))
            {
                var fields = typeInfo.IfPointed!.Keys;

                Dictionary<string, dynamic> s = new Dictionary<string, dynamic>();

                foreach (var field in fields)
                {
                    s[field] = default;
                }

                return new StructValue(s);
            }
            else if (typeParts.Contains("void"))
            {
                throw new NotImplementedException();
            }
            else if (typeParts.Contains("float"))
            {
                return CreateInstance<float>(value, arrays);
            }
            else if (typeParts.Contains("double"))
            {
                return CreateInstance<double>(value, arrays);
            }
            if (typeParts.Contains("char"))
            {
                if (typeParts.Contains("signed"))
                    return CreateInstance<sbyte>(value, arrays);

                return CreateInstance<byte>(value,arrays);
            }
            else if (typeParts.Contains("short"))
            {
                if (typeParts.Contains("unsigned"))
                    return CreateInstance<ushort>(value, arrays);

                return CreateInstance<short>(value, arrays);
            }
            else if (typeParts.Contains("long"))
            {
                if (typeParts.Contains("unsigned"))
                    return CreateInstance<ulong>(value, arrays);

                return CreateInstance<long>(value, arrays);
            }
            else if (typeParts.Contains("int"))
            {
                if (typeParts.Contains("unsigned"))
                    return CreateInstance<uint>(value, arrays);

                return CreateInstance<int>(value, arrays);
            }

            throw new NotImplementedException();
        }


        static dynamic CreateArray(dynamic[][] arr)
        {
            int x = arr.Length;
            int y = arr[0].Length;

            dynamic matrix = CreateInstance<int>(null, [x, y]);

            for (int i = 0; i < x; i++)
            {
                for (int j = 0; j < y; j++)
                {
                    matrix[i][j] = arr[i][j];
                }
            }

            return matrix;
        }


        static dynamic CreateInstance<T>(string? value, List<int> arrayLenghts) where T : new()
        {
            dynamic newValue;

            if (arrayLenghts.Any())
            {
                if (arrayLenghts.Count == 1)
                {
                    T[] array = new T[arrayLenghts[0]];

                    fixed (T* ptr = &array[0])
                    {
                        newValue = new PointerValue((object*)ptr);
                    }
                }
                else if (arrayLenghts.Count == 2)
                {
                    PointerValue[] arr = new PointerValue[arrayLenghts[0]];

                    for (int i = 0; i < arrayLenghts[0]; i++)
                    {
                        T[] a = new T[arrayLenghts[1]];

                        fixed (T* ptr = &a[0])
                        {
                            arr[i] = new PointerValue((object*)ptr);
                        }
                    }

                    fixed (PointerValue* p = &arr[0])
                    {
                        newValue = new PointerValue((object*)p);
                    }
                }
                else
                {
                    throw new NotImplementedException();    
                }
            }
            else if (value is null)
            {
                newValue = new Value(new T());
            }
            else
            {
                newValue = new Value((T)Convert.ChangeType(value, typeof(T)));
            }

           // objects.Add(newValue);
            return newValue;
        }
    }

    private class Scope
    {
        private Scope? _parentScope;

        public IDictionary<int, dynamic> Variables { get; }


        public Scope()
        {
            Variables = new InnerDictionary<int, dynamic>();
        }

        public Scope(Scope parentScope)
        {
            _parentScope = parentScope;
            Variables = new InnerDictionary<int, dynamic>(_parentScope.Variables);
        }


        private class InnerDictionary<TKey, TValue> : IDictionary<TKey, TValue>
        {
            private Dictionary<TKey, TValue> _dict;
            private IDictionary<TKey, TValue>? _innerDictionary;


            public InnerDictionary()
            {
                _dict = [];
                _innerDictionary = null;
            }

            public InnerDictionary(IDictionary<TKey, TValue> other)
            {
                _dict = [];
                _innerDictionary = other;   
            }


            public TValue this[TKey key] 
            { 
                get
                {
                    if (_dict.TryGetValue(key, out var value))
                    {
                        return value;
                    }
                    else
                    {
                        return _innerDictionary[key];
                    }
                }
                set
                {
                    _dict[key] = value;
                }
            }

            public ICollection<TKey> Keys => _dict.Keys.Union(_innerDictionary?.Keys ?? []).ToList();

            public ICollection<TValue> Values => Keys.Select(k => this[k]).ToList();

            public int Count => Keys.Count;

            public bool IsReadOnly => false;

            public void Add(TKey key, TValue value)
            {
                _dict.Add(key, value);
            }

            public void Add(KeyValuePair<TKey, TValue> item)
            {
                (_dict as ICollection<KeyValuePair<TKey, TValue>>).Add(item);
            }

            public void Clear()
            {
                _dict?.Clear();
                _innerDictionary.Clear();
            }

            public bool Contains(KeyValuePair<TKey, TValue> item)
            {
                if (!(_dict as ICollection<KeyValuePair<TKey, TValue>>).Contains(item))
                    return _innerDictionary?.Contains(item) ?? false;

                return true;
            }

            public bool ContainsKey(TKey key)
            {
                if (!_dict.ContainsKey(key))
                    return _innerDictionary?.ContainsKey(key) ?? false;

                return true;
            }

            public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            {
                return _dict.Union(_innerDictionary ?? new Dictionary<TKey, TValue>()).GetEnumerator();
            }

            public bool Remove(TKey key)
            {
                throw new NotImplementedException();
            }

            public bool Remove(KeyValuePair<TKey, TValue> item)
            {
                throw new NotImplementedException();
            }

            public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

    }



    private unsafe class Value : DynamicObject
    {
        public dynamic _value;


        public Value(dynamic value)
        {
            _value = value;
        }

        internal Value()
        {
            _value = null;
        }

        public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object? result)
        {
            dynamic left = _value;
            dynamic right = arg switch
            { 
                Value v => v._value,
                PointerValue p => p,
                _ => arg,
            };

            if (right is PointerValue)
                (left, right) = (right, left);

            var expressionType = binder.Operation;

            result = expressionType switch
            {
                ExpressionType.Add => left + right,
                ExpressionType.Subtract => left - right,
                ExpressionType.Multiply => left * right,
                ExpressionType.Divide => left / right,
                ExpressionType.Modulo => left % right,

                ExpressionType.AddAssign => _value += right,
                ExpressionType.SubtractAssign => _value -= right,
                ExpressionType.MultiplyAssign => _value *= right,
                ExpressionType.DivideAssign => _value /= right,
                ExpressionType.ModuloAssign => _value %= right,

                ExpressionType.Equal => left == right,
                ExpressionType.NotEqual => left != right,
                ExpressionType.LessThan => left < right,
                ExpressionType.GreaterThan => left > right,
                ExpressionType.LessThanOrEqual => left <= right,
                ExpressionType.GreaterThanOrEqual => left >= right,

                ExpressionType.AndAlso => left && right,
                ExpressionType.OrElse => left || right,

                ExpressionType.And => left & right,
                ExpressionType.Or => left | right,
                ExpressionType.ExclusiveOr => left ^ right,

                _ => throw new NotImplementedException()
            };

            result = new Value(result);

            return true;
        }

        public override bool TryUnaryOperation(UnaryOperationBinder binder, out object? result)
        {
            dynamic operand = _value is Value v ? v._value : _value;

            var expressionType = binder.Operation;
            var expr = Expression.MakeUnary(expressionType, Expression.Constant(operand), null);

            Delegate binaryFunc = Expression.Lambda(expr).Compile();
            result = new Value(binaryFunc.DynamicInvoke());

            return true;
        }

        public override bool TryConvert(ConvertBinder binder, out object? result)
        {
            var returnType = binder.ReturnType;
            var expr = Expression.Convert(Expression.Constant(_value), returnType);

            Delegate binaryFunc = Expression.Lambda(expr).Compile();
            result = new Value(binaryFunc.DynamicInvoke());

            return true;
        }

        public override bool Equals(object? obj)
        {
            return true;
        }

        public dynamic Addres
        {
            get
            {
                fixed (object* ptr = &_value)
                {
                    return new PointerValue(ptr);
                }
            }
        }

        public override string ToString()
        {
            return _value.ToString();   
        }
    }


    private class StructValue : Value
    {
        public StructValue(Dictionary<string, dynamic> value)
            : base(value) 
        { }


        public IDictionary<string, dynamic> Fields
        {
            get
            {
                return _value;
            }
        }
    }

    private unsafe class PointerValue : DynamicObject
    {
        private object* _ptr;


        public PointerValue(object* ptr)
        {
            _ptr = ptr;
        }


        public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object? result)
        {
            var expressionType = binder.Operation;

            result = expressionType switch
            {
                ExpressionType.Add => new PointerValue(_ptr + (int)arg),
                ExpressionType.Subtract => new PointerValue(_ptr - (int)arg),

                ExpressionType.AddAssign => new PointerValue(_ptr += (int)arg),
                ExpressionType.SubtractAssign => new PointerValue(_ptr -= (int)arg),

                _ => throw new UnexpectedException()
            };

            return true;
        }

        public override bool TryUnaryOperation(UnaryOperationBinder binder, out object? result)
        {
            var expressionType = binder.Operation;

            int addition = expressionType switch
            {
                ExpressionType.Increment => +1,
                ExpressionType.Decrement => -1,

                _ => throw new UnexpectedException()
            };

            _ptr += addition;
            result = this;

            return true;
        }

        public dynamic Dereference
        {
            get => this[0];
            set => this[0] = value;
        }

        public dynamic this[int index]
        {
            get
            {
                try
                {
                    return *(_ptr + index);
                }
                catch (Exception ex)
                {
                    throw new InterpretationException("Invalid index", ex);
                }
            }
            set
            {
                try
                {
                    *(_ptr + index) = value;
                }
                catch (Exception ex)
                {
                    throw new InterpretationException("Invalid index", ex);
                }
            } 
        }

        public dynamic this[Value index]
        {
            get
            {
                return this[index._value]; 
            }
        }

        public dynamic Addres
        {
            get
            {
                object pointer = new PointerValue(_ptr);

                return new PointerValue(&pointer);
            }
        }

        public override string ToString()
        {
            return $"0x{(IntPtr)(_ptr):X16}";  
        }
    }

    private class FunctionValue : Value
    {
        public FunctionValue(Delegate func)
            : base(func)
        { }


        public override bool TryInvoke(InvokeBinder binder, object?[]? args, out object? result)
        {
            Delegate func = (Delegate)_value;

            result = func.DynamicInvoke(args);

            return true;
        }
    }





}

public static class StringExtension
{
    public static IEnumerable<int> AllIndexesOf(this string str, string searchstring)
    {
        int minIndex = str.IndexOf(searchstring);
        while (minIndex != -1)
        {
            yield return minIndex;
            minIndex = str.IndexOf(searchstring, minIndex + searchstring.Length);
        }
    }
}

