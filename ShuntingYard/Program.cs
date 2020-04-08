using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace ShuntingYard
{
    enum OpType
    {
        Add,
        Sub,
        Mul,
        Div,
        LeftParens,
        RightParens,
        Plus,
        Minus,
        Coma
    }
    interface INode
    {
    }

    interface IOp : INode
    {
    }

    interface IVal : INode
    {
    }

    struct UnOp : IOp
    {
        public readonly OpType Type;
        public readonly INode A;

        public UnOp(OpType type, INode a)
        {
            Type = type;
            A = a;
        }
    }

    struct BinOp : IOp
    {
        public OpType Type;
        public INode A;
        public INode B;

        public BinOp(OpType type, INode a, INode b)
        {
            Type = type;
            A = a;
            B = b;
        }
    }

    struct FuncCall : IOp
    {
        public readonly string Id;
        public readonly List<INode> Arguments;

        public FuncCall(string id, List<INode> arguments)
        {
            Id = id;
            Arguments = arguments;
        }
    }

    struct Value : IVal
    {
        public readonly float F;

        public Value(float f)
        {
            F = f;
        }
    }

    struct Variable : IVal
    {
        public string Id;

        public Variable(string id)
        {
            Id = id;
        }
    }

    static class Evaluator
    {
        public static float Eval(INode node, Dictionary<string, float> variables = null)
        {
            switch (node)
            {
                case Value v:
                    return v.F;
                case Variable variable:
                    return variables[variable.Id];
                case UnOp u:
                    return u.Type == OpType.Plus ? Eval(u.A, variables) : -Eval(u.A, variables);
                case BinOp bin:
                    var a = Eval(bin.A, variables);
                    var b = Eval(bin.B, variables);
                    switch (bin.Type)
                    {
                        case OpType.Add:
                            return a + b;
                        case OpType.Sub:
                            return a - b;
                        case OpType.Mul:
                            return a * b;
                        case OpType.Div:
                            return a / b;
                        default:
                            throw new ArgumentOutOfRangeException(bin.Type.ToString());
                    }
                    case FuncCall f:
                        void CheckArgCount(int n) => Assert.AreEqual(f.Arguments.Count, n);
                        switch (f.Id)
                        {
                            case "sin": return MathF.Sin(Eval(f.Arguments.Single(), variables));
                            case "cos": return MathF.Sin(Eval(f.Arguments.Single(), variables));
                            case "sqrt": return MathF.Sqrt(Eval(f.Arguments.Single(), variables));
                            case "abs": return MathF.Abs(Eval(f.Arguments.Single(), variables));
                            case "pow":
                                CheckArgCount(2);
                                return MathF.Pow(Eval(f.Arguments[0], variables), Eval(f.Arguments[1], variables));
                            case "min":
                                CheckArgCount(2);
                                return MathF.Min(Eval(f.Arguments[0], variables), Eval(f.Arguments[1], variables));
                            case "max":
                                CheckArgCount(2);
                                return MathF.Max(Eval(f.Arguments[0], variables), Eval(f.Arguments[1], variables));
                            default: throw new InvalidDataException($"Unknown function {f.Id}");
                        }

                default: throw new NotImplementedException();
            }
        }
    }

    public enum Token
    {
        None,
        Op,
        Number,
        Identifier,
        LeftParens,
        RightParens,
    }


    static class Parser
    {
        public static string Format(INode n)
        {
            switch (n)
            {
                case Value v:
                    return v.F.ToString();
                case Variable v:
                    return "$" + v.Id.ToString();
                case UnOp un:
                    return $"{FormatOp(un.Type)}{Format(un.A)}";
                case BinOp b:
                    return $"({Format(b.A)} {FormatOp(b.Type)} {Format(b.B)})";
                case FuncCall f:
                    var args = string.Join(", ", f.Arguments.Select(Format));
                    return $"{f.Id}({args})";
                default:
                    throw new NotImplementedException();
            }
        }

        struct OpDesc
        {
            public readonly OpType Type;
            public readonly string Str;
            public readonly int Precedence;
            public readonly Associativity Associativity;
            public bool Unary;

            public OpDesc(OpType type, string str, int precedence, Associativity associativity = Associativity.None, bool unary = false)
            {
                Type = type;
                Str = str;
                Precedence = precedence;
                Associativity = associativity;
                Unary = unary;
            }
        }

        enum Associativity
        {
            None,
            Left,
            Right,
        }
        static Dictionary<OpType, OpDesc> Ops = new Dictionary<OpType,OpDesc>
        {
            {OpType.Add, new OpDesc(OpType.Add, "+", 2, Associativity.Left)},
            {OpType.Sub, new OpDesc(OpType.Sub, "-", 2, Associativity.Left)},
            
            {OpType.Mul, new OpDesc(OpType.Mul, "*", 3, Associativity.Left)},
            {OpType.Div, new OpDesc(OpType.Div, "/", 3, Associativity.Left)},
            
            {OpType.Plus, new OpDesc(OpType.Plus, "+", 2000, Associativity.Right, unary: true)},
            {OpType.Minus, new OpDesc(OpType.Minus, "-", 2000, Associativity.Right, unary: true)},
            
            {OpType.LeftParens, new OpDesc(OpType.LeftParens, "(", 5)},
            {OpType.Coma, new OpDesc(OpType.Coma, ",", 1000)},
        };

        private static string FormatOp(OpType bType)
        {
            return Ops[bType].Str;
        }

        internal class Reader
        {
            private string _input;
            private int _i;

            public Reader(string input)
            {
                _input = input.Trim();
                _i = 0;
            }

            private void SkipWhitespace()
            {
                while (!Done && char.IsWhiteSpace(_input[_i]))
                    _i++;
            }

            public bool Done => _i >= _input.Length;
            private char NextChar => _input[_i];
            private char ConsumeChar() => _input[_i++];

            public string CurrentToken;
            public Token CurrentTokenType;
            public Token PrevTokenType;

            public void ReadToken()
            {
                CurrentToken = null;
                PrevTokenType = CurrentTokenType;
                CurrentTokenType = Token.None;
                if(Done)
                    return;
                if (NextChar == '(')
                {
                    ConsumeChar();
                    CurrentTokenType = Token.LeftParens;
                }
                else if (NextChar == ')')
                {
                    ConsumeChar();
                    CurrentTokenType = Token.RightParens;
                }
                else if (char.IsDigit(NextChar))
                {
                    StringBuilder sb = new StringBuilder();
                    do
                    {
                        sb.Append(ConsumeChar());
                    } while (!Done && char.IsDigit(NextChar));

                    CurrentToken = sb.ToString();
                    CurrentTokenType = Token.Number;
                }
                else
                {
                    if (MatchOp(out var op))
                    {
                        CurrentToken = op.Str;
                        CurrentTokenType = Token.Op;
                        for (int i = 0; i < CurrentToken.Length; i++)
                            ConsumeChar();
                    }
                    else
                    {
                        CurrentTokenType = Token.Identifier;
                        StringBuilder sb = new StringBuilder();
                        while (!Done && !char.IsDigit(NextChar) && !MatchOp(out _))
                            sb.Append(ConsumeChar());
                        CurrentToken = sb.ToString();
                    }
                }

                SkipWhitespace();
                
                bool MatchOp(out OpDesc desc)
                {
                    foreach (var pair in Ops)
                    {
                        if (_input.IndexOf(pair.Value.Str, _i, StringComparison.Ordinal) != _i)
                            continue;
                        desc = pair.Value;
                        return true;
                    }

                    desc = default;
                    return false;
                }
            }
        }

        static OpDesc ReadBinOp(string input, bool unary)
        {
            return Ops.Single(o => o.Value.Str == input && o.Value.Unary == unary).Value;
        }

        public static INode Parse(string s)
        {
            var output = new Stack<INode>();
            var opStack = new Stack<OpDesc>();
            
            Reader r = new Reader(s);
            r.ReadToken();
            var readUntilToken = Token.None;
            int startOpStackSize = 0;
            
            return ParseUntil(r, opStack, output, readUntilToken, startOpStackSize);
        }

        private static INode ParseUntil(Reader r, Stack<OpDesc> opStack, Stack<INode> output, Token readUntilToken, int startOpStackSize)
        {
            void PopOpOpandsAndPushNode(OpDesc readBinOp)
            {
                if (readBinOp.Unary)
                {
                    var a = output.Pop();
                    output.Push(new UnOp(readBinOp.Type, a));
                }
                else
                {
                    var b = output.Pop();
                    var a = output.Pop();
                    output.Push(new BinOp(readBinOp.Type, a, b));
                }
            }

            do
            {
                switch (r.CurrentTokenType)
                {
                    case Token.LeftParens:
                        opStack.Push(Ops[OpType.LeftParens]);
                        r.ReadToken();
                        break;
                    case Token.RightParens:
                    {
                        while (opStack.TryPeek(out var stackOp) && stackOp.Type != OpType.LeftParens)
                        {
                            opStack.Pop();
                            PopOpOpandsAndPushNode(stackOp);
                        }

                        if (opStack.TryPeek(out var leftParens) && leftParens.Type == OpType.LeftParens)
                            opStack.Pop();
                        r.ReadToken();
                        break;
                    }
                    case Token.Op:
                    {
                        bool unary = r.PrevTokenType == Token.Op || r.PrevTokenType == Token.LeftParens ||
                                     r.PrevTokenType == Token.None;

                        var readBinOp = ReadBinOp(r.CurrentToken, unary);

                        /*while ((there is a function at the top of the operator stack)
                        or (there is an operator at the top of the operator stack with greater precedence)
                        or (the operator at the top of the operator stack has equal precedence and the token is left associative))
                        and (the operator at the top of the operator stack is not a left parenthesis):*/
                        while (opStack.TryPeek(out var stackOp) &&
                               ( /* function ||*/
                                   stackOp.Precedence > readBinOp.Precedence ||
                                   stackOp.Precedence == readBinOp.Precedence &&
                                   readBinOp.Associativity == Associativity.Left) &&
                               stackOp.Type != OpType.LeftParens && stackOp.Type != OpType.Coma)
                        {
                            opStack.Pop();
                            PopOpOpandsAndPushNode(stackOp);
                        }

                        opStack.Push(readBinOp);
                        r.ReadToken();
                        break;
                    }
                    case Token.Number:
                        output.Push(new Value(float.Parse(r.CurrentToken)));
                        r.ReadToken();
                        break;
                    case Token.Identifier:
                        var id = r.CurrentToken;
                        r.ReadToken();
                        if (r.CurrentTokenType != Token.LeftParens) // variable
                        {
                            output.Push(new Variable(id));
                            break;
                        }
                        else // function call
                        {
                            List<INode> args = new List<INode>();
                            r.ReadToken(); // skip (
                            
                            var arg = ParseUntil(r, opStack, output, Token.RightParens, opStack.Count);
                            r.ReadToken(); // skip )

                            void RecurseThroughArguments(INode n)
                            {
                                switch (n)
                                {
                                    case BinOp b when b.Type == OpType.Coma:
                                        RecurseThroughArguments(b.A);
                                        RecurseThroughArguments(b.B);
                                        break;
                                    default:
                                        args.Add(n);
                                        break;
                                }
                            }
                            
                            RecurseThroughArguments(arg);
                            output.Push(new FuncCall(id, args));
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(r.CurrentTokenType.ToString());
                }

                Console.WriteLine(r.CurrentTokenType + " " + r.CurrentToken);
            } while (r.CurrentTokenType != readUntilToken);

            while (opStack.Count > startOpStackSize)
            {
                var readBinOp = opStack.Pop();
                if (readBinOp.Type == OpType.LeftParens || readBinOp.Type == OpType.RightParens)
                    throw new InvalidDataException("Mismatched parens");
                PopOpOpandsAndPushNode(readBinOp);
            }

            return output.Pop();
        }
    }
}