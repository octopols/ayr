using System;

namespace Ayr.Ui.Pages
{
    /// <summary>What a page needs from the window around it.</summary>
    public interface IPageHost
    {
        UiBackend Backend { get; }

        /// <summary>Runs one change at a time and refreshes every page afterwards, showing an error if it fails.</summary>
        void Apply(Func<string> change);

        /// <summary>Reads everything again, for changes made outside the window's own Apply, such as in a dialog.</summary>
        void RefreshState();

        int Scale(int pixels);
    }
}
