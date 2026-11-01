using System;
using System.Collections.ObjectModel;
using Ayr.Hardware;

namespace Ayr.App
{
    /// <summary>
    /// What each Fn key does. Until the user picks something, Fn+F7 opens Ayr, except when MSI's own software is still
    /// installed and listening for it: then Ayr stays out of the way, so one press never opens two apps. Fn+F12 does
    /// nothing until chosen, as it does in MSI's software.
    /// </summary>
    public sealed class FnKeyBindings
    {
        public static readonly ReadOnlyCollection<FnKeyAction> Choices = Array.AsReadOnly(new[]
        {
            FnKeyAction.Nothing, FnKeyAction.OpenAyr, FnKeyAction.NextPerformance, FnKeyAction.NextFanMode
        });

        private readonly DesiredState _desired;
        private readonly bool _msiUsesF7;

        public FnKeyBindings(DesiredState desired, bool msiUsesF7)
        {
            _desired = desired;
            _msiUsesF7 = msiUsesF7;
        }

        public static FnKeyBindings Current() => new FnKeyBindings(DesiredState.Load(), MsiSoftware.UsesFnF7);

        public FnKeyAction ActionFor(FnKey key) => _desired.ChosenFnAction(key) ?? DefaultFor(key);

        /// <summary>True when MSI's software also reacts to the key.</summary>
        public bool SharedWithMsi(FnKey key) => key == FnKey.F7 && _msiUsesF7;

        private FnKeyAction DefaultFor(FnKey key) =>
            key == FnKey.F7 && !_msiUsesF7 ? FnKeyAction.OpenAyr : FnKeyAction.Nothing;
    }
}
