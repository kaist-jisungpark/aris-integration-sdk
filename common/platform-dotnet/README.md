# platform-dotnet

This folder contains a solution that implements ARIS protocols as [.NET Standard](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) projects.

## Intent

Much of our internal code at Sound Metrics is written in native C++ or in .NET languages such as [C#](https://docs.microsoft.com/en-us/dotnet/csharp/index) and [F#](https://docs.microsoft.com/en-us/dotnet/fsharp/index). We are publishing this protocol implementation as we build out internal tooling.

## Roadmap

The future of this library is discussed [here](ROADMAP.md).

## Cross Platform

As .NET Standard runs on multiple platforms, much of the code in this solution will as well--excepting UI code, for which we depend on [WPF](https://docs.microsoft.com/en-us/dotnet/framework/wpf/index). However,

1. Our internal goal for running cross-platform--e.g., on Linux/ARM--is to do so for sanity checks.

1. .NET Core implementations may not yet have the necessary synchronization context or dispatcher support as found in .NET (desktop) Framework and WPF. These are required for the asynchronous callbacks on some APIs. We will post work-arounds for that as we go.

## History

**1.\_.\_-alpha** - This project is currently under development and has not yet been released. Until it exits alpha stage, APIs may break daily. Documentation may be lacking entirely.
