using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ActionLanguage
{
    /// <summary>
    /// Main entry point for the Action language
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Action Language REPL v1.0");
            Console.WriteLine("Type 'exit' to quit");
            Console.WriteLine();

            var interpreter = new Interpreter();
            var inputBuffer = new StringBuilder();
            bool continuationMode = false;

            while (true)
            {
                Console.Write(continuationMode ? "... " : ">>> ");
                string line = Console.ReadLine();

                if (line.Trim() == "exit")
                    break;

                inputBuffer.AppendLine(line);
                string currentInput = inputBuffer.ToString();

                if (!AreBracesBalanced(currentInput))
                {
                    continuationMode = true;
                    continue;
                }

                continuationMode = false;

                try
                {
                    var result = interpreter.Execute(currentInput);
                    if (result != null)
                    {
                        Console.WriteLine(result.ToString());
                    }
                }
                catch (ParseException e)
                {
                    Console.WriteLine($"Parse error: {e.Message}");
                    Console.WriteLine(e.SourceSnippet);
                }
                catch (RuntimeException e)
                {
                    Console.WriteLine($"Runtime error: {e.Message}");
                    if (e.StackTrace != null)
                        Console.WriteLine(e.StackTrace);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }

                // clear buffer for next input
                inputBuffer.Clear();
            }
        }

        private static bool AreBracesBalanced(string code)
        {
            int braceCount = 0;
            bool inString = false;
            bool inSingleLineComment = false;
            bool inMultiLineComment = false;

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                char next = i < code.Length - 1 ? code[i + 1] : '\0';

                // handle strings
                if (c == '"' && !inSingleLineComment && !inMultiLineComment)
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                // handle comments
                if (c == '/' && next == '/' && !inSingleLineComment && !inMultiLineComment)
                {
                    inSingleLineComment = true;
                    i++; // skip next char
                    continue;
                }

                if (c == '/' && next == '*' && !inSingleLineComment && !inMultiLineComment)
                {
                    inMultiLineComment = true;
                    i++; // skip next char
                    continue;
                }

                if (c == '*' && next == '/' && inMultiLineComment)
                {
                    inMultiLineComment = false;
                    i++; // skip next char
                    continue;
                }

                if (c == '\n' && inSingleLineComment)
                {
                    inSingleLineComment = false;
                    continue;
                }

                if (inSingleLineComment || inMultiLineComment)
                    continue;

                // count braces
                if (c == '{')
                    braceCount++;
                else if (c == '}')
                    braceCount--;
            }

            return braceCount == 0;
        }
    }

    /// <summary>
    /// Base class for built-in callable functions/methods.
    /// </summary>
    public abstract class BuiltInCallable
    {
        public string Name { get; }

        protected BuiltInCallable(string name)
        {
            Name = name;
        }

        public abstract object Call(Interpreter interpreter, List<object> arguments);
    }

    /// <summary>
    /// Represents the built-in 'print' function.
    /// </summary>
    public class PrintFunction : BuiltInCallable
    {
        public PrintFunction() : base("print") { }

        public override object Call(Interpreter interpreter, List<object> arguments)
        {
            var outputParts = new List<string>();
            foreach (var arg in arguments)
            {
                outputParts.Add(interpreter.ConvertToString(arg));
            }
            Console.WriteLine(string.Join(" ", outputParts));
            return null; // print function returns void/null
        }
    }

    /// <summary>
    /// Custom exception for parse errors
    /// </summary>
    public class ParseException : Exception
    {
        public int Line { get; }
        public int Column { get; }
        public string SourceSnippet { get; }

        public ParseException(string message, int line, int column, string sourceSnippet)
            : base(message)
        {
            Line = line;
            Column = column;
            SourceSnippet = sourceSnippet;
        }
    }

    /// <summary>
    /// Custom exception for runtime errors
    /// </summary>
    public class RuntimeException : Exception
    {
        public string StackTrace { get; }

        public RuntimeException(string message, string stackTrace = null)
            : base(message)
        {
            StackTrace = stackTrace;
        }
    }

    /// <summary>
    /// Main interpreter class that processes Action code
    /// </summary>
    public class Interpreter
    {
        private Environment globalEnvironment;
        private Dictionary<string, TypeDefinition> typeDefinitions;
        private Dictionary<string, ImportedModule> importedModules;

        public Interpreter()
        {
            globalEnvironment = new Environment();
            typeDefinitions = new Dictionary<string, TypeDefinition>();
            importedModules = new Dictionary<string, ImportedModule>();

            RegisterBuiltInTypes();
        }

        private void RegisterBuiltInTypes()
        {
            // REGISTER PRIMITIVES
            typeDefinitions["int"] = new PrimitiveType("int", typeof(int));
            typeDefinitions["uint"] = new PrimitiveType("uint", typeof(uint));
            typeDefinitions["string"] = new PrimitiveType("string", typeof(string));
            typeDefinitions["bool"] = new PrimitiveType("bool", typeof(bool));
            typeDefinitions["void"] = new PrimitiveType("void", typeof(void));

            // BUILT-IN FUNCTIONS
            globalEnvironment.Define("print", new PrintFunction());
        }

        public object Execute(string code)
        {
            var parser = new Parser(code);
            var ast = parser.Parse();

            return ExecuteAst(ast);
        }

        private object ExecuteAst(AstNode ast)
        {
            if (ast is ActionProgram program)
            {
                object lastResult = null;
                foreach (var statement in program.Statements)
                {
                    lastResult = ExecuteAst(statement);
                }
                return lastResult;
            }
            else if (ast is VariableDeclaration varDecl)
            {
                var value = ExecuteAst(varDecl.Initializer);
                
                if (!IsTypeCompatible(value, varDecl.Type))
                {
                    throw new RuntimeException($"Type mismatch: Cannot assign {GetValueType(value)} to {varDecl.Type}");
                }

                globalEnvironment.Define(varDecl.Name, value);
                return null;
            }
            else if (ast is FunctionDeclaration funcDecl)
            {
                var function = new ActionFunction(funcDecl.Name, funcDecl.Parameters, funcDecl.ReturnType,
                                                 funcDecl.Body, globalEnvironment);
                globalEnvironment.Define(funcDecl.Name, function);
                return null;
            }
            else if (ast is ClassDeclaration classDecl)
            {
                // register the class type
                var classType = new ClassType(classDecl.Name, classDecl.SuperClass);

                // add methods
                foreach (var method in classDecl.Methods)
                {
                    classType.AddMethod(method.Name, method);
                }

                // add props
                foreach (var property in classDecl.Properties)
                {
                    classType.AddProperty(property.Name, property.Type);
                }

                typeDefinitions[classDecl.Name] = classType;
                return null;
            }
            else if (ast is ImportStatement importStmt)
            {
                // import module
                string filePath = importStmt.Path;
                if (!File.Exists(filePath))
                {
                    throw new RuntimeException($"Import error: File '{filePath}' not found");
                }

                string moduleCode = File.ReadAllText(filePath);
                var moduleInterpreter = new Interpreter();
                moduleInterpreter.Execute(moduleCode);

                // merge envs
                importedModules[filePath] = new ImportedModule(filePath, moduleInterpreter.globalEnvironment);
                globalEnvironment.MergeWith(moduleInterpreter.globalEnvironment);

                return null;
            }
            else if (ast is BinaryExpression binary)
            {
                var left = ExecuteAst(binary.Left);
                var right = ExecuteAst(binary.Right);

                switch (binary.Operator)
                {
                    case "+": return Add(left, right);
                    case "-": return Subtract(left, right);
                    case "*": return Multiply(left, right);
                    case "/": return Divide(left, right);
                    case "%": return Modulo(left, right);
                    case "==": return Equals(left, right);
                    case "!=": return !Equals(left, right);
                    case "<": return LessThan(left, right);
                    case "<=": return LessThanOrEqual(left, right);
                    case ">": return GreaterThan(left, right);
                    case ">=": return GreaterThanOrEqual(left, right);
                    case "&&": return And(left, right);
                    case "||": return Or(left, right);
                    default:
                        throw new RuntimeException($"Unknown operator: {binary.Operator}");
                }
            }
            else if (ast is UnaryExpression unary)
            {
                var operand = ExecuteAst(unary.Operand);

                switch (unary.Operator)
                {
                    case "-": return Negate(operand);
                    case "!": return Not(operand);
                    default:
                        throw new RuntimeException($"Unknown unary operator: {unary.Operator}");
                }
            }
            else if (ast is Literal literal)
            {
                return literal.Value;
            }
            else if (ast is Identifier identifier)
            {
                var value = globalEnvironment.Get(identifier.Name);
                if (value == null)
                {
                    throw new RuntimeException($"Undefined variable: {identifier.Name}");
                }
                return value;
            }
            else if (ast is CallExpression call)
            {
                var callee = ExecuteAst(call.Callee);
                var arguments = call.Arguments.Select(ExecuteAst).ToList();

                if (callee is ActionFunction function)
                {
                    // check arg count
                    if (arguments.Count != function.Parameters.Count)
                    {
                        throw new RuntimeException($"Function '{function.Name}' expects {function.Parameters.Count} arguments but got {arguments.Count}");
                    }

                    // check arg types
                    for (int i = 0; i < arguments.Count; i++)
                    {
                        var paramType = function.Parameters[i].Type;
                        var argValue = arguments[i];

                        if (!IsTypeCompatible(argValue, paramType))
                        {
                            throw new RuntimeException($"Type mismatch: Parameter '{function.Parameters[i].Name}' expects {paramType} but got {GetValueType(argValue)}");
                        }
                    }

                    // create new env for the func
                    var functionEnv = new Environment(function.Closure);

                    // bind params to arguments
                    for (int i = 0; i < arguments.Count; i++)
                    {
                        functionEnv.Define(function.Parameters[i].Name, arguments[i]);
                    }

                    // execute the function body
                    var previousEnv = globalEnvironment;
                    globalEnvironment = functionEnv;

                    object result = null;
                    try
                    {
                        foreach (var statement in function.Body)
                        {
                            result = ExecuteAst(statement);

                            // handle return statements
                            if (statement is ReturnStatement)
                                break;
                        }
                    }
                    finally
                    {
                        globalEnvironment = previousEnv;
                    }

                    if (function.ReturnType != "void" && !IsTypeCompatible(result, function.ReturnType))
                    {
                        throw new RuntimeException($"Return type mismatch: Function '{function.Name}' should return {function.ReturnType} but returned {GetValueType(result)}");
                    }

                    return result;
                }
                else if (callee is BuiltInCallable builtIn)
                {
                    return builtIn.Call(this, arguments);
                }
                else
                {
                    throw new RuntimeException("Cannot call non-function or non-built-in.");
                }
            }
            else if (ast is ObjectLiteral objectLiteral)
            {
                var obj = new Dictionary<string, object>();
                foreach (var property in objectLiteral.Properties)
                {
                    var value = ExecuteAst(property.Value);
                    obj[property.Name] = value;
                }
                return obj;
            }
            else if (ast is MemberExpression member)
            {
                var obj = ExecuteAst(member.Object);

                if (obj is Dictionary<string, object> dict)
                {
                    if (dict.TryGetValue(member.PropertyName, out var value))
                    {
                        return value;
                    }
                    throw new RuntimeException($"Property '{member.PropertyName}' not found on object");
                }
                else
                {
                    throw new RuntimeException("Cannot access property of non-object");
                }
            }
            else if (ast is IfStatement ifStmt)
            {
                var condition = ExecuteAst(ifStmt.Condition);

                if (!(condition is bool))
                {
                    throw new RuntimeException("Condition must be a boolean expression");
                }

                if ((bool)condition)
                {
                    object result = null;
                    foreach (var statement in ifStmt.ThenBranch)
                    {
                        result = ExecuteAst(statement);
                    }
                    return result;
                }
                else if (ifStmt.ElseBranch != null)
                {
                    object result = null;
                    foreach (var statement in ifStmt.ElseBranch)
                    {
                        result = ExecuteAst(statement);
                    }
                    return result;
                }

                return null;
            }
            else if (ast is WhileStatement whileStmt)
            {
                object result = null;

                while (true)
                {
                    var condition = ExecuteAst(whileStmt.Condition);

                    if (!(condition is bool))
                    {
                        throw new RuntimeException("Condition must be a boolean expression");
                    }

                    if (!(bool)condition)
                        break;

                    foreach (var statement in whileStmt.Body)
                    {
                        result = ExecuteAst(statement);
                    }
                }

                return result;
            }
            else if (ast is ForStatement forStmt)
            {
                object result = null;

                var previousEnv = globalEnvironment;
                globalEnvironment = new Environment(previousEnv);

                try
                {
                    // init
                    if (forStmt.Initializer != null)
                    {
                        ExecuteAst(forStmt.Initializer);
                    }

                    // check condition and execute body
                    while (true)
                    {
                        if (forStmt.Condition != null)
                        {
                            var condition = ExecuteAst(forStmt.Condition);

                            if (!(condition is bool))
                            {
                                throw new RuntimeException("Condition must be a boolean expression");
                            }

                            if (!(bool)condition)
                                break;
                        }

                        foreach (var statement in forStmt.Body)
                        {
                            result = ExecuteAst(statement);
                        }

                        // increment
                        if (forStmt.Increment != null)
                        {
                            ExecuteAst(forStmt.Increment);
                        }
                    }
                }
                finally
                {
                    globalEnvironment = previousEnv;
                }

                return result;
            }
            else if (ast is ReturnStatement returnStmt)
            {
                if (returnStmt.Value != null)
                {
                    return ExecuteAst(returnStmt.Value);
                }
                return null;
            }
            else if (ast is BlockStatement blockStmt)
            {
                var previousEnv = globalEnvironment;
                globalEnvironment = new Environment(previousEnv);

                object result = null;
                try
                {
                    foreach (var statement in blockStmt.Statements)
                    {
                        result = ExecuteAst(statement);
                    }
                }
                finally
                {
                    globalEnvironment = previousEnv;
                }

                return result;
            }
            else if (ast is ExpressionStatement exprStmt)
            {
                return ExecuteAst(exprStmt.Expression);
            }
            else if (ast is AssignmentExpression assignExpr)
            {
                var value = ExecuteAst(assignExpr.Value);

                if (assignExpr.Target is Identifier identifiera)
                {
                    var variable = globalEnvironment.GetVariable(identifiera.Name);
                    if (variable == null)
                    {
                        throw new RuntimeException($"Undefined variable: {identifiera.Name}");
                    }

                    var originalValue = globalEnvironment.Get(identifiera.Name);
                    if (!IsTypeCompatible(value, GetValueType(originalValue)))
                    {
                        throw new RuntimeException($"Type mismatch: Cannot assign {GetValueType(value)} to {GetValueType(originalValue)}");
                    }

                    globalEnvironment.Assign(identifiera.Name, value);
                    return value;
                }
                else if (assignExpr.Target is MemberExpression memberExpr)
                {
                    var obj = ExecuteAst(memberExpr.Object);

                    if (obj is Dictionary<string, object> dict)
                    {
                        dict[memberExpr.PropertyName] = value;
                        return value;
                    }
                    else
                    {
                        throw new RuntimeException("Cannot assign to property of non-object");
                    }
                }
                else
                {
                    throw new RuntimeException("Invalid assignment target");
                }
            }
            else if (ast is SwitchStatement switchStmt)
            {
                var discriminant = ExecuteAst(switchStmt.Discriminant);

                foreach (var caseClause in switchStmt.Cases)
                {
                    if (caseClause.Test == null) // default case
                    {
                        object result = null;
                        foreach (var statement in caseClause.Consequent)
                        {
                            result = ExecuteAst(statement);
                        }
                        return result;
                    }

                    var test = ExecuteAst(caseClause.Test);
                    if (Equals(discriminant, test))
                    {
                        object result = null;
                        foreach (var statement in caseClause.Consequent)
                        {
                            result = ExecuteAst(statement);
                        }
                        return result;
                    }
                }

                return null;
            }

            throw new RuntimeException($"Unknown AST node: {ast.GetType().Name}");
        }

        private bool IsTypeCompatible(object value, string typeName)
        {
            if (value == null)
                return false;

            string valueType = GetValueType(value);

            if (valueType == typeName)
                return true;

            // check inheritance
            if (typeDefinitions.TryGetValue(typeName, out var type) && type is ClassType classType)
            {
                if (typeDefinitions.TryGetValue(valueType, out var valueTypeDefinition) && valueTypeDefinition is ClassType valueClassType)
                {
                    return valueClassType.IsSubclassOf(classType.Name);
                }
            }

            return false;
        }

        public string ConvertToString(object value)
        {
            if (value == null) return "null";
            if (value is bool b) return b.ToString().ToLower(); // true/false instead of True/False
            if (value is string s) return s; // already a string, no quotes needed for internal representation

            if (value is Dictionary<string, object> dict)
            {
                var parts = dict.Select(kvp => $"{kvp.Key}: {ConvertToString(kvp.Value)}");
                return $"{{ {string.Join(", ", parts)} }}";
            }

            // TODO: more custom formatting for other types
            return value.ToString();
        }

        private string GetValueType(object value)
        {
            if (value == null)
                return "null";

            if (value is int) return "int";
            if (value is uint) return "uint";
            if (value is string) return "string";
            if (value is bool) return "bool";
            if (value is Dictionary<string, object>) return "Object";
            if (value is ActionFunction) return "Function";

            return value.GetType().Name;
        }

        #region Operators
        private object Add(object left, object right)
        {
            if (left is int l && right is int r)
                return l + r;
            if (left is uint l2 && right is uint r2)
                return l2 + r2;
            if (left is string || right is string)
                return left.ToString() + right.ToString();

            throw new RuntimeException($"Cannot add {GetValueType(left)} and {GetValueType(right)}");
        }

        private object Subtract(object left, object right)
        {
            if (left is int l && right is int r)
                return l - r;
            if (left is uint l2 && right is uint r2)
                return l2 - r2;

            throw new RuntimeException($"Cannot subtract {GetValueType(right)} from {GetValueType(left)}");
        }

        private object Multiply(object left, object right)
        {
            if (left is int l && right is int r)
                return l * r;
            if (left is uint l2 && right is uint r2)
                return l2 * r2;

            throw new RuntimeException($"Cannot multiply {GetValueType(left)} and {GetValueType(right)}");
        }

        private object Divide(object left, object right)
        {
            if (right is int r && r == 0 || right is uint r2 && r2 == 0)
                throw new RuntimeException("Division by zero");

            if (left is int l && right is int r3)
                return l / r3;
            if (left is uint l2 && right is uint r4)
                return l2 / r4;

            throw new RuntimeException($"Cannot divide {GetValueType(left)} by {GetValueType(right)}");
        }

        private object Modulo(object left, object right)
        {
            if (right is int r && r == 0 || right is uint r2 && r2 == 0)
                throw new RuntimeException("Modulo by zero");

            if (left is int l && right is int r3)
                return l % r3;
            if (left is uint l2 && right is uint r4)
                return l2 % r4;

            throw new RuntimeException($"Cannot perform modulo on {GetValueType(left)} and {GetValueType(right)}");
        }

        private bool Equals(object left, object right)
        {
            if (left == null && right == null)
                return true;
            if (left == null || right == null)
                return false;

            return left.Equals(right);
        }

        private bool LessThan(object left, object right)
        {
            if (left is int l && right is int r)
                return l < r;
            if (left is uint l2 && right is uint r2)
                return l2 < r2;
            if (left is string l3 && right is string r3)
                return string.Compare(l3, r3) < 0;

            throw new RuntimeException($"Cannot compare {GetValueType(left)} < {GetValueType(right)}");
        }

        private bool LessThanOrEqual(object left, object right)
        {
            if (left is int l && right is int r)
                return l <= r;
            if (left is uint l2 && right is uint r2)
                return l2 <= r2;
            if (left is string l3 && right is string r3)
                return string.Compare(l3, r3) <= 0;

            throw new RuntimeException($"Cannot compare {GetValueType(left)} <= {GetValueType(right)}");
        }

        private bool GreaterThan(object left, object right)
        {
            if (left is int l && right is int r)
                return l > r;
            if (left is uint l2 && right is uint r2)
                return l2 > r2;
            if (left is string l3 && right is string r3)
                return string.Compare(l3, r3) > 0;

            throw new RuntimeException($"Cannot compare {GetValueType(left)} > {GetValueType(right)}");
        }

        private bool GreaterThanOrEqual(object left, object right)
        {
            if (left is int l && right is int r)
                return l >= r;
            if (left is uint l2 && right is uint r2)
                return l2 >= r2;
            if (left is string l3 && right is string r3)
                return string.Compare(l3, r3) >= 0;

            throw new RuntimeException($"Cannot compare {GetValueType(left)} >= {GetValueType(right)}");
        }

        private bool And(object left, object right)
        {
            if (left is bool l && right is bool r)
                return l && r;

            throw new RuntimeException($"Cannot perform logical AND on {GetValueType(left)} and {GetValueType(right)}");
        }

        private bool Or(object left, object right)
        {
            if (left is bool l && right is bool r)
                return l || r;

            throw new RuntimeException($"Cannot perform logical OR on {GetValueType(left)} and {GetValueType(right)}");
        }

        private object Negate(object operand)
        {
            if (operand is int i)
                return -i;
            if (operand is uint u)
                return (uint)-(int)u;

            throw new RuntimeException($"Cannot negate {GetValueType(operand)}");
        }

        private bool Not(object operand)
        {
            if (operand is bool b)
                return !b;

            throw new RuntimeException($"Cannot perform logical NOT on {GetValueType(operand)}");
        }
        #endregion
    }

    /// <summary>
    /// Environment class for variable and function storage
    /// </summary>
    public class Environment
    {
        private Dictionary<string, object> values = new Dictionary<string, object>();
        private Environment parent;

        public Environment(Environment parent = null)
        {
            this.parent = parent;
        }

        public void Define(string name, object value)
        {
            values[name] = value;
        }

        public object Get(string name)
        {
            if (values.TryGetValue(name, out var value))
            {
                return value;
            }

            if (parent != null)
            {
                return parent.Get(name);
            }

            return null;
        }

        public Environment GetVariable(string name)
        {
            if (values.ContainsKey(name))
            {
                return this;
            }

            if (parent != null)
            {
                return parent.GetVariable(name);
            }

            return null;
        }

        public void Assign(string name, object value)
        {
            var env = GetVariable(name);
            if (env != null)
            {
                env.values[name] = value;
            }
            else
            {
                throw new RuntimeException($"Undefined variable: {name}");
            }
        }

        public void MergeWith(Environment other)
        {
            foreach (var kvp in other.values)
            {
                this.values[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Parser class that converts code into AST
    /// </summary>
    public class Parser
    {
        private readonly string source;
        private int pos = 0;
        private int line = 1;
        private int column = 1;
        private string currentToken;
        private object currentValue;

        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "var", "function", "return", "if", "else", "while", "for", "class", "extends",
            "import", "true", "false", "null", "switch", "case", "default", "break"
        };

        public Parser(string source)
        {
            this.source = source;
            NextToken(); // init with first token
        }

        public AstNode Parse()
        {
            var program = new ActionProgram();

            while (currentToken != "EOF")
            {
                var statement = ParseStatement();
                if (statement != null)
                {
                    program.Statements.Add(statement);
                }
            }

            return program;
        }

        private AstNode ParseStatement()
        {
            switch (currentToken)
            {
                case "var":
                    return ParseVariableDeclaration();
                case "function":
                    return ParseFunctionDeclaration();
                case "return":
                    return ParseReturnStatement();
                case "if":
                    return ParseIfStatement();
                case "while":
                    return ParseWhileStatement();
                case "for":
                    return ParseForStatement();
                case "class":
                    return ParseClassDeclaration();
                case "import":
                    return ParseImportStatement();
                case "switch":
                    return ParseSwitchStatement();
                case "{":
                    return ParseBlockStatement();
                case ";":
                    NextToken(); // skip empty statements
                    return null;
                default:
                    var expr = ParseExpression();
                    Expect(";");
                    return new ExpressionStatement { Expression = expr };
            }
        }

        private VariableDeclaration ParseVariableDeclaration()
        {
            Expect("var");

            var name = ExpectIdentifier();

            Expect(":");
            var type = ExpectIdentifier();

            AstNode initializer = null;
            if (currentToken == "=")
            {
                NextToken();
                initializer = ParseExpression();
            }

            Expect(";");

            return new VariableDeclaration
            {
                Name = name,
                Type = type,
                Initializer = initializer
            };
        }

        private FunctionDeclaration ParseFunctionDeclaration()
        {
            Expect("function");

            var name = ExpectIdentifier();

            Expect("(");
            var parameters = new List<ParameterDeclaration>();

            if (currentToken != ")")
            {
                do
                {
                    var paramName = ExpectIdentifier();
                    Expect(":");
                    var paramType = ExpectIdentifier();

                    parameters.Add(new ParameterDeclaration
                    {
                        Name = paramName,
                        Type = paramType
                    });

                    if (currentToken != ",")
                        break;

                    NextToken(); // Skip ,
                }
                while (true);
            }

            Expect(")");

            Expect(":");
            var returnType = ExpectIdentifier();

            Expect("{");
            var body = new List<AstNode>();

            while (currentToken != "}" && currentToken != "EOF")
            {
                var statement = ParseStatement();
                if (statement != null)
                {
                    body.Add(statement);
                }
            }

            Expect("}");

            return new FunctionDeclaration
            {
                Name = name,
                Parameters = parameters,
                ReturnType = returnType,
                Body = body
            };
        }

        private ReturnStatement ParseReturnStatement()
        {
            Expect("return");

            AstNode value = null;
            if (currentToken != ";")
            {
                value = ParseExpression();
            }

            Expect(";");

            return new ReturnStatement { Value = value };
        }

        private IfStatement ParseIfStatement()
        {
            Expect("if");

            Expect("(");
            var condition = ParseExpression();
            Expect(")");

            var thenBranch = new List<AstNode>();

            if (currentToken == "{")
            {
                var block = ParseBlockStatement() as BlockStatement;
                thenBranch.AddRange(block.Statements);
            }
            else
            {
                var statement = ParseStatement();
                if (statement != null)
                {
                    thenBranch.Add(statement);
                }
            }

            List<AstNode> elseBranch = null;

            if (currentToken == "else")
            {
                NextToken();

                elseBranch = new List<AstNode>();

                if (currentToken == "{")
                {
                    var block = ParseBlockStatement() as BlockStatement;
                    elseBranch.AddRange(block.Statements);
                }
                else if (currentToken == "if")
                {
                    var nestedIf = ParseIfStatement();
                    elseBranch.Add(nestedIf);
                }
                else
                {
                    var statement = ParseStatement();
                    if (statement != null)
                    {
                        elseBranch.Add(statement);
                    }
                }
            }

            return new IfStatement
            {
                Condition = condition,
                ThenBranch = thenBranch,
                ElseBranch = elseBranch
            };
        }

        private WhileStatement ParseWhileStatement()
        {
            Expect("while");

            Expect("(");
            var condition = ParseExpression();
            Expect(")");

            var body = new List<AstNode>();

            if (currentToken == "{")
            {
                var block = ParseBlockStatement() as BlockStatement;
                body.AddRange(block.Statements);
            }
            else
            {
                var statement = ParseStatement();
                if (statement != null)
                {
                    body.Add(statement);
                }
            }

            return new WhileStatement
            {
                Condition = condition,
                Body = body
            };
        }

        private ForStatement ParseForStatement()
        {
            Expect("for");

            Expect("(");

            AstNode initializer = null;
            if (currentToken != ";")
            {
                if (currentToken == "var")
                {
                    initializer = ParseVariableDeclaration();
                }
                else
                {
                    initializer = new ExpressionStatement { Expression = ParseExpression() };
                    Expect(";");
                }
            }
            else
            {
                NextToken();
            }

            AstNode condition = null;
            if (currentToken != ";")
            {
                condition = ParseExpression();
            }

            Expect(";");

            AstNode increment = null;
            if (currentToken != ")")
            {
                increment = ParseExpression();
            }

            Expect(")");

            var body = new List<AstNode>();

            if (currentToken == "{")
            {
                var block = ParseBlockStatement() as BlockStatement;
                body.AddRange(block.Statements);
            }
            else
            {
                var statement = ParseStatement();
                if (statement != null)
                {
                    body.Add(statement);
                }
            }

            return new ForStatement
            {
                Initializer = initializer,
                Condition = condition,
                Increment = increment,
                Body = body
            };
        }

        private ClassDeclaration ParseClassDeclaration()
        {
            Expect("class");

            var name = ExpectIdentifier();

            string superClass = null;
            if (currentToken == "extends")
            {
                NextToken();
                superClass = ExpectIdentifier();
            }

            Expect("{");

            var methods = new List<FunctionDeclaration>();
            var properties = new List<VariableDeclaration>();

            while (currentToken != "}" && currentToken != "EOF")
            {
                if (currentToken == "function")
                {
                    var method = ParseFunctionDeclaration();
                    methods.Add(method);
                }
                else if (currentToken == "var")
                {
                    var property = ParseVariableDeclaration();
                    properties.Add(property);
                }
                else
                {
                    throw new ParseException($"Expected method or property declaration, got {currentToken}", line, column, GetSourceSnippet());
                }
            }

            Expect("}");

            return new ClassDeclaration
            {
                Name = name,
                SuperClass = superClass,
                Methods = methods,
                Properties = properties
            };
        }

        private ImportStatement ParseImportStatement()
        {
            Expect("import");

            if (currentToken != "STRING")
            {
                throw new ParseException("Expected string literal for import path", line, column, GetSourceSnippet());
            }

            var path = (string)currentValue;
            NextToken();

            Expect(";");

            return new ImportStatement { Path = path };
        }

        private SwitchStatement ParseSwitchStatement()
        {
            Expect("switch");

            Expect("(");
            var discriminant = ParseExpression();
            Expect(")");

            Expect("{");

            var cases = new List<SwitchCase>();

            while (currentToken != "}" && currentToken != "EOF")
            {
                if (currentToken == "case")
                {
                    NextToken();

                    var test = ParseExpression();

                    Expect(":");

                    var consequent = new List<AstNode>();

                    while (currentToken != "case" && currentToken != "default" && currentToken != "}" && currentToken != "EOF")
                    {
                        var statement = ParseStatement();
                        if (statement != null)
                        {
                            consequent.Add(statement);
                        }
                    }

                    cases.Add(new SwitchCase { Test = test, Consequent = consequent });
                }
                else if (currentToken == "default")
                {
                    NextToken();

                    Expect(":");

                    var consequent = new List<AstNode>();

                    while (currentToken != "case" && currentToken != "default" && currentToken != "}" && currentToken != "EOF")
                    {
                        var statement = ParseStatement();
                        if (statement != null)
                        {
                            consequent.Add(statement);
                        }
                    }

                    cases.Add(new SwitchCase { Test = null, Consequent = consequent }); // null test indicates default case
                }
                else
                {
                    throw new ParseException($"Expected 'case' or 'default', got {currentToken}", line, column, GetSourceSnippet());
                }
            }

            Expect("}");

            return new SwitchStatement
            {
                Discriminant = discriminant,
                Cases = cases
            };
        }

        private BlockStatement ParseBlockStatement()
        {
            Expect("{");

            var statements = new List<AstNode>();

            while (currentToken != "}" && currentToken != "EOF")
            {
                var statement = ParseStatement();
                if (statement != null)
                {
                    statements.Add(statement);
                }
            }

            Expect("}");

            return new BlockStatement { Statements = statements };
        }

        private AstNode ParseExpression()
        {
            return ParseAssignment();
        }

        private AstNode ParseAssignment()
        {
            var expr = ParseLogicalOr();

            if (currentToken == "=")
            {
                NextToken();
                var value = ParseAssignment();

                return new AssignmentExpression
                {
                    Target = expr,
                    Value = value
                };
            }

            return expr;
        }

        private AstNode ParseLogicalOr()
        {
            var expr = ParseLogicalAnd();

            while (currentToken == "||")
            {
                var op = currentToken;
                NextToken();
                var right = ParseLogicalAnd();

                expr = new BinaryExpression
                {
                    Left = expr,
                    Operator = op,
                    Right = right
                };
            }

            return expr;
        }

        private AstNode ParseLogicalAnd()
        {
            var expr = ParseEquality();

            while (currentToken == "&&")
            {
                var op = currentToken;
                NextToken();
                var right = ParseEquality();

                expr = new BinaryExpression
                {
                    Left = expr,
                    Operator = op,
                    Right = right
                };
            }

            return expr;
        }

        private AstNode ParseEquality()
        {
            var expr = ParseComparison();

            while (currentToken == "==" || currentToken == "!=")
            {
                var op = currentToken;
                NextToken();
                var right = ParseComparison();

                expr = new BinaryExpression
                {
                    Left = expr,
                    Operator = op,
                    Right = right
                };
            }

            return expr;
        }

        private AstNode ParseComparison()
        {
            var expr = ParseAdditive();

            while (currentToken == "<" || currentToken == "<=" || currentToken == ">" || currentToken == ">=")
            {
                var op = currentToken;
                NextToken();
                var right = ParseAdditive();

                expr = new BinaryExpression
                {
                    Left = expr,
                    Operator = op,
                    Right = right
                };
            }

            return expr;
        }

        private AstNode ParseAdditive()
        {
            var expr = ParseMultiplicative();

            while (currentToken == "+" || currentToken == "-")
            {
                var op = currentToken;
                NextToken();
                var right = ParseMultiplicative();

                expr = new BinaryExpression
                {
                    Left = expr,
                    Operator = op,
                    Right = right
                };
            }

            return expr;
        }

        private AstNode ParseMultiplicative()
        {
            var expr = ParseUnary();

            while (currentToken == "*" || currentToken == "/" || currentToken == "%")
            {
                var op = currentToken;
                NextToken();
                var right = ParseUnary();

                expr = new BinaryExpression
                {
                    Left = expr,
                    Operator = op,
                    Right = right
                };
            }

            return expr;
        }

        private AstNode ParseUnary()
        {
            if (currentToken == "-" || currentToken == "!")
            {
                var op = currentToken;
                NextToken();
                var operand = ParseUnary();

                return new UnaryExpression
                {
                    Operator = op,
                    Operand = operand
                };
            }

            return ParseCallAndMember();
        }

        private AstNode ParseCallAndMember()
        {
            var expr = ParsePrimary();

            while (true)
            {
                if (currentToken == "(")
                {
                    expr = ParseCall(expr);
                }
                else if (currentToken == ".")
                {
                    NextToken();
                    var propertyName = ExpectIdentifier();

                    expr = new MemberExpression
                    {
                        Object = expr,
                        PropertyName = propertyName
                    };
                }
                else
                {
                    break;
                }
            }

            return expr;
        }

        private AstNode ParseCall(AstNode callee)
        {
            Expect("(");

            var arguments = new List<AstNode>();

            if (currentToken != ")")
            {
                do
                {
                    arguments.Add(ParseExpression());

                    if (currentToken != ",")
                        break;

                    NextToken(); // Skip ,
                }
                while (true);
            }

            Expect(")");

            return new CallExpression
            {
                Callee = callee,
                Arguments = arguments
            };
        }

        private AstNode ParsePrimary()
        {
            switch (currentToken)
            {
                case "IDENTIFIER":
                    var name = (string)currentValue;
                    NextToken();
                    return new Identifier { Name = name };

                case "NUMBER":
                    var value = currentValue;
                    NextToken();
                    return new Literal { Value = value };

                case "STRING":
                    var str = (string)currentValue;
                    NextToken();
                    return new Literal { Value = str };

                case "true":
                    NextToken();
                    return new Literal { Value = true };

                case "false":
                    NextToken();
                    return new Literal { Value = false };

                case "null":
                    NextToken();
                    return new Literal { Value = null };

                case "(":
                    NextToken();
                    var expr = ParseExpression();
                    Expect(")");
                    return expr;

                case "{":
                    return ParseObjectLiteral();

                default:
                    throw new ParseException($"Unexpected token: {currentToken}", line, column, GetSourceSnippet());
            }
        }

        private ObjectLiteral ParseObjectLiteral()
        {
            Expect("{");

            var properties = new List<PropertyDefinition>();

            if (currentToken != "}")
            {
                do
                {
                    var name = ExpectIdentifier();

                    Expect(":");

                    var value = ParseExpression();

                    properties.Add(new PropertyDefinition { Name = name, Value = value });

                    if (currentToken != ",")
                        break;

                    NextToken(); // Skip ,
                }
                while (true);
            }

            Expect("}");

            return new ObjectLiteral { Properties = properties };
        }

        private void NextToken()
        {
            SkipWhitespace();

            if (pos >= source.Length)
            {
                currentToken = "EOF";
                currentValue = null;
                return;
            }

            var c = source[pos];

            // Identifiers and keywords
            if (char.IsLetter(c) || c == '_')
            {
                var start = pos;
                while (pos < source.Length && (char.IsLetterOrDigit(source[pos]) || source[pos] == '_'))
                {
                    pos++;
                    column++;
                }

                var identifier = source.Substring(start, pos - start);

                if (Keywords.Contains(identifier))
                {
                    currentToken = identifier;
                }
                else
                {
                    currentToken = "IDENTIFIER";
                    currentValue = identifier;
                }

                return;
            }

            // Numbers
            if (char.IsDigit(c))
            {
                var start = pos;
                while (pos < source.Length && char.IsDigit(source[pos]))
                {
                    pos++;
                    column++;
                }

                currentToken = "NUMBER";
                currentValue = int.Parse(source.Substring(start, pos - start));
                return;
            }

            // Strings
            if (c == '"')
            {
                pos++; // Skip opening quote
                column++;

                var start = pos;
                while (pos < source.Length && source[pos] != '"')
                {
                    if (source[pos] == '\\' && pos + 1 < source.Length)
                    {
                        pos += 2; // Skip escape sequence
                        column += 2;
                    }
                    else
                    {
                        pos++;
                        column++;
                    }
                }

                if (pos >= source.Length)
                {
                    throw new ParseException("Unterminated string literal", line, column, GetSourceSnippet());
                }

                var value = source.Substring(start, pos - start);
                pos++; // Skip closing quote
                column++;

                currentToken = "STRING";
                currentValue = value;
                return;
            }

            // Comments
            if (c == '/' && pos + 1 < source.Length)
            {
                if (source[pos + 1] == '/')
                {
                    // Single-line comment
                    pos += 2; // Skip //
                    column += 2;

                    while (pos < source.Length && source[pos] != '\n')
                    {
                        pos++;
                        column++;
                    }

                    NextToken();
                    return;
                }
                else if (source[pos + 1] == '*')
                {
                    // Multi-line comment
                    pos += 2; // Skip /*
                    column += 2;

                    while (pos < source.Length - 1 && !(source[pos] == '*' && source[pos + 1] == '/'))
                    {
                        if (source[pos] == '\n')
                        {
                            pos++;
                            line++;
                            column = 1;
                        }
                        else
                        {
                            pos++;
                            column++;
                        }
                    }

                    if (pos >= source.Length - 1)
                    {
                        throw new ParseException("Unterminated multi-line comment", line, column, GetSourceSnippet());
                    }

                    pos += 2; // Skip */
                    column += 2;

                    NextToken();
                    return;
                }
            }

            // Operators and punctuation
            switch (c)
            {
                case ';':
                case ',':
                case '(':
                case ')':
                case '{':
                case '}':
                case '[':
                case ']':
                case ':':
                case '.':
                    currentToken = c.ToString();
                    pos++;
                    column++;
                    break;

                case '+':
                case '-':
                case '*':
                case '/':
                case '%':
                case '!':
                    currentToken = c.ToString();
                    pos++;
                    column++;
                    break;

                case '=':
                    pos++;
                    column++;
                    if (pos < source.Length && source[pos] == '=')
                    {
                        currentToken = "==";
                        pos++;
                        column++;
                    }
                    else
                    {
                        currentToken = "=";
                    }
                    break;

                case '<':
                    pos++;
                    column++;
                    if (pos < source.Length && source[pos] == '=')
                    {
                        currentToken = "<=";
                        pos++;
                        column++;
                    }
                    else
                    {
                        currentToken = "<";
                    }
                    break;

                case '>':
                    pos++;
                    column++;
                    if (pos < source.Length && source[pos] == '=')
                    {
                        currentToken = ">=";
                        pos++;
                        column++;
                    }
                    else
                    {
                        currentToken = ">";
                    }
                    break;

                case '&':
                    pos++;
                    column++;
                    if (pos < source.Length && source[pos] == '&')
                    {
                        currentToken = "&&";
                        pos++;
                        column++;
                    }
                    else
                    {
                        throw new ParseException("Expected '&' after '&'", line, column, GetSourceSnippet());
                    }
                    break;

                case '|':
                    pos++;
                    column++;
                    if (pos < source.Length && source[pos] == '|')
                    {
                        currentToken = "||";
                        pos++;
                        column++;
                    }
                    else
                    {
                        throw new ParseException("Expected '|' after '|'", line, column, GetSourceSnippet());
                    }
                    break;

                default:
                    throw new ParseException($"Unexpected character: {c}", line, column, GetSourceSnippet());
            }
        }

        private void SkipWhitespace()
        {
            while (pos < source.Length && char.IsWhiteSpace(source[pos]))
            {
                if (source[pos] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }

                pos++;
            }
        }

        private void Expect(string expected)
        {
            if (currentToken != expected)
            {
                throw new ParseException($"Expected '{expected}', got '{currentToken}'", line, column, GetSourceSnippet());
            }

            NextToken();
        }

        private string ExpectIdentifier()
        {
            if (currentToken != "IDENTIFIER")
            {
                throw new ParseException($"Expected identifier, got '{currentToken}'", line, column, GetSourceSnippet());
            }

            var identifier = (string)currentValue;
            NextToken();
            return identifier;
        }

        private string GetSourceSnippet()
        {
            int snippetStart = Math.Max(0, pos - 20);
            int snippetEnd = Math.Min(source.Length, pos + 20);

            var snippet = source.Substring(snippetStart, snippetEnd - snippetStart);
            var pointer = new string(' ', Math.Min(20, pos - snippetStart)) + "^";

            return snippet + "\n" + pointer;
        }
    }

    #region AST Nodes
    public abstract class AstNode { }

    public class ActionProgram : AstNode
    {
        public List<AstNode> Statements { get; } = new List<AstNode>();
    }

    public class VariableDeclaration : AstNode
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public AstNode Initializer { get; set; }
    }

    public class ParameterDeclaration
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }

    public class FunctionDeclaration : AstNode
    {
        public string Name { get; set; }
        public List<ParameterDeclaration> Parameters { get; set; }
        public string ReturnType { get; set; }
        public List<AstNode> Body { get; set; }
    }

    public class ClassDeclaration : AstNode
    {
        public string Name { get; set; }
        public string SuperClass { get; set; }
        public List<FunctionDeclaration> Methods { get; set; }
        public List<VariableDeclaration> Properties { get; set; }
    }

    public class ImportStatement : AstNode
    {
        public string Path { get; set; }
    }

    public class ReturnStatement : AstNode
    {
        public AstNode Value { get; set; }
    }

    public class IfStatement : AstNode
    {
        public AstNode Condition { get; set; }
        public List<AstNode> ThenBranch { get; set; }
        public List<AstNode> ElseBranch { get; set; }
    }

    public class WhileStatement : AstNode
    {
        public AstNode Condition { get; set; }
        public List<AstNode> Body { get; set; }
    }

    public class ForStatement : AstNode
    {
        public AstNode Initializer { get; set; }
        public AstNode Condition { get; set; }
        public AstNode Increment { get; set; }
        public List<AstNode> Body { get; set; }
    }

    public class SwitchStatement : AstNode
    {
        public AstNode Discriminant { get; set; }
        public List<SwitchCase> Cases { get; set; }
    }

    public class SwitchCase
    {
        public AstNode Test { get; set; }
        public List<AstNode> Consequent { get; set; }
    }

    public class BlockStatement : AstNode
    {
        public List<AstNode> Statements { get; set; }
    }

    public class ExpressionStatement : AstNode
    {
        public AstNode Expression { get; set; }
    }

    public class BinaryExpression : AstNode
    {
        public AstNode Left { get; set; }
        public string Operator { get; set; }
        public AstNode Right { get; set; }
    }

    public class UnaryExpression : AstNode
    {
        public string Operator { get; set; }
        public AstNode Operand { get; set; }
    }

    public class AssignmentExpression : AstNode
    {
        public AstNode Target { get; set; }
        public AstNode Value { get; set; }
    }

    public class CallExpression : AstNode
    {
        public AstNode Callee { get; set; }
        public List<AstNode> Arguments { get; set; }
    }

    public class MemberExpression : AstNode
    {
        public AstNode Object { get; set; }
        public string PropertyName { get; set; }
    }

    public class Identifier : AstNode
    {
        public string Name { get; set; }
    }

    public class Literal : AstNode
    {
        public object Value { get; set; }
    }

    public class ObjectLiteral : AstNode
    {
        public List<PropertyDefinition> Properties { get; set; }
    }

    public class PropertyDefinition
    {
        public string Name { get; set; }
        public AstNode Value { get; set; }
    }
    #endregion

    #region Type System
    public abstract class TypeDefinition
    {
        public string Name { get; }

        protected TypeDefinition(string name)
        {
            Name = name;
        }
    }

    public class PrimitiveType : TypeDefinition
    {
        public Type ClrType { get; }

        public PrimitiveType(string name, Type clrType) : base(name)
        {
            ClrType = clrType;
        }
    }

    public class ClassType : TypeDefinition
    {
        public string SuperClassName { get; }
        private Dictionary<string, FunctionDeclaration> methods = new Dictionary<string, FunctionDeclaration>();
        private Dictionary<string, string> properties = new Dictionary<string, string>();

        public ClassType(string name, string superClassName = null) : base(name)
        {
            SuperClassName = superClassName;
        }

        public void AddMethod(string name, FunctionDeclaration method)
        {
            methods[name] = method;
        }

        public FunctionDeclaration GetMethod(string name)
        {
            if (methods.TryGetValue(name, out var method))
            {
                return method;
            }
            return null;
        }

        public void AddProperty(string name, string type)
        {
            properties[name] = type;
        }

        public string GetPropertyType(string name)
        {
            if (properties.TryGetValue(name, out var type))
            {
                return type;
            }
            return null;
        }

        public bool IsSubclassOf(string className)
        {
            if (Name == className)
                return true;

            if (SuperClassName == null)
                return false;

            // TODO: check the inheritance chain :(
            return SuperClassName == className;
        }

        public Dictionary<string, string> GetAllProperties()
        {
            return new Dictionary<string, string>(properties);
        }

        public Dictionary<string, FunctionDeclaration> GetAllMethods()
        {
            return new Dictionary<string, FunctionDeclaration>(methods);
        }
    }
    #endregion

    /// <summary>
    /// Represents a function in the Action language
    /// </summary>
    public class ActionFunction
    {
        public string Name { get; }
        public List<ParameterDeclaration> Parameters { get; }
        public string ReturnType { get; }
        public List<AstNode> Body { get; }
        public Environment Closure { get; }

        public ActionFunction(string name, List<ParameterDeclaration> parameters, string returnType, List<AstNode> body, Environment closure)
        {
            Name = name;
            Parameters = parameters;
            ReturnType = returnType;
            Body = body;
            Closure = closure;
        }
    }

    /// <summary>
    /// Represents an imported module
    /// </summary>
    public class ImportedModule
    {
        public string Path { get; }
        public Environment ExportedEnvironment { get; }

        public ImportedModule(string path, Environment exportedEnvironment)
        {
            Path = path;
            ExportedEnvironment = exportedEnvironment;
        }
    }
}