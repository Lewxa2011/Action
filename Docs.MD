# Action Language Documentation

## Table of Contents

1. [Introduction](#introduction)
2. [Getting Started](#getting-started)

   * [Prerequisites](#prerequisites)
   * [Building & Running](#building--running)
   * [REPL & CLI Usage](#repl--cli-usage)
3. [Language Overview](#language-overview)

   * [Primitive Types](#primitive-types)
   * [Variables & Declarations](#variables--declarations)
   * [Expressions & Operators](#expressions--operators)
   * [Functions & Closures](#functions--closures)
   * [Classes, Inheritance & Interfaces](#classes-inheritance--interfaces)
   * [Control Flow](#control-flow)
   * [Modules & Imports](#modules--imports)
4. [Standard Library Reference](#standard-library-reference)

   * [Built-in Functions](#built-in-functions)
   * [I/O Utilities](#io-utilities)
   * [Collections & Algorithms](#collections--algorithms)
   * [Math & String Utilities](#math--string-utilities)
5. [Advanced Features & Edge Cases](#advanced-features--edge-cases)
6. [Examples & Tutorials](#examples--tutorials)

   * [Hello World](#hello-world)
   * [Factorial Function](#factorial-function)
   * [Generic Swap Function](#generic-swap-function)
   * [File I/O Example](#file-io-example)
   * [Module & REPL Script](#module--repl-script)
7. [Error Handling](#error-handling)
8. [Appendix: Grammar (EBNF)](#appendix-grammar-ebnf)

---

## Introduction

Action is a lightweight, statically‑typed scripting language implemented in C#. It features:

* **Simple Syntax:** Clear, C‑style syntax for easy adoption
* **Static Typing:** Compile‑time type checks, with optional type inference in locals
* **OOP Support:** Single inheritance, interfaces, and first‑class functions
* **Modules & REPL:** Rapid prototyping with file modules or interactive REPL
* **Extensible Stdlib:** Collection utilities, file I/O, math, and string helpers

## Getting Started

### Prerequisites

* [.NET 6.0 SDK](https://dotnet.microsoft.com/) or later installed
* Clone the [ActionLanguage](https://github.com/your-org/ActionLanguage) repo

### Building & Running

```bash
git clone https://github.com/Lewxa2011/Action
cd ActionLanguage
dotnet build
```

### REPL & CLI Usage

* Launch the REPL:

  ```bash
  dotnet run --project ActionLanguage -- repl
  ```
* Execute a script file:

  ```bash
  dotnet run --project ActionLanguage -- run path/to/script.action
  ```
* In REPL:

  * Prompt: `>>>` for new statements, `...` inside open blocks
  * `:help` for commands (`:exit`, `:load <file>`, `:clear`)

Example:

```text
>>> var x: int = 42;
>>> print(x * 2);
84
>>> :exit
```

## Language Overview

### Primitive Types

| Type     | Description                    |
| -------- | ------------------------------ |
| `int`    | 32-bit signed integer          |
| `uint`   | 32-bit unsigned integer        |
| `bool`   | `true`/`false` values          |
| `string` | UTF-8 text                     |
| `void`   | No value (for function return) |

Type inference: `var id = 10; // id is int`

### Variables & Declarations

```action
var count: int = 0;
var name: string;
var flag = true;    // inferred as bool
```

### Expressions & Operators

* **Arithmetic:** `+`, `-`, `*`, `/`, `%`
* **Comparison:** `<`, `<=`, `>`, `>=`, `==`, `!=`
* **Logical:** `&&`, `||`, `!`
* **String:** `+` for concatenation
* **Ternary:** `cond ? expr1 : expr2`

### Functions & Closures

```action
function add(a: int, b: int): int { return a + b; }
var square = function(x: int): int { return x * x; };
```

Supports optional named parameters and default values:

```action
function greet(name: string = "Guest"): void {
    print("Hello, " + name);
}
```

### Classes, Inheritance & Interfaces

```action
interface IPrintable { function toString(): string; }

class Animal implements IPrintable {
    var name: string;
    function speak(): void { print("..."); }
    function toString(): string { return "Animal(" + name + ")"; }
}

class Dog extends Animal {
    function speak(): void { print(name + " says woof"); }
}
```

* **Single inheritance**, **multiple interfaces**.
* **Constructors** via `new ClassName(args)` with optional overloading.
* **Visibility:** `public` (default) and `private` on members.

### Control Flow

* `if` / `else if` / `else`
* `while`, `do while`
* `for (init; cond; step)`
* `switch (expr) { case ...: ...; default: ...; }`

### Modules & Imports

Split into `.action` files:

```action
// utils.action
function max(a: int, b: int): int { return a > b ? a : b; }
```

```action
// main.action
import "utils.action";
print(max(3, 7));
```

## Standard Library Reference

### Built-in Functions

| Function            | Description                      |
| ------------------- | -------------------------------- |
| `print(...)`        | Print values, newline-terminated |
| `println(...)`      | Alias for `print`                |
| `len(x)`            | Length of string or collection   |
| `typeof(x)`         | Return string name of type       |
| `assert(cond, msg)` | Abort if `cond` false            |

### I/O Utilities

In `io` module (auto-imported):

* `readAllText(path: string): string`
* `writeAllText(path: string, text: string): void`
* `readLines(path: string): string[]`
* `writeLines(path: string, lines: string[]): void`

### Collections & Algorithms

In `collections` module:

* `List<T>`: dynamic array (`add`, `remove`, `contains`)
* `Map<K,V>`: hash map (`get`, `set`, `keys`, `values`)
* Utility funcs: `sort(list, cmp)`, `filter(list, fn)`, `map(list, fn)`

### Math & String Utilities

In `math` and `string` modules:

* `math.abs(x)`, `math.sqrt(x)`, `math.pow(x,y)`, `math.sin(x)`, etc.
* `string.substring(s, start, len)`, `string.split(s, sep)`, `string.replace(s, old, new)`

## Advanced Features & Edge Cases

* **Type Casting & Generics:** Basic generics for functions and collections.
* **Overflow Behavior:** Ints throw `RuntimeException` on overflow unless compiled with `-checked` flag.
* **Short-circuit Logic:** `&&` / `||` stop evaluating early.
* **Closure Captures:** Captured variables are by reference, mutable in closures.
* **Null Safety:** All non-primitive variables default to `null`; accessing null fields throws.
* **Tail Call Optimization:** Recursive tail calls are optimized when compiling in `--optimize` mode.

## Examples & Tutorials

### Hello World

```action
print("Hello, World!");
```

### Factorial Function

```action
function factorial(n: int): int {
    return n <= 1 ? 1 : n * factorial(n - 1);
}
print(factorial(6)); // 720
```

### Generic Swap Function

```action
function swap<T>(a: T, b: T): (T, T) {
    return (b, a);
}
var (x, y) = swap<int>(3, 7);
print(x, y); // 7 3
```

### File I/O Example

```action
import "io";
var path = "data.txt";
writeAllText(path, "Line1\nLine2");
var lines = readLines(path);
for (var l: string in lines) {
    print(l);
}
```

### Module & REPL Script

```action
// mathutils.action
function fib(n: int): int {
    var a = 0; var b = 1;
    for (var i = 0; i < n; i++) {
        var t = a; a = b; b = t + b;
    }
    return a;
}
```

```text
>>> import "mathutils.action";
>>> print(fib(10));
55
```

## Error Handling

* **Compile Errors (ParseException):** Syntax issues with line/column.
* **Type Errors:** Mismatched types at compile time.
* **RuntimeException:** Division by zero, null reference, overflow, assert failures.

## Appendix: Grammar (EBNF)

```ebnf
Program       ::= Statement* EOF;
Statement     ::= VarDecl | FuncDecl | ClassDecl | ExprStmt | ImportStmt;
ImportStmt    ::= 'import' STRING_LITERAL ';';
VarDecl       ::= 'var' IDENT ':' Type ( '=' Expression )? ';';
FuncDecl      ::= 'function' IDENT '(' ParamList? ')' ( ':' Type )? Block;
ClassDecl     ::= 'class' IDENT ( 'extends' IDENT )? ( 'implements' IdentList )? ClassBody;
Block         ::= '{' Statement* '}';
ParamList     ::= Param ( ',' Param )*;
Param         ::= IDENT ':' Type;
ExprStmt      ::= Expression ';';
Expression    ::= Assignment;
Assignment    ::= Conditional ( ( '=' | '+=' | '-=' ) Assignment )?;
Conditional   ::= LogicalOr ( '?' Expression ':' Expression )?;
LogicalOr     ::= LogicalAnd ( '||' LogicalAnd )*;
LogicalAnd    ::= Equality ( '&&' Equality )*;
Equality      ::= Relational ( ( '==' | '!=' ) Relational )*;
Relational    ::= Additive ( ( '<' | '<=' | '>' | '>=' ) Additive )*;
Additive      ::= Multiplicative ( ( '+' | '-' ) Multiplicative )*;
Multiplicative::= Unary ( ( '*' | '/' | '%' ) Unary )*;
Unary         ::= ( '!' | '-' ) Unary | Primary;
Primary       ::= INTEGER_LITERAL | STRING_LITERAL | 'true' | 'false'
                | IDENT | '(' Expression ')' | FunctionLiteral | NewExpr;
FunctionLiteral ::= 'function' '(' ParamList? ')' ( ':' Type )? Block;
NewExpr       ::= 'new' IDENT '(' ArgList? ')';
ArgList       ::= Expression ( ',' Expression )*;
Type          ::= IDENT ( '<' Type ( ',' Type )* '>' )?;
ClassBody     ::= '{' ( VarDecl | FuncDecl )* '}';
IdentList     ::= IDENT ( ',' IDENT )*;
```

*End of Action Language Documentation*
