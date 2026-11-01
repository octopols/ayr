using System;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>
    /// Tells screen readers what an owner-drawn control shows. Windows can't read pixels, so the name, the current value
    /// and states such as "checked" have to be supplied by the control itself.
    /// </summary>
    public sealed class OwnerDrawnAccessibility : Control.ControlAccessibleObject
    {
        private readonly Func<string> _name;
        private readonly Func<string> _value;
        private readonly Func<AccessibleStates> _states;

        public OwnerDrawnAccessibility(Control owner, Func<string> name = null, Func<string> value = null, Func<AccessibleStates> states = null)
            : base(owner)
        {
            _name = name;
            _value = value;
            _states = states;
        }

        public override string Name => Owner.AccessibleName ?? _name?.Invoke() ?? base.Name;

        public override string Value => _value?.Invoke() ?? base.Value;

        public override AccessibleStates State => base.State | (_states?.Invoke() ?? AccessibleStates.None);
    }
}
