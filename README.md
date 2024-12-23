# WTDawson.EventPipes
I made a blog post about `WTDawson.EventPipes`: [View](https://go.wtdawson.info/dd2060)

`WTDawson.EventPipes` is a C# Class library designed to make a named pipe instance event based to make it easier for interprocess communication using named pipes.

## Getting started
To get started, add the NuGet package to your project by either running the command below or if you are using visual studio follow these instructions:
- Right click your project or solution
- Find the button called something along the lines of "Manage NuGet Packages"
- Search for "WTDawson.EventPipes"
- Install it
Or run this command:
```bash
dotnet add package WTDawson.EventPipes
```

Once `WTDawson.EventPipes` is added to your project or solution, create an instance of `EventPipes`
and call the `Connect` or `ConnectAsync` function to begin attempting to connect the pipes.
For example:
```cs
using WTDawson.EVentPipes;

namespace Testing {
	internal class Program
	{
		static void Main(string[] args)
		{
			EventPipe eventPipe = new EventPipe("TestA", "TestB");
			eventPipe.Connect();
		}
	}
}
```
Then, in another project, do the same thing but swap the `primary` and `secondary` pipe names around, for example:
```cs
using WTDawson.EVentPipes;

namespace Testing {
	internal class Program
	{
		static void Main(string[] args)
		{
			EventPipe eventPipe = new EventPipe("TestB", "TestA");
			eventPipe.Connect();
		}
	}
}
```
You can then easily create and send events between the two, for example on both sides you could do:
```cs
eventPipe.On("msg", (bytes[] data) => {
	Console.WriteLine("Received " + Encoding.Default.GetString(data));
	eventPipe.Send("msg", data);
});
eventPipe.Send("msg", Encoding.Default.GetBytes("Hello"))
```
You can also then listen on system events, for example:
```cs
eventPipe.Once("connected", () => {
	Console.WriteLine("Pipe connected");
});

eventPipe.Once("disconnected", () => {
	Console.WriteLine("Pipe disconnected");
});
```
Available system events: `connected`, `disconnected`.