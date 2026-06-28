namespace JaskLang;

public partial class Interpreter
{
    public delegate object? InternalFunctionDelegate(Expression.Call call);

    // dictionary for internal functions: name -> delegate
    private readonly Dictionary<string, InternalFunctionDelegate> _internalFunctions = [];

    private void initInternalFunctions()
    {
        // standard functions
        _internalFunctions["print"]       = CallInternalFunctionPrint;
        _internalFunctions["type"]        = CallInternalFunctionType;
        _internalFunctions["clock"]       = CallInternalFunctionClock;
        _internalFunctions["exit"]        = CallInternalFunctionExit;
        _internalFunctions["stringFrom"]  = CallInternalFunctionStringFrom;

        // list functions
        _internalFunctions["list"]         = CallInternalFunctionList;
        _internalFunctions["listSize"]     = CallInternalFunctionListSize;
        _internalFunctions["listAdd"]      = CallInternalFunctionListAdd;
        _internalFunctions["listGet"]      = CallInternalFunctionListGet;
        _internalFunctions["listGetRange"] = CallInternalFunctionListGetRange;
        _internalFunctions["listSet"]      = CallInternalFunctionListSet;
        _internalFunctions["listRemove"]   = CallInternalFunctionListRemove;
        _internalFunctions["listReverse"]  = CallInternalFunctionListReverse;
        _internalFunctions["listExtend"]   = CallInternalFunctionListExtend;

        // IO functions
        _internalFunctions["readInput"] = CallInternalFunctionReadInput; 
    }

    private object? CallInternalFunctionPrint(Expression.Call call)
    {
        if (call.Arguments.Count < 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'print' expects at least 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        // print all arguments
        var parts = new List<string>();
        foreach (var arg in call.Arguments)
        {
            parts.Add(Stringify(Evaluate(arg)));
        }

        Console.Write(string.Join("", parts));

        return null;
    }

    private object? CallInternalFunctionType(Expression.Call call)
    {
        if (call.Arguments.Count != 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'type' expects 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }
        
        object? value = Evaluate(call.Arguments[0]);

        return GetValueType(value);
    }

    private object? CallInternalFunctionList(Expression.Call call)
    {
        var list = new List<object?>();

        foreach (var arg in call.Arguments)
        {
            list.Add(Evaluate(arg));
        }

        return list;
    }

    private object? CallInternalFunctionListSize(Expression.Call call)
    {
        if (call.Arguments.Count != 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSize' expects 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSize' expects a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        return (double)list.Count;
    }

    private object? CallInternalFunctionListAdd(Expression.Call call)
    {
        if (call.Arguments.Count < 2)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listAdd' expects at least 2 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        // first argument must be a list
        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listAdd' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        // create a copy of the list to avoid modifying the original
        var newList = list.ToList();

        // add arguments to the list
        for (int i = 1; i < call.Arguments.Count; i++)
        {   
            object? value = Evaluate(call.Arguments[i]);
            newList.Add(value);
        }

        return newList;
    }

    private object? CallInternalFunctionListGet(Expression.Call call)
    {
        if (call.Arguments.Count != 2)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGet' expects 2 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGet' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        object? indexObj = Evaluate(call.Arguments[1]);
        if (indexObj is not double indexDouble)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGet' expects second argument to be a number, but got '{GetValueType(indexObj)}'", funcExpr!.Name);
        }

        int index = (int)indexDouble;
        if (index < 0 || index >= list.Count)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGet' index {index} is out of bounds for list of size {list.Count}", funcExpr!.Name);
        }

        return list[index];
    }

    private object? CallInternalFunctionListGetRange(Expression.Call call)
    {
        if (call.Arguments.Count != 3)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGetRange' expects 3 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGetRange' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        object? startIndexObj = Evaluate(call.Arguments[1]);
        if (startIndexObj is not double startIndexDouble)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGetRange' expects second argument to be a number, but got '{GetValueType(startIndexObj)}'", funcExpr!.Name);
        }

