using HelloWorld.Abstractions;
using Orleans;
using Orleans.Runtime;

namespace HelloWorld.Entities;

public class HelloWorld : Grain, IHelloWorld
{
    public HelloWorld([PersistentState("hello", ConfigurationConstants.StorageName)] IPersistentState<HelloState> hello
    )
    {
        _hello = hello;
    }

    private readonly IPersistentState<HelloState> _hello;


    public async Task<string> SayHelloWorld()
    {
        var greeting = $"Hello, from {this.GetPrimaryKeyString()}";
        _hello.State.Greeting = greeting;
        await _hello.WriteStateAsync();
        return greeting;
    }
}
