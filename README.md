# Introduction

This is a reimplementation of the [jpaffrath/jask](https://github.com/jpaffrath/jask) interpreter in C#, making this version 2 of the jask language.
I decided to rewrite the entire interpreter because the first version of jask had some non-intuitive and annoying "features" and I wanted to dig deeper into learning C# and the dotnet framework.

This implementation uses the platform independent dotnet framework version 10 and has no dependencies to other libraries.

# Changes to jask 1.0.0

## assign has been removed
New and already existing variables can now be assigned with store
```Assembly
store 100 to myNum
store "Test" to myNum
```

## Arithmetic has been simplified and calculations has been enhanced
In jask version 1.0.0, one has to use plus, minus, divide, etc. for simple arithmetic calculations.
This has been changed vor a better readability:
```Assembly
store 100 * 2.123 in myNum
store myNum / myNum in result
```
Additionally, arithmetic operations in jask 1.0.0 were heavily limited. Version 2.0.0 allows complex statements:
```Assembly
store 2 + 2.123 * (2.3 + 4 / 5) in myNum
store myNum / myNum in result
```

## Loops have been simplified
A simple for-loop can be invoked like this:
```Assembly
// per default, increment with 1
for i from 1 to n
    print(i)
endfor

// define own incrementation function
for i from 0 to 100 with i * 2
    print(i)
endfor
```

## Function parameters have fixed types and are separated by a comma
```Assembly
function greetNumber(name:string, value:number)
    print("Hello, " + name + " with value " + value + "!")
end

greetNumber("Bob", 100)
```

# Use
To use the interactive mode, invoke jask:
```Assembly
dotnet run
```
Exit the interactive mode with the exit command.
jask can interpret files:
```Assembly
dotnet run examples/simple.jask
```
