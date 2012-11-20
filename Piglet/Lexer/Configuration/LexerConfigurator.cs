using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Piglet.Lexer.Construction;
using Piglet.Lexer.Runtime;

namespace Piglet.Lexer.Configuration
{
    internal class LexerConfigurator<TContext, T> : ILexerConfigurator<TContext, T>
    {
        private readonly List<Tuple<string, Func<TContext, string, T>>> tokens;
        private readonly List<string> ignore;

        public LexerConfigurator()
        {
            tokens = new List<Tuple<string, Func<TContext, string, T>>>();
            ignore = new List<string>();
            EndOfInputTokenNumber = -1;
            MinimizeDfa = true;
            Runtime = LexerRuntime.Tabular;
        }

		public ILexer<T> CreateLexer()
		{
			return (ILexer<T>)CreateContextualLexer();
		}

    	public ILexer<TContext, T> CreateContextualLexer()
        {
            // For each token, create a NFA
            IList<NFA> nfas = tokens.Select(token => NfaBuilder.Create(new ShuntingYard(new RegExLexer( new StringReader(token.Item1))))).ToList();
            foreach (var ignoreExpr in ignore)
            {
                nfas.Add(NfaBuilder.Create(new ShuntingYard(new RegExLexer(new StringReader(ignoreExpr)))));
            }

            // Create a merged NFA
            NFA mergedNfa = NFA.Merge(nfas);

            // If we desire a NFA based lexer, stop now
            if (Runtime == LexerRuntime.Nfa)
            {
                return new NfaLexer<TContext, T>(mergedNfa, nfas, tokens, EndOfInputTokenNumber);
            }

            // Convert the NFA to a DFA
            DFA dfa = DFA.Create(mergedNfa);

            // Minimize the DFA if required
            dfa.Minimize();

            // If we desire a DFA based lexer, stop
            if (Runtime == LexerRuntime.Dfa)
            {
                // TODO:
                // The input ranges which will have been previously split into the smallest distinct
                // units will need to be recombined in order for this to work as fast as possible.
                //dfa.CombineInputRanges();
                return new DfaLexer<TContext, T>(dfa, nfas, tokens, EndOfInputTokenNumber);
            }

            // Convert the dfa to table form
            var transitionTable = new TransitionTable<TContext, T>(dfa, nfas, tokens);

            return new TabularLexer<TContext, T>(transitionTable, EndOfInputTokenNumber);
        }        

        public void Token(string regEx, Func<string, T> action)
        {
        	Token(regEx, (ctx, s) => action(s));
        }

		public void Token(string regEx, Func<TContext, string, T> action)
		{
			tokens.Add(new Tuple<string, Func<TContext, string, T>>(regEx, action));
		}

    	public void Ignore(string regEx)
        {
            ignore.Add(regEx);
        }

        public int EndOfInputTokenNumber { get; set; }
        public bool MinimizeDfa { get; set; }
        public LexerRuntime Runtime { get; set; }
    }
}