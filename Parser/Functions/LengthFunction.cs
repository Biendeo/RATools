﻿using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class LengthFunction : FunctionDefinitionExpression
    {
        public LengthFunction()
            : base("length")
        {
            Parameters.Add(new VariableDefinitionExpression("object"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var obj = GetParameter(scope, "object", out result);
            if (obj == null)
                return false;

            switch (obj.Type)
            {
                case ExpressionType.Array:
                    result = new IntegerConstantExpression(((ArrayExpression)obj).Entries.Count);
                    return true;

                case ExpressionType.Dictionary:
                    result = new IntegerConstantExpression(((DictionaryExpression)obj).Entries.Count);
                    return true;

                case ExpressionType.StringConstant:
                    result = new IntegerConstantExpression(((StringConstantExpression)obj).Value.Length);
                    return true;

                default:
                    result = new ParseErrorExpression("Cannot calculate length of " + obj.Type);
                    return false;
            }
        }
    }
}