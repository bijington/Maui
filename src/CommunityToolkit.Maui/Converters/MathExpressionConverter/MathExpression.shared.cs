﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CommunityToolkit.Maui.Converters;

sealed class MathExpression
{
	const string regexPattern = @"(?<!\d)\-?(?:\d+\.\d+|\d+)|\+|\-|\/|\*|\(|\)|\^|\%|\,|\w+";
	const NumberStyles numberStyle = NumberStyles.Float | NumberStyles.AllowThousands;

	static readonly IFormatProvider formatProvider = new CultureInfo("en-US");

	readonly IReadOnlyList<MathOperator> operators;
	readonly IReadOnlyList<double> arguments;

	internal string Expression { get; }

	internal MathExpression(string expression, IEnumerable<double>? arguments = null)
	{
		if (string.IsNullOrEmpty(expression))
			throw new ArgumentNullException(nameof(expression), "Expression can't be null or empty.");

		Expression = expression.ToLower();
		this.arguments = arguments?.ToList() ?? new List<double>();

		var operators = new List<MathOperator>
			{
				new ("+", 2, MathOperatorPrecedence.Low, x => x[0] + x[1]),
				new ("-", 2, MathOperatorPrecedence.Low, x => x[0] - x[1]),
				new ("*", 2, MathOperatorPrecedence.Medium, x => x[0] * x[1]),
				new ("/", 2, MathOperatorPrecedence.Medium, x => x[0] / x[1]),
				new ("%", 2, MathOperatorPrecedence.Medium, x => x[0] % x[1]),
				new ("abs", 1, MathOperatorPrecedence.Medium, x => Math.Abs(x[0])),
				new ("acos", 1, MathOperatorPrecedence.Medium, x => Math.Acos(x[0])),
				new ("asin", 1, MathOperatorPrecedence.Medium, x => Math.Asin(x[0])),
				new ("atan", 1, MathOperatorPrecedence.Medium, x => Math.Atan(x[0])),
				new ("atan2", 2, MathOperatorPrecedence.Medium, x => Math.Atan2(x[0], x[1])),
				new ("ceiling", 1, MathOperatorPrecedence.Medium, x => Math.Ceiling(x[0])),
				new ("cos", 1, MathOperatorPrecedence.Medium, x => Math.Cos(x[0])),
				new ("cosh", 1, MathOperatorPrecedence.Medium, x => Math.Cosh(x[0])),
				new ("exp", 1, MathOperatorPrecedence.Medium, x => Math.Exp(x[0])),
				new ("floor", 1, MathOperatorPrecedence.Medium, x => Math.Floor(x[0])),
				new ("ieeeremainder", 2, MathOperatorPrecedence.Medium, x => Math.IEEERemainder(x[0], x[1])),
				new ("log", 2, MathOperatorPrecedence.Medium, x => Math.Log(x[0], x[1])),
				new ("log10", 1, MathOperatorPrecedence.Medium, x => Math.Log10(x[0])),
				new ("max", 2, MathOperatorPrecedence.Medium, x => Math.Max(x[0], x[1])),
				new ("min", 2, MathOperatorPrecedence.Medium, x => Math.Min(x[0], x[1])),
				new ("pow", 2, MathOperatorPrecedence.Medium, x => Math.Pow(x[0], x[1])),
				new ("round", 2, MathOperatorPrecedence.Medium, x => Math.Round(x[0], Convert.ToInt32(x[1]))),
				new ("sign", 1, MathOperatorPrecedence.Medium, x => Math.Sign(x[0])),
				new ("sin", 1, MathOperatorPrecedence.Medium, x => Math.Sin(x[0])),
				new ("sinh", 1, MathOperatorPrecedence.Medium, x => Math.Sinh(x[0])),
				new ("sqrt", 1, MathOperatorPrecedence.Medium, x => Math.Sqrt(x[0])),
				new ("tan", 1, MathOperatorPrecedence.Medium, x => Math.Tan(x[0])),
				new ("tanh", 1, MathOperatorPrecedence.Medium, x => Math.Tanh(x[0])),
				new ("truncate", 1, MathOperatorPrecedence.Medium, x => Math.Truncate(x[0])),
				new ("^", 2, MathOperatorPrecedence.High, x => Math.Pow(x[0], x[1])),
				new ("pi", 0, MathOperatorPrecedence.Constant, _ => Math.PI),
				new ("e", 0, MathOperatorPrecedence.Constant, _ => Math.E),
			};

		var argumentsCount = this.arguments.Count;

		if (argumentsCount > 0)
		{
			operators.Add(new MathOperator("x", 0, MathOperatorPrecedence.Constant, _ => this.arguments[0]));
		}

		for (var i = 0; i < argumentsCount; i++)
		{
			var index = i;
			operators.Add(new MathOperator($"x{i}", 0, MathOperatorPrecedence.Constant, _ => this.arguments[index]));
		}

		this.operators = operators;
	}

