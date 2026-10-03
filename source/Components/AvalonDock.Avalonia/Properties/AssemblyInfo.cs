using System;
using System.Runtime.CompilerServices;
using Avalonia.Metadata;

[assembly: CLSCompliant(false)]

[assembly: XmlnsPrefix("https://github.com/Dirkster99/AvalonDock", "avalonDock")]
[assembly: XmlnsDefinition("https://github.com/Dirkster99/AvalonDock", "AvalonDock")]
[assembly: XmlnsDefinition("https://github.com/Dirkster99/AvalonDock", "AvalonDock.Controls")]
[assembly: XmlnsDefinition("https://github.com/Dirkster99/AvalonDock", "AvalonDock.Converters")]
[assembly: XmlnsDefinition("https://github.com/Dirkster99/AvalonDock", "AvalonDock.Layout")]
[assembly: XmlnsDefinition("https://github.com/Dirkster99/AvalonDock", "AvalonDock.Themes")]

// The headless test project is signed with this repository's sn.snk, so it is named by that key.
[assembly: InternalsVisibleTo("AvalonDock.Avalonia.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100d59d8147eb2015ca98a92da860fd766d101271d8c2f545894870fd6183255737d79347bbf5250291ae75651e11501b7452ee003b80b936614cdda51db8eb6f8fde913e67d45395b480a992be17bf04744a7fe803ea131b925dcf84a73d22264352eca7c3fcf9387f3eee1d60ac7974f04866e6c72928dc0609abe341f92cbfb5")]
