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
        RightParens
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

    struct Value : IVal
    {
        public float F;

        public Value(float f)
        {
            F = f;
        }
    }

    public enum Token
    {
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
                case BinOp b:

                    return $"({Format(b.A)} {FormatOp(b.Type)} {Format(b.B)})";
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

            public OpDesc(BinOpType type, string str, int precedence, Associativity associativity = Associativity.None)
            {
                Type = type;
                Str = str;
                Precedence = precedence;
                Associativity = associativity;
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
            {BinOpType.LeftParens, new OpDesc(BinOpType.LeftParens, "(", 4)},
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

            public void ReadToken()
            {
                CurrentToken = null;
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

        static OpDesc ReadBinOp(string input)
        {
            return Ops.Single(o => o.Value.Str == input).Value;
        }

        public static INode Parse(string s)
        {
            var output = new Stack<INode>();
            var opStack = new Stack<OpDesc>();
            
            Reader r = new Reader(s);
            while (!r.Done)
            {
                r.ReadToken();
                switch (r.CurrentTokenType)
                {
                    case Token.LeftParens:
                        opStack.Push(Ops[BinOpType.LeftParens]);
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
                        break;
                    }
                    case Token.Op:
                    {
                        var readBinOp = ReadBinOp(r.CurrentToken);

                        /*while ((there is a function at the top of the operator stack)
                            or (there is an operator at the top of the operator stack with greater precedence)
                            or (the operator at the top of the operator stack has equal precedence and the token is left associative))
                            and (the operator at the top of the operator stack is not a left parenthesis):*/
                        while (opStack.TryPeek(out var stackOp) && 
                               (/* function ||*/
                               stackOp.Precedence > readBinOp.Precedence ||
                               stackOp.Precedence == readBinOp.Precedence && readBinOp.Associativity == Associativity.Left) &&
                               stackOp.Type != BinOpType.LeftParens)
                        {
                            opStack.Pop();
                            PopOpOpandsAndPushNode(stackOp);
                        }
                        opStack.Push(readBinOp);
                        break;
                    }
                    case Token.Number:
                        output.Push(new Value(float.Parse(r.CurrentToken)));
                        break;
                    case Token.Identifier:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(r.CurrentTokenType.ToString());
                }
                Console.WriteLine(r.CurrentTokenType + " " + r.CurrentToken);
            }

            while (opStack.TryPop(out var readBinOp))
            {
                PopOpOpandsAndPushNode(readBinOp);
            }
            return output.Single();

            void PopOpOpandsAndPushNode(OpDesc readBinOp)
            {
                var b = output.Pop();
                var a = output.Pop();
                output.Push(new BinOp(readBinOp.Type, a, b));
            }
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