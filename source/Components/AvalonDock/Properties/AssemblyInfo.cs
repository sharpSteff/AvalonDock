using System;
#if AVALONIA
using System.Runtime.CompilerServices;
using Avalonia.Metadata;
#else
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Markup;
#endif

#if AVALONIA
[assembly: CLSCompliant(false)]
#else
// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
[assembly: CLSCompliant(true)]
#endif

#if AVALONIA
[assembly: XmlnsPrefix("https://github.com/Dirkster99/AvalonDock", "avalonDock")]
#else
// In order to begin building localizable applications, set 
// <UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file
// inside a <PropertyGroup>.  For example, if you are using US english
// in your source files, set the <UICulture> to en-US.  Then uncomment
// the NeutralResourceLanguage attribute below.  Update the "en-US" in
// the line below to match the UICulture setting in the project file.

// [assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]
[assembly: ThemeInfo(
	ResourceDictionaryLocation.None, // where theme specific resource dictionaries are located
									 // (used if a resource is not found in the page, 
									 // or application resource dictionaries)
	ResourceDictionaryLocation.SourceAssembly) // where the generic resource dictionary is located
											  // (used if a resource is not found in the page, 
											  // app, or any theme specific resource dictionaries)
]

[assembly: XmlnsPrefix("https://github.com/Dirkster99/AvalonDock", "avalondock")]
#endif
[assembly: XmlnsDefinition("https://github.com/Dirkster99/AvalonDock", "AvalonDock")]
[assembly: XmlnsDefinition("https://github.com/Dirkster99/AvalonDock", "AvalonDock.Controls")]
[assembly: XmlnsDefinition("https://github.com/Dirkster99/AvalonDock", "AvalonDock.Converters")]
[assembly: XmlnsDefinition("https://github.com/Dirkster99/AvalonDock", "AvalonDock.Layout")]
[assembly: XmlnsDefinition("https://github.com/Dirkster99/AvalonDock", "AvalonDock.Themes")]
#if AVALONIA

// The headless test project is signed with this repository's sn.snk, so it is named by that key.
[assembly: InternalsVisibleTo("AvalonDock.Avalonia.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100d59d8147eb2015ca98a92da860fd766d101271d8c2f545894870fd6183255737d79347bbf5250291ae75651e11501b7452ee003b80b936614cdda51db8eb6f8fde913e67d45395b480a992be17bf04744a7fe803ea131b925dcf84a73d22264352eca7c3fcf9387f3eee1d60ac7974f04866e6c72928dc0609abe341f92cbfb5")]
[assembly: InternalsVisibleTo("AvalonDock.Avalonia.PlatformTests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100d59d8147eb2015ca98a92da860fd766d101271d8c2f545894870fd6183255737d79347bbf5250291ae75651e11501b7452ee003b80b936614cdda51db8eb6f8fde913e67d45395b480a992be17bf04744a7fe803ea131b925dcf84a73d22264352eca7c3fcf9387f3eee1d60ac7974f04866e6c72928dc0609abe341f92cbfb5")]
#else
[assembly: XmlnsPrefix("http://schemas.microsoft.com/winfx/2006/xaml/presentation", "avalondock")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/winfx/2006/xaml/presentation", "AvalonDock")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/winfx/2006/xaml/presentation", "AvalonDock.Controls")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/winfx/2006/xaml/presentation", "AvalonDock.Converters")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/winfx/2006/xaml/presentation", "AvalonDock.Layout")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/winfx/2006/xaml/presentation", "AvalonDock.Themes")]
#endif