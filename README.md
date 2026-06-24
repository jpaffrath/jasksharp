<p>
    <a href="https://github.com/jpaffrath/jasksharp/releases">
        <img src="https://img.shields.io/badge/version-2.0.0-green.svg"
             alt="Version Badge">
    </a>
    <a href="https://www.gnu.org/licenses/gpl-3.0.html">
        <img src="https://img.shields.io/badge/license-GPL--3.0-blue.svg"
             alt="License Badge">
    </a>
    <a href="https://java.com/">
        <img src="https://img.shields.io/badge/required-.NET--10-purple.svg"
             alt="Java Badge">
    </a>
</p>

# Introduction

This is a reimplementation of the [jpaffrath/jask](https://github.com/jpaffrath/jask) interpreter in C#, making this version 2 of the jask language.
I decided to rewrite the entire interpreter because the first version of jask was non-intuitive, very limited and I wanted to dig deeper into learning C# and the dotnet framework.

This implementation uses the platform independent dotnet framework version 10 and has no dependencies to other libraries.

# Changes to jask 1.0.0

## assign has been removed
New and already existing variables must now be assigned with store
```pseudo
store 100 to myNum
store "Test" to myNum
```

## Arithmetic has been simplified and calculations has been enhanced
In jask version 1.0.0, one has to use the keywords plus, minus, divide, etc. for simple arithmetic calculations.
This has been changed vor a better readability:
```pseudo
store 100 * 2.123 in myNum
store myNum / myNum in result
```
Additionally, arithmetic operations in jask 1.0.0 were heavily limited. Version 2.0.0 allows complex statements:
```pseudo
store 2 + 2.123 * (2.3 + 4 / 5) in myNum
store myNum / myNum in result
```

## Loops have been simplified
A simple for-loop can be invoked like this:
```pseudo
; per default, increment with 1

for i from 1 to n
    print(i)
endfor

; define own incrementation function

for i from 0 to 100 with i * 2
    print(i)
endfor
```

## Function parameters have types and are separated by a comma
```pseudo
function greetNumber(name:string, age:number)
    print("Hello, " + name + ". You are " + value + " years old.")
end

greetNumber("Bob", 31)
```
Notice: If the type of the parameter is not fix, `:any` can be used but the type should be checked before using it.

# Use
To use the interactive mode, invoke jask:
```terminal
dotnet run
```
Exit the interactive mode with the exit command.
jask can interpret files:
```pseudo
dotnet run examples/simple.jask
```
