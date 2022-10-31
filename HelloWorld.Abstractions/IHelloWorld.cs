using Orleans;

namespace HelloWorld.Abstractions;

public interface IHelloWorld : IGrainWithStringKey
{
    Task<string> SayHelloWorld();
}
