using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;

namespace ShuntingYard
{
    interface INode
    {
    }

    interface IOp : INode
    {
    }

    interface IVal : INode
    {
    }

    enum BinOpType
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

    struct UnOp : IOp
    {
        public BinOpType Type;
        public INode A;

        public UnOp(BinOpType type, INode a)
        {
            Type = type;
            A = a;
        }
    }

    struct BinOp : IOp
    {
        public BinOpType Type;
        public INode A;
        public INode B;

        public BinOp(BinOpType type, INode a, INode b)
        {
            Type = type;
            A = a;
            B = b;
        }
    }

    struct FuncCall : IOp
    {
        public string Id;
        public List<INode> Arguments;

        public FuncCall(string id, List<INode> arguments)
        {
            Id = id;
            Arguments = arguments;
        }
    }

    struct Value : IVal
    {
        public float F;

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
            public readonly BinOpType Type;
            public readonly string Str;
            public readonly int Precedence;
            public readonly Associativity Associativity;
            public bool Unary;

            public OpDesc(BinOpType type, string str, int precedence, Associativity associativity = Associativity.None, bool unary = false)
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
        static Dictionary<BinOpType, OpDesc> Ops = new Dictionary<BinOpType,OpDesc>
        {
            {BinOpType.Add, new OpDesc(BinOpType.Add, "+", 2, Associativity.Left)},
            {BinOpType.Sub, new OpDesc(BinOpType.Sub, "-", 2, Associativity.Left)},
            
            {BinOpType.Mul, new OpDesc(BinOpType.Mul, "*", 3, Associativity.Left)},
            {BinOpType.Div, new OpDesc(BinOpType.Div, "/", 3, Associativity.Left)},
            
            {BinOpType.Plus, new OpDesc(BinOpType.Plus, "+", 2000, Associativity.Right, unary: true)},
            {BinOpType.Minus, new OpDesc(BinOpType.Minus, "-", 2000, Associativity.Right, unary: true)},
            
            {BinOpType.LeftParens, new OpDesc(BinOpType.LeftParens, "(", 5)},
            {BinOpType.Coma, new OpDesc(BinOpType.Coma, ",", 1000)},
        };

        private static string FormatOp(BinOpType bType)
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
                        if (_input.IndexOf(pair.Value.Str, _i) == _i)
                        {
                            desc = pair.Value;
                            return true;
                        }
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
                        opStack.Push(Ops[BinOpType.LeftParens]);
                        r.ReadToken();
                        break;
                    case Token.RightParens:
                    {
                        while (opStack.TryPeek(out var stackOp) && stackOp.Type != BinOpType.LeftParens)
                        {
                            opStack.Pop();
                            PopOpOpandsAndPushNode(stackOp);
                        }

                        if (opStack.TryPeek(out var leftParens) && leftParens.Type == BinOpType.LeftParens)
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
                               stackOp.Type != BinOpType.LeftParens && stackOp.Type != BinOpType.Coma)
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
                                    case BinOp b when b.Type == BinOpType.Coma:
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
                if (readBinOp.Type == BinOpType.LeftParens || readBinOp.Type == BinOpType.RightParens)
                    throw new InvalidDataException("Mismatched parens");
                PopOpOpandsAndPushNode(readBinOp);
            }

            return output.Pop();
        }
    }


    class Program
    {
        [Test]
        public void Test()
        {
            Console.WriteLine(Parser.Format(new BinOp(BinOpType.Add,
                new BinOp(BinOpType.Mul, new Value(1), new Value(2)), new Value(3))));
        }

        [TestCase("3+4", "(3 + 4)")]
        [TestCase("12*34", "(12 * 34)")]
        [TestCase("12*34+45", "((12 * 34) + 45)")]
        [TestCase("12+34*45", "(12 + (34 * 45))")]
        [TestCase("12+34+45", "((12 + 34) + 45)", Description = "Left associativity")]
        [TestCase("(32+4)", "(32 + 4)")]
        [TestCase("a", "$a")]
        [TestCase("1 * a+3", "((1 * $a) + 3)")]
        // unary
        [TestCase("-1", "-1")]
        [TestCase("--1", "--1")]
        [TestCase("-3+4", "(-3 + 4)")]
        [TestCase("3+-4", "(3 + -4)")]
        [TestCase("-(3+4)", "-(3 + 4)")]
        // coma
        [TestCase("1,2", "(1 , 2)")]
        [TestCase("1,2,3", "(1 , (2 , 3))")]
        // func calls
        [TestCase("sin(42)", "sin(42)")]
        [TestCase("sin(42, 43)", "sin(42, 43)")]
        [TestCase("sin(-42)", "sin(-42)")]
        [TestCase("sin(1+2)", "sin((1 + 2))")]
        [TestCase("sin(cos(43))", "sin(cos(43))")]
        [TestCase("sin(1, cos(43))", "sin(1, cos(43))")]
        [TestCase("sin(-42, cos(-43))", "sin(-42, cos(-43))")]
        public void Parse(string input, string expectedFormat)
        {
            INode parsed = Parser.Parse(input);
            var format = Parser.Format(parsed);
            Console.WriteLine(format);
            Assert.AreEqual(expectedFormat, format);
        }
        [TestCase("32+4", "32 + 4")]
        [TestCase("32+ 4", "32 + 4")]
        [TestCase("32+ 4*1", "32 + 4 * 1")]
        [TestCase("32+ 4*a+2", "32 + 4 * a + 2")]
        [TestCase("1*a", "1 * a")]
        [TestCase("(32+4)", "( 32 + 4 )")]
        [TestCase("(32+4)*1", "( 32 + 4 ) * 1")]
        [TestCase("1,2", "1 , 2")]
        public void Tokenizer_Works(string input, string spaceSeparatedTokens)
        {
            var reader = new Parser.Reader(input);
            string result = null;
            while (!reader.Done)
            {
                reader.ReadToken();
                var readerCurrentToken = reader.CurrentTokenType == Token.LeftParens ? "(" : reader.CurrentTokenType == Token.RightParens ? ")" : reader.CurrentToken;
                if (result == null)
                    result = readerCurrentToken;
                else
                    result += " " + readerCurrentToken;
            }

            Console.WriteLine(result);
            Assert.AreEqual(spaceSeparatedTokens, result);
        }
    }
}