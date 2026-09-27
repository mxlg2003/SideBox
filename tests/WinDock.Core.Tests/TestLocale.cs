using System.Runtime.CompilerServices;
using WinDock.Core;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace WinDock.Core.Tests;

internal static class TestLocale
{
    [ModuleInitializer]
    internal static void PinChinese() => UiLocale.Apply(UiLanguage.Chinese);
}