	public double Calculate()
	{
		var rpn = GetReversePolishNotation(Expression);

		var stack = new Stack<double>();

		foreach (var value in rpn)
		{
			if (double.TryParse(value, numberStyle, formatProvider, out var numeric))
			{
				stack.Push(numeric);
				continue;
			}

			var @operator = operators.FirstOrDefault(x => x.Name == value);

			if (@operator == null)
				throw new ArgumentException($"Invalid math expression. Can't find operator or value with name \"{value}\".");

			if (@operator.Precedence == MathOperatorPrecedence.Constant)
			{
				stack.Push(@operator.CalculateFunc(Array.Empty<double>()));
				continue;
			}

			var operatorNumericCount = @operator.NumericCount;

			if (stack.Count < operatorNumericCount)
				throw new ArgumentException("Invalid math expression.");

			var args = new List<double>();
			for (var j = 0; j < operatorNumericCount; j++)
			{
				args.Add(stack.Pop());
			}

			args.Reverse();

			stack.Push(@operator.CalculateFunc(args.ToArray()));
		}

		if (stack.Count != 1)
			throw new ArgumentException("Invalid math expression.");

		return stack.Pop();
	}

	IEnumerable<string> GetReversePolishNotation(string expression)
	{
		var regex = new Regex(regexPattern);

		var matches = regex.Matches(expression);
		if (matches == null)
			throw new ArgumentException("Invalid math expression.");

		var output = new List<string>();
		var stack = new Stack<(string Name, MathOperatorPrecedence Precedence)>();

		foreach (Match? match in matches)
		{
			if (match == null || string.IsNullOrEmpty(match.Value))
				continue;

			var value = match.Value;

			if (double.TryParse(value, numberStyle, formatProvider, out var numeric))
			{
				if (numeric < 0)
				{
					var isNegative = output.Count == 0 || stack.Count != 0;

					if (!isNegative)
					{
						stack.Push(("-", MathOperatorPrecedence.Low));
						output.Add(Math.Abs(numeric).ToString());
						continue;
					}
				}

				output.Add(value);
				continue;
			}

			var @operator = operators.FirstOrDefault(x => x.Name == value);
			if (@operator != null)
			{
				if (@operator.Precedence == MathOperatorPrecedence.Constant)
				{
					output.Add(value);
					continue;
				}

				while (stack.Count > 0)
				{
					var (name, precedence) = stack.Peek();
					if (precedence >= @operator.Precedence)
					{
						output.Add(stack.Pop().Name);
					}
					else
					{
						break;
					}
				}

				stack.Push((value, @operator.Precedence));
			}
			else if (value == "(")
			{
				stack.Push((value, MathOperatorPrecedence.Lowest));
			}
			else if (value == ")")
			{
				var isFound = false;
				for (var i = stack.Count - 1; i >= 0; i--)
				{
					if (stack.Count == 0)
						throw new ArgumentException("Invalid math expression.");

					var stackValue = stack.Pop().Name;
					if (stackValue == "(")
					{
						isFound = true;
						break;
					}

					output.Add(stackValue);
				}

				if (!isFound)
					throw new ArgumentException("Invalid math expression.");
			}
			else if (value == ",")
			{
				while (stack.Count > 0)
				{
					var (name, precedence) = stack.Peek();
					if (precedence >= MathOperatorPrecedence.Low)
					{
						output.Add(stack.Pop().Name);
					}
					else
					{
						break;
					}
				}
			}
		}

		for (var i = stack.Count - 1; i >= 0; i--)
		{
			var (name, precedence) = stack.Pop();
			if (name == "(")
			{
				throw new ArgumentException("Invalid math expression.");
			}

			output.Add(name);
		}

		return output;
	}
}