        object? endIndexObj = Evaluate(call.Arguments[2]);
        if (endIndexObj is not double endIndexDouble)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGetRange' expects third argument to be a number, but got '{GetValueType(endIndexObj)}'", funcExpr!.Name);
        }

        int startIndex = (int)startIndexDouble;
        int endIndex = (int)endIndexDouble;

        if (startIndex < 0 || endIndex >= list.Count || startIndex > endIndex)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGetRange' indices [{startIndex}, {endIndex}] are out of bounds for list of size {list.Count}", funcExpr!.Name);
        }

        return list.GetRange(startIndex, endIndex - startIndex + 1);
    }

    private object? CallInternalFunctionListSet(Expression.Call call)
    {
        if (call.Arguments.Count != 3)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSet' expects 3 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSet' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        object? indexObj = Evaluate(call.Arguments[1]);
        if (indexObj is not double indexDouble)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSet' expects second argument to be a number, but got '{GetValueType(indexObj)}'", funcExpr!.Name);
        }

        int index = (int)indexDouble;
        if (index < 0 || index >= list.Count)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSet' index {index} is out of bounds for list of size {list.Count}", funcExpr!.Name);
        }

        object? value = Evaluate(call.Arguments[2]);

        // create a copy of the list to avoid modifying the original
        var newList = list.ToList();
        newList[index] = value;

        return newList;
    }

    private object? CallInternalFunctionListRemove(Expression.Call call)
    {
        if (call.Arguments.Count != 2)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listRemove' expects 2 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listRemove' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        object? indexObj = Evaluate(call.Arguments[1]);
        if (indexObj is not double indexDouble)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listRemove' expects second argument to be a number, but got '{GetValueType(indexObj)}'", funcExpr!.Name);
        }

        int index = (int)indexDouble;
        if (index < 0 || index >= list.Count)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listRemove' index {index} is out of bounds for list of size {list.Count}", funcExpr!.Name);
        }

        // create a copy of the list to avoid modifying the original
        var newList = list.ToList();
        newList.RemoveAt(index);

        return newList;
    }

    private object? CallInternalFunctionListReverse(Expression.Call call)
    {
        if (call.Arguments.Count != 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listReverse' expects 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listReverse' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        // create a copy of the list to avoid modifying the original
        var newList = list.ToList();
        newList.Reverse();

        return newList;
    }

    private object? CallInternalFunctionListExtend(Expression.Call call)
    {
        if (call.Arguments.Count != 2)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listExtend' expects 2 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj1 = Evaluate(call.Arguments[0]);
        if (listObj1 is not List<object?> list1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listExtend' expects first argument to be a list, but got '{GetValueType(listObj1)}'", funcExpr!.Name);
        }

        object? listObj2 = Evaluate(call.Arguments[1]);
        if (listObj2 is not List<object?> list2)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listExtend' expects second argument to be a list, but got '{GetValueType(listObj2)}'", funcExpr!.Name);
        }

        // create a copy of the first list to avoid modifying the original
        var newList = list1.ToList();
        newList.AddRange(list2);

        return newList;
    }

    private object? CallInternalFunctionClock(Expression.Call call)
    {
        if (call.Arguments.Count != 0)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'clock' expects 0 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    private object? CallInternalFunctionReadInput(Expression.Call call)
    {
        if (call.Arguments.Count > 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'readInput' expects 0 or 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        // if there's one argument, print it as a prompt
        if (call.Arguments.Count == 1)
        {
            object? promptValue = Evaluate(call.Arguments[0]);
            Console.Write(Stringify(promptValue));
        }

        return Console.ReadLine();
    }

    private object? CallInternalFunctionStringFrom(Expression.Call call)
    {
        if (call.Arguments.Count != 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'stringFrom' expects 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? argValue = Evaluate(call.Arguments[0]);
        return Stringify(argValue);
    }

    private object? CallInternalFunctionExit(Expression.Call call)
    {
        if (call.Arguments.Count != 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'exit' expects 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? argValue = Evaluate(call.Arguments[0]);
        if (argValue is not double d)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'exit' expects an integer argument, but got '{GetValueType(argValue)}'", funcExpr!.Name);
        }

        Environment.Exit((int)d);

        // this line will never be reached
        return null;
    }
